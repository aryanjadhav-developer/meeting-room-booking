using System.Net;
using System.Net.Http.Json;
using CabinBooking.Application.Dtos;
using CabinBooking.Domain.Enums;
using CabinBooking.IntegrationTests.Infrastructure;

namespace CabinBooking.IntegrationTests;

// ============================================================== 01 Registration
public class F01_Registration(TestApp app) : ApiTest(app)
{
    [Fact]
    public async Task TC01_registered_accounts_are_employees()
    {
        var s = await App.RegisterAsync("First Person");
        Assert.Equal(UserRole.Employee, s.User.Role);
    }

    [Fact]
    public async Task TC02_admin_is_seeded_from_configuration()
    {
        var admin = await App.LoginAdminAsync();
        Assert.Equal(UserRole.Admin, admin.User.Role);
    }

    [Fact]
    public async Task TC03_duplicate_email_is_rejected_with_409()
    {
        await App.RegisterAsync("Alice");
        var res = await App.CreateClient().PostAsJsonAsync("/api/auth/register", new { name = "Alice Again", email = "ALICE@example.test", password = TestApp.Password });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task TC04_short_password_is_rejected()
    {
        var res = await App.CreateClient().PostAsJsonAsync("/api/auth/register", new { name = "Alice", email = "alice@example.test", password = "short" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("at least 8", await DetailAsync(res));
    }

    [Fact]
    public async Task TC05_invalid_email_is_rejected()
    {
        var res = await App.CreateClient().PostAsJsonAsync("/api/auth/register", new { name = "Alice", email = "not-an-email", password = TestApp.Password });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}

// ============================================================== 02 Login & session
public class F02_LoginAndSession(TestApp app) : ApiTest(app)
{
    [Fact]
    public async Task TC01_valid_login_returns_token_that_works()
    {
        await App.RegisterAsync("Alice");
        var http = App.CreateClient();
        var res = await http.PostAsJsonAsync("/api/auth/login", new { email = "alice@example.test", password = TestApp.Password });
        var auth = await ReadAsync<AuthResponse>(res);
        http.DefaultRequestHeaders.Authorization = new("Bearer", auth.Token);

        var me = await http.GetFromJsonAsync<UserDto>("/api/auth/me", TestApp.Json);
        Assert.Equal("Alice", me!.Name);
    }

    [Fact]
    public async Task TC02_wrong_password_is_rejected()
    {
        await App.RegisterAsync("Alice");
        var res = await App.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = "alice@example.test", password = "WrongPass123" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("Invalid e-mail or password.", await DetailAsync(res));
    }

    [Fact]
    public async Task TC03_unknown_email_gives_same_generic_error()
    {
        var res = await App.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = "nobody@example.test", password = TestApp.Password });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("Invalid e-mail or password.", await DetailAsync(res));
    }

    [Fact]
    public async Task TC04_api_requires_a_token()
    {
        var http = App.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync("/api/cabins")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync("/api/bookings/mine")).StatusCode);
    }

    [Fact]
    public async Task TC05_deactivated_user_cannot_log_in()
    {
        var (admin, alice, _) = await SetupUsersAsync();
        await admin.Http.PutAsJsonAsync($"/api/users/{alice.User.Id}", new UpdateUserRequest(UserRole.Employee, false), TestApp.Json);

        var res = await App.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = "alice@example.test", password = TestApp.Password });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }
}

// ============================================================== 03 Cabins & live status
public class F03_CabinsAndLiveStatus(TestApp app) : ApiTest(app)
{
    [Fact]
    public async Task TC01_lists_the_three_rooms_with_capacity_labels()
    {
        var s = await App.RegisterAsync("Alice");
        var cabins = await s.Http.GetFromJsonAsync<List<CabinDto>>("/api/cabins", TestApp.Json);
        Assert.Equal(["Cabin 3", "Conference Room", "Lunch Room"], cabins!.Select(c => c.Name));
        Assert.Equal(["3-4", "5+", "9+"], cabins!.Select(c => c.CapacityLabel));
    }

    [Fact]
    public async Task TC02_all_cabins_free_when_nothing_booked()
    {
        var s = await App.RegisterAsync("Alice");
        var status = await s.Http.GetFromJsonAsync<List<CabinStatusDto>>("/api/cabins/status", TestApp.Json);
        Assert.All(status!, c => Assert.False(c.IsOccupied));
    }

    [Fact]
    public async Task TC03_running_meeting_marks_cabin_occupied_with_who_and_until_when()
    {
        var s = await App.RegisterAsync("Alice");
        await PostAsync(s, "/api/bookings/quick", new QuickBookRequest(1, 30, "Client call"));

        var c1 = (await s.Http.GetFromJsonAsync<List<CabinStatusDto>>("/api/cabins/status", TestApp.Json))!.Single(c => c.Cabin.Id == 1);
        Assert.True(c1.IsOccupied);
        Assert.Equal("Alice", c1.Current!.BookedByName);
        Assert.Equal(At(10, 30), c1.Current.EndTime);
    }

    [Fact]
    public async Task TC04_shows_next_booking_of_the_day()
    {
        var s = await App.RegisterAsync("Alice");
        await BookOkAsync(s, 2, At(14), At(15), "Design review");

        var c2 = (await s.Http.GetFromJsonAsync<List<CabinStatusDto>>("/api/cabins/status", TestApp.Json))!.Single(c => c.Cabin.Id == 2);
        Assert.False(c2.IsOccupied);
        Assert.Equal("Design review", c2.Next!.Title);
    }

    [Fact]
    public async Task TC05_disabled_cabin_is_hidden_unless_requested()
    {
        var (admin, _, _) = await SetupUsersAsync();
        await admin.Http.PutAsJsonAsync("/api/cabins/3", new UpsertCabinRequest("Cabin 3", null, 8, [], "#ed6c02", false), TestApp.Json);

        Assert.Equal(2, (await admin.Http.GetFromJsonAsync<List<CabinDto>>("/api/cabins", TestApp.Json))!.Count);
        Assert.Equal(3, (await admin.Http.GetFromJsonAsync<List<CabinDto>>("/api/cabins?includeInactive=true", TestApp.Json))!.Count);
    }
}

// ============================================================== 14 Admin: cabins & users
public class F14_AdminCabinsAndUsers(TestApp app) : ApiTest(app)
{
    [Fact]
    public async Task TC01_admin_can_add_a_cabin()
    {
        var (admin, _, _) = await SetupUsersAsync();
        var res = await admin.Http.PostAsJsonAsync("/api/cabins", new UpsertCabinRequest("Phone Booth", "1 person", 1, ["Phone"], "#9c27b0", true), TestApp.Json);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.Equal(4, (await admin.Http.GetFromJsonAsync<List<CabinDto>>("/api/cabins", TestApp.Json))!.Count);
    }

    [Fact]
    public async Task TC02_duplicate_cabin_name_is_rejected()
    {
        var (admin, _, _) = await SetupUsersAsync();
        var res = await admin.Http.PostAsJsonAsync("/api/cabins", new UpsertCabinRequest("Lunch Room", null, 4, [], "#000000", true), TestApp.Json);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task TC03_employee_cannot_manage_cabins_or_users()
    {
        var (_, alice, _) = await SetupUsersAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await alice.Http.PostAsJsonAsync("/api/cabins", new UpsertCabinRequest("X", null, 2, [], "#000000", true), TestApp.Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await alice.Http.GetAsync("/api/users")).StatusCode);
    }

    [Fact]
    public async Task TC04_disabled_cabin_cannot_be_booked()
    {
        var (admin, alice, _) = await SetupUsersAsync();
        await admin.Http.PutAsJsonAsync("/api/cabins/2", new UpsertCabinRequest("Conference Room", null, 5, [], "#2e7d32", false), TestApp.Json);

        var res = await BookAsync(alice, 2, At(11), At(12));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("unavailable", await DetailAsync(res));
    }

    [Fact]
    public async Task TC05_admin_promotes_user_but_cannot_demote_self()
    {
        var (admin, alice, _) = await SetupUsersAsync();
        Assert.Equal(HttpStatusCode.OK, (await admin.Http.PutAsJsonAsync($"/api/users/{alice.User.Id}", new UpdateUserRequest(UserRole.Admin, true), TestApp.Json)).StatusCode);

        var relogin = await App.CreateClient().PostAsJsonAsync("/api/auth/login", new { email = "alice@example.test", password = TestApp.Password });
        Assert.Equal(UserRole.Admin, (await ReadAsync<AuthResponse>(relogin)).User.Role);

        var self = await admin.Http.PutAsJsonAsync($"/api/users/{admin.User.Id}", new UpdateUserRequest(UserRole.Employee, true), TestApp.Json);
        Assert.Equal(HttpStatusCode.BadRequest, self.StatusCode);
    }
}
