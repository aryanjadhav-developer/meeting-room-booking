using System.Net.Http.Json;
using CabinBooking.Application.Dtos;
using CabinBooking.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.SignalR.Client;

namespace CabinBooking.IntegrationTests;

// ============================================================== 16 Real-time updates (SignalR)
public class F16_RealtimeUpdates(TestApp app) : ApiTest(app)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// <summary>Connects as <paramref name="viewer"/> and returns a task completing with the first <paramref name="evt"/>.</summary>
    private async Task<(HubConnection Hub, Task<BookingDto> Received)> ListenAsync(Session viewer, string evt)
    {
        var hub = App.CreateHubConnection(viewer.Token);
        var tcs = new TaskCompletionSource<BookingDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        hub.On<BookingDto>(evt, b => tcs.TrySetResult(b));
        await hub.StartAsync();
        return (hub, tcs.Task.WaitAsync(Timeout));
    }

    [Fact]
    public async Task TC01_connection_without_login_is_refused()
    {
        await App.RegisterAsync("Alice"); // starts the server
        await using var hub = App.CreateHubConnection(token: null);
        await Assert.ThrowsAnyAsync<Exception>(() => hub.StartAsync());
    }

    [Fact]
    public async Task TC02_other_users_see_a_new_booking_instantly()
    {
        var (_, alice, bob) = await SetupUsersAsync();
        var (hub, received) = await ListenAsync(bob, "BookingCreated");
        await using var _ = hub;

        await BookOkAsync(alice, 2, At(11), At(12), "Sprint planning");

        var b = await received;
        Assert.Equal(("Alice", "Conference Room", "Sprint planning"), (b.BookedByName, b.CabinName, b.Title));
    }

    [Fact]
    public async Task TC03_extension_is_broadcast_with_new_end_time()
    {
        var (_, alice, bob) = await SetupUsersAsync();
        var booking = await BookOkAsync(alice, 1, At(11), At(12));
        var (hub, received) = await ListenAsync(bob, "BookingUpdated");
        await using var _ = hub;

        await PostAsync(alice, $"/api/bookings/{booking.Id}/extend", new ExtendBookingRequest(30));

        Assert.Equal(At(12, 30), (await received).EndTime);
    }

    [Fact]
    public async Task TC04_cancellation_is_broadcast()
    {
        var (_, alice, bob) = await SetupUsersAsync();
        var booking = await BookOkAsync(alice, 1, At(11), At(12));
        var (hub, received) = await ListenAsync(bob, "BookingCancelled");
        await using var _ = hub;

        await alice.Http.DeleteAsync($"/api/bookings/{booking.Id}");

        Assert.Equal(booking.Id, (await received).Id);
    }

    [Fact]
    public async Task TC05_cabin_changes_by_admin_are_broadcast()
    {
        var (admin, alice, _) = await SetupUsersAsync();
        await using var hub = App.CreateHubConnection(alice.Token);
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        hub.On("CabinsChanged", () => tcs.TrySetResult());
        await hub.StartAsync();

        await admin.Http.PutAsJsonAsync("/api/cabins/1", new UpsertCabinRequest("Focus Room", null, 4, ["Whiteboard"], "#1976d2", true), TestApp.Json);

        await tcs.Task.WaitAsync(Timeout);
    }
}
