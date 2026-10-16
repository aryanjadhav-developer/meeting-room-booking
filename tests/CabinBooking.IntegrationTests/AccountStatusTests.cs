using System.Net;
using System.Net.Http.Json;
using CabinBooking.Application.Dtos;
using CabinBooking.Domain.Enums;
using CabinBooking.IntegrationTests.Infrastructure;

namespace CabinBooking.IntegrationTests;

// ============================================================== 18 Deactivation & role changes apply immediately
/// <remarks>Regression tests: a deactivated user's still-unexpired token used to keep working.</remarks>
public class F18_AccountStatusEnforcement(TestApp app) : ApiTest(app)
{
    private static Task<HttpResponseMessage> SetStatus(Session admin, Session target, UserRole role, bool active) =>
        admin.Http.PutAsJsonAsync($"/api/users/{target.User.Id}", new UpdateUserRequest(role, active), TestApp.Json);

    [Fact]
    public async Task TC01_deactivated_user_cannot_book_with_existing_session()
    {
        var (admin, alice, _) = await SetupUsersAsync();
        await BookOkAsync(alice, 1, At(11), At(12)); // works while active

        await SetStatus(admin, alice, UserRole.Employee, active: false);

        var res = await BookAsync(alice, 2, At(13), At(14));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        Assert.Contains("deactivated", await DetailAsync(res));
    }

    [Fact]
    public async Task TC02_deactivated_user_cannot_view_or_change_anything()
    {
        var (admin, alice, _) = await SetupUsersAsync();
        var booking = await BookOkAsync(alice, 1, At(11), At(12));
        await SetStatus(admin, alice, UserRole.Employee, active: false);

        Assert.Equal(HttpStatusCode.Unauthorized, (await alice.Http.GetAsync("/api/cabins/status")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await alice.Http.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await alice.Http.DeleteAsync($"/api/bookings/{booking.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostAsync(alice, "/api/bookings/quick", new QuickBookRequest(2, 15, null))).StatusCode);
    }

    [Fact]
    public async Task TC03_reactivated_user_can_book_again()
    {
        var (admin, alice, _) = await SetupUsersAsync();
        await SetStatus(admin, alice, UserRole.Employee, active: false);
        Assert.Equal(HttpStatusCode.Unauthorized, (await BookAsync(alice, 1, At(11), At(12))).StatusCode);

        await SetStatus(admin, alice, UserRole.Employee, active: true);
        await BookOkAsync(alice, 1, At(11), At(12));
    }

    [Fact]
    public async Task TC04_removing_admin_rights_applies_without_signing_in_again()
    {
        var (admin, alice, _) = await SetupUsersAsync();
        await SetStatus(admin, alice, UserRole.Admin, active: true);
        var promoted = await App.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = "alice@example.test", password = TestApp.Password });
        var token = (await ReadAsync<AuthResponse>(promoted)).Token;
        alice.Http.DefaultRequestHeaders.Authorization = new("Bearer", token); // token says Admin
        Assert.Equal(HttpStatusCode.OK, (await alice.Http.GetAsync("/api/users")).StatusCode);

        await SetStatus(admin, alice, UserRole.Employee, active: true);

        Assert.Equal(HttpStatusCode.Forbidden, (await alice.Http.GetAsync("/api/users")).StatusCode);
    }

    [Fact]
    public async Task TC05_deactivated_user_cannot_connect_to_live_updates()
    {
        var (admin, alice, _) = await SetupUsersAsync();
        await SetStatus(admin, alice, UserRole.Employee, active: false);

        await using var hub = App.CreateHubConnection(alice.Token);
        await Assert.ThrowsAnyAsync<Exception>(() => hub.StartAsync());
    }
}
