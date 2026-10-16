using System.Net;
using System.Net.Http.Json;
using CabinBooking.Application.Dtos;
using CabinBooking.Domain.Enums;
using CabinBooking.IntegrationTests.Infrastructure;

namespace CabinBooking.IntegrationTests;

// ============================================================== 12 Availability, day view & my bookings
public class F12_AvailabilityAndLists(TestApp app) : ApiTest(app)
{
    private static Task<AvailabilityDto?> Availability(Session s, int cabin, string date) =>
        s.Http.GetFromJsonAsync<AvailabilityDto>($"/api/cabins/{cabin}/availability?date={date}", TestApp.Json);

    [Fact]
    public async Task TC01_empty_day_is_free_from_now_until_closing()
    {
        var alice = await App.RegisterAsync("Alice");
        var a = await Availability(alice, 1, "2026-10-21");
        var slot = Assert.Single(a!.FreeSlots);
        Assert.Equal((At(10), At(20)), (slot.Start, slot.End));
    }

    [Fact]
    public async Task TC02_shows_gaps_between_bookings()
    {
        var alice = await App.RegisterAsync("Alice");
        await BookOkAsync(alice, 1, At(11), At(12));
        await BookOkAsync(alice, 1, At(14), At(15));
        var a = await Availability(alice, 1, "2026-10-21");
        Assert.Equal([(At(10), At(11)), (At(12), At(14)), (At(15), At(20))], a!.FreeSlots.Select(s => (s.Start, s.End)));
    }

    [Fact]
    public async Task TC03_sunday_has_no_availability()
    {
        var alice = await App.RegisterAsync("Alice");
        Assert.Empty((await Availability(alice, 1, "2026-10-25"))!.FreeSlots);
    }

    [Fact]
    public async Task TC04_day_view_shows_everyones_bookings_but_not_cancelled()
    {
        var (_, alice, bob) = await SetupUsersAsync();
        await BookOkAsync(alice, 1, At(11), At(12));
        await BookOkAsync(bob, 2, At(13), At(14));
        var gone = await BookOkAsync(bob, 3, At(15), At(16));
        await bob.Http.DeleteAsync($"/api/bookings/{gone.Id}");

        var url = $"/api/bookings?from={Uri.EscapeDataString(At(0).ToString("O"))}&to={Uri.EscapeDataString(At(0, 0, 1).ToString("O"))}";
        var day = await alice.Http.GetFromJsonAsync<List<BookingDto>>(url, TestApp.Json);
        Assert.Equal(["Alice", "Bob"], day!.Select(b => b.BookedByName));
    }

    [Fact]
    public async Task TC05_my_bookings_shows_upcoming_and_history_on_request()
    {
        var (_, alice, bob) = await SetupUsersAsync();
        await BookOkAsync(alice, 1, At(10, 15), At(10, 45), "Morning");
        await BookOkAsync(alice, 1, At(16), At(17), "Afternoon");
        await BookOkAsync(bob, 2, At(11), At(12), "Bob's");
        App.Clock.Now = At(11);

        var upcoming = await alice.Http.GetFromJsonAsync<List<BookingDto>>("/api/bookings/mine", TestApp.Json);
        Assert.Equal(["Afternoon"], upcoming!.Select(b => b.Title));

        var all = await alice.Http.GetFromJsonAsync<List<BookingDto>>("/api/bookings/mine?includePast=true", TestApp.Json);
        Assert.Equal(["Morning", "Afternoon"], all!.Select(b => b.Title));
        Assert.Equal(BookingPhase.Completed, all![0].Phase);
    }
}

// ============================================================== 13 No-show auto release
public class F13_NoShowAutoRelease(TestApp app) : ApiTest(app)
{
    [Fact]
    public async Task TC01_not_released_within_grace_period()
    {
        var alice = await App.RegisterAsync("Alice");
        await BookOkAsync(alice, 1, At(10, 30), At(11, 30));
        App.Clock.Now = At(10, 39);
        Assert.Equal(0, await App.ReleaseNoShowsAsync());
    }

    [Fact]
    public async Task TC02_released_after_grace_period_and_marked_no_show()
    {
        var alice = await App.RegisterAsync("Alice");
        var b = await BookOkAsync(alice, 1, At(10, 30), At(11, 30));
        App.Clock.Now = At(10, 41);
        Assert.Equal(1, await App.ReleaseNoShowsAsync());

        var after = await alice.Http.GetFromJsonAsync<BookingDto>($"/api/bookings/{b.Id}", TestApp.Json);
        Assert.Equal(BookingStatus.NoShow, after!.Status);
    }

    [Fact]
    public async Task TC03_checked_in_meeting_is_kept()
    {
        var alice = await App.RegisterAsync("Alice");
        var b = await BookOkAsync(alice, 1, At(10, 30), At(11, 30));
        App.Clock.Now = At(10, 32);
        await PostAsync(alice, $"/api/bookings/{b.Id}/check-in");
        App.Clock.Now = At(11);
        Assert.Equal(0, await App.ReleaseNoShowsAsync());
    }

    [Fact]
    public async Task TC04_already_finished_meetings_are_not_touched()
    {
        var alice = await App.RegisterAsync("Alice");
        var b = await BookOkAsync(alice, 1, At(10, 15), At(10, 45));
        App.Clock.Now = At(11);
        Assert.Equal(0, await App.ReleaseNoShowsAsync());
        Assert.Equal(BookingStatus.Confirmed, (await alice.Http.GetFromJsonAsync<BookingDto>($"/api/bookings/{b.Id}", TestApp.Json))!.Status);
    }

    [Fact]
    public async Task TC05_released_cabin_can_be_booked_by_someone_else()
    {
        var (_, alice, bob) = await SetupUsersAsync();
        await BookOkAsync(alice, 1, At(10, 30), At(11, 30));
        App.Clock.Now = At(10, 45);
        await App.ReleaseNoShowsAsync();
        var res = await PostAsync(bob, "/api/bookings/quick", new QuickBookRequest(1, 30, "Took the free cabin"));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }
}

// ============================================================== 15 Utilisation report
public class F15_Reports(TestApp app) : ApiTest(app)
{
    private const string Day = "from=2026-10-21&to=2026-10-21";

    [Fact]
    public async Task TC01_utilisation_percentage_is_booked_over_office_hours()
    {
        var (admin, alice, _) = await SetupUsersAsync();
        await BookOkAsync(alice, 1, At(11), At(13));
        var r = await admin.Http.GetFromJsonAsync<UtilizationReportDto>($"/api/reports/utilization?{Day}", TestApp.Json);
        var c1 = r!.Cabins.Single(c => c.CabinId == 1);
        Assert.Equal(2, c1.BookedHours);
        Assert.Equal(11, c1.AvailableHours);
        Assert.Equal(18.2, c1.UtilizationPercent);
    }

    [Fact]
    public async Task TC02_counts_cancellations_and_no_shows()
    {
        var (admin, alice, _) = await SetupUsersAsync();
        var c = await BookOkAsync(alice, 2, At(14), At(15));
        await alice.Http.DeleteAsync($"/api/bookings/{c.Id}");
        await BookOkAsync(alice, 3, At(10, 15), At(11));
        App.Clock.Now = At(10, 30);
        await App.ReleaseNoShowsAsync();

        var r = await admin.Http.GetFromJsonAsync<UtilizationReportDto>($"/api/reports/utilization?{Day}", TestApp.Json);
        Assert.Equal(1, r!.Cabins.Single(x => x.CabinId == 2).CancelledCount);
        Assert.Equal(1, r.Cabins.Single(x => x.CabinId == 3).NoShowCount);
        Assert.Equal(0, r.Cabins.Single(x => x.CabinId == 3).BookedHours);
    }

    [Fact]
    public async Task TC03_top_bookers_ordered_by_hours()
    {
        var (admin, alice, bob) = await SetupUsersAsync();
        await BookOkAsync(bob, 1, At(11), At(12));
        await BookOkAsync(alice, 2, At(11), At(14));
        var r = await admin.Http.GetFromJsonAsync<UtilizationReportDto>($"/api/reports/utilization?{Day}", TestApp.Json);
        Assert.Equal(["Alice", "Bob"], r!.TopUsers.Select(u => u.Name));
        Assert.Equal(3, r.TopUsers[0].BookedHours);
    }

    [Fact]
    public async Task TC04_employee_cannot_view_reports()
    {
        var (_, alice, _) = await SetupUsersAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await alice.Http.GetAsync($"/api/reports/utilization?{Day}")).StatusCode);
    }

    [Fact]
    public async Task TC05_invalid_date_range_is_rejected()
    {
        var (admin, _, _) = await SetupUsersAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Http.GetAsync("/api/reports/utilization?from=2026-10-21&to=2026-10-01")).StatusCode);
    }
}
