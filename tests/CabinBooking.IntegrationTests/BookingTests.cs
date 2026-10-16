using System.Net;
using System.Net.Http.Json;
using CabinBooking.Application.Dtos;
using CabinBooking.Domain.Enums;
using CabinBooking.IntegrationTests.Infrastructure;

namespace CabinBooking.IntegrationTests;

// ============================================================== 04 Create booking
public class F04_CreateBooking(TestApp app) : ApiTest(app)
{
    [Fact]
    public async Task TC01_valid_booking_records_who_when_and_how_long()
    {
        var alice = await App.RegisterAsync("Alice");
        var b = await BookOkAsync(alice, 1, At(11), At(12, 30), "Sprint planning");

        Assert.Equal("Alice", b.BookedByName);
        Assert.Equal("alice@example.test", b.BookedByEmail);
        Assert.Equal(90, b.DurationMinutes);
        Assert.Equal(BookingPhase.Upcoming, b.Phase);
    }

    [Fact]
    public async Task TC02_outside_office_hours_is_rejected()
    {
        var alice = await App.RegisterAsync("Alice");
        var res = await BookAsync(alice, 1, At(19, 30), At(20, 30));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("office hours", await DetailAsync(res));
    }

    [Fact]
    public async Task TC03_start_in_the_past_is_rejected()
    {
        var alice = await App.RegisterAsync("Alice");
        var res = await BookAsync(alice, 1, At(9), At(9, 30));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("past", await DetailAsync(res));
    }

    [Fact]
    public async Task TC04_longer_than_four_hours_is_rejected()
    {
        var alice = await App.RegisterAsync("Alice");
        var res = await BookAsync(alice, 1, At(11), At(15, 15));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("at most 4 hours", await DetailAsync(res));
    }

    [Fact]
    public async Task TC05_sunday_is_rejected()
    {
        var alice = await App.RegisterAsync("Alice");
        var res = await BookAsync(alice, 1, At(11, 0, 4), At(12, 0, 4)); // 25 Oct 2026 = Sunday
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("Sunday", await DetailAsync(res));
    }
}

// ============================================================== 05 Double-booking protection
public class F05_DoubleBookingProtection(TestApp app) : ApiTest(app)
{
    [Fact]
    public async Task TC01_exact_same_slot_is_rejected_naming_the_holder()
    {
        var (_, alice, bob) = await SetupUsersAsync();
        await BookOkAsync(alice, 2, At(11), At(12), "Sprint planning");

        var res = await BookAsync(bob, 2, At(11), At(12));
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("Conference Room is already booked by Alice from 11:00 to 12:00 (Sprint planning).", await DetailAsync(res));
    }

    [Fact]
    public async Task TC02_partial_overlap_is_rejected()
    {
        var (_, alice, bob) = await SetupUsersAsync();
        await BookOkAsync(alice, 2, At(11), At(12));
        Assert.Equal(HttpStatusCode.Conflict, (await BookAsync(bob, 2, At(11, 45), At(12, 30))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await BookAsync(bob, 2, At(10, 30), At(11, 15))).StatusCode);
    }

    [Fact]
    public async Task TC03_back_to_back_bookings_are_allowed()
    {
        var (_, alice, bob) = await SetupUsersAsync();
        await BookOkAsync(alice, 2, At(11), At(12));
        await BookOkAsync(bob, 2, At(12), At(13));
        await BookOkAsync(bob, 2, At(10, 30), At(11));
    }

    [Fact]
    public async Task TC04_same_time_in_a_different_cabin_is_allowed()
    {
        var (_, alice, bob) = await SetupUsersAsync();
        await BookOkAsync(alice, 1, At(11), At(12));
        await BookOkAsync(bob, 2, At(11), At(12));
    }

    [Fact]
    public async Task TC05_ten_simultaneous_requests_for_one_slot_give_exactly_one_booking()
    {
        var users = new List<Session>();
        for (var i = 0; i < 5; i++) users.Add(await App.RegisterAsync($"Racer {i}"));

        var results = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(i => BookAsync(users[i % 5], 3, At(15), At(16), $"Race {i}")));

        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(9, results.Count(r => r.StatusCode == HttpStatusCode.Conflict));
    }
}

// ============================================================== 06 Quick book ("Book now")
public class F06_QuickBook(TestApp app) : ApiTest(app)
{
    [Fact]
    public async Task TC01_starts_now_and_is_in_progress()
    {
        var alice = await App.RegisterAsync("Alice");
        var b = await ReadAsync<BookingDto>(await PostAsync(alice, "/api/bookings/quick", new QuickBookRequest(1, 30, "Huddle")));
        Assert.Equal(At(10), b.StartTime);
        Assert.Equal(At(10, 30), b.EndTime);
        Assert.Equal(BookingPhase.InProgress, b.Phase);
    }

    [Fact]
    public async Task TC02_default_title_is_quick_meeting()
    {
        var alice = await App.RegisterAsync("Alice");
        var b = await ReadAsync<BookingDto>(await PostAsync(alice, "/api/bookings/quick", new QuickBookRequest(1, 15, null)));
        Assert.Equal("Quick meeting", b.Title);
    }

    [Fact]
    public async Task TC03_occupied_cabin_cannot_be_quick_booked()
    {
        var (_, alice, bob) = await SetupUsersAsync();
        await PostAsync(alice, "/api/bookings/quick", new QuickBookRequest(1, 60, null));
        var res = await PostAsync(bob, "/api/bookings/quick", new QuickBookRequest(1, 15, null));
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task TC04_cannot_run_past_office_closing()
    {
        var alice = await App.RegisterAsync("Alice");
        App.Clock.Now = At(19, 30);
        var res = await PostAsync(alice, "/api/bookings/quick", new QuickBookRequest(1, 60, null));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task TC05_is_blocked_by_an_upcoming_booking_that_would_overlap()
    {
        var (_, alice, bob) = await SetupUsersAsync();
        await BookOkAsync(alice, 1, At(10, 30), At(11));
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(bob, "/api/bookings/quick", new QuickBookRequest(1, 60, null))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await PostAsync(bob, "/api/bookings/quick", new QuickBookRequest(1, 30, null))).StatusCode);
    }
}

// ============================================================== 07 Edit / reschedule
public class F07_EditBooking(TestApp app) : ApiTest(app)
{
    private static UpdateBookingRequest Update(int cabin, DateTimeOffset s, DateTimeOffset e, string title = "Edited") =>
        new(cabin, title, BookingPurpose.Discussion, "notes", s, e);

    [Fact]
    public async Task TC01_owner_can_reschedule()
    {
        var alice = await App.RegisterAsync("Alice");
        var b = await BookOkAsync(alice, 1, At(11), At(12));
        var res = await alice.Http.PutAsJsonAsync($"/api/bookings/{b.Id}", Update(1, At(13), At(14)), TestApp.Json);
        var updated = await ReadAsync<BookingDto>(res);
        Assert.Equal(At(13), updated.StartTime);
        Assert.Equal("Edited", updated.Title);
    }

    [Fact]
    public async Task TC02_owner_can_move_to_another_cabin()
    {
        var alice = await App.RegisterAsync("Alice");
        var b = await BookOkAsync(alice, 1, At(11), At(12));
        var updated = await ReadAsync<BookingDto>(await alice.Http.PutAsJsonAsync($"/api/bookings/{b.Id}", Update(3, At(11), At(12)), TestApp.Json));
        Assert.Equal(3, updated.CabinId);
    }

    [Fact]
    public async Task TC03_moving_into_someone_elses_slot_is_rejected()
    {
        var (_, alice, bob) = await SetupUsersAsync();
        var b = await BookOkAsync(alice, 1, At(11), At(12));
        await BookOkAsync(bob, 1, At(14), At(15));
        var res = await alice.Http.PutAsJsonAsync($"/api/bookings/{b.Id}", Update(1, At(14, 30), At(15, 30)), TestApp.Json);
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task TC04_other_employee_cannot_edit()
    {
        var (_, alice, bob) = await SetupUsersAsync();
        var b = await BookOkAsync(alice, 1, At(11), At(12));
        var res = await bob.Http.PutAsJsonAsync($"/api/bookings/{b.Id}", Update(1, At(13), At(14)), TestApp.Json);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task TC05_running_meeting_cannot_be_edited()
    {
        var alice = await App.RegisterAsync("Alice");
        var b = await ReadAsync<BookingDto>(await PostAsync(alice, "/api/bookings/quick", new QuickBookRequest(1, 30, null)));
        var res = await alice.Http.PutAsJsonAsync($"/api/bookings/{b.Id}", Update(1, At(13), At(14)), TestApp.Json);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}

// ============================================================== 08 Extend
public class F08_Extend(TestApp app) : ApiTest(app)
{
    [Fact]
    public async Task TC01_extends_by_requested_minutes()
    {
        var alice = await App.RegisterAsync("Alice");
        var b = await BookOkAsync(alice, 1, At(11), At(12));
        var e = await ReadAsync<BookingDto>(await PostAsync(alice, $"/api/bookings/{b.Id}/extend", new ExtendBookingRequest(15)));
        Assert.Equal(At(12, 15), e.EndTime);
        Assert.Equal(75, e.DurationMinutes);
    }

    [Fact]
    public async Task TC02_blocked_by_the_next_booking()
    {
        var (_, alice, bob) = await SetupUsersAsync();
        var b = await BookOkAsync(alice, 1, At(11), At(12));
        await BookOkAsync(bob, 1, At(12), At(13));
        var res = await PostAsync(alice, $"/api/bookings/{b.Id}/extend", new ExtendBookingRequest(15));
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Contains("Bob", await DetailAsync(res));
    }

    [Fact]
    public async Task TC03_cannot_extend_past_office_closing()
    {
        var alice = await App.RegisterAsync("Alice");
        var b = await BookOkAsync(alice, 1, At(19), At(20));
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(alice, $"/api/bookings/{b.Id}/extend", new ExtendBookingRequest(15))).StatusCode);
    }

    [Fact]
    public async Task TC04_cannot_exceed_maximum_duration()
    {
        var alice = await App.RegisterAsync("Alice");
        var b = await BookOkAsync(alice, 1, At(11), At(15));
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(alice, $"/api/bookings/{b.Id}/extend", new ExtendBookingRequest(15))).StatusCode);
    }

    [Fact]
    public async Task TC05_other_employee_cannot_extend()
    {
        var (_, alice, bob) = await SetupUsersAsync();
        var b = await BookOkAsync(alice, 1, At(11), At(12));
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(bob, $"/api/bookings/{b.Id}/extend", new ExtendBookingRequest(15))).StatusCode);
    }
}

// ============================================================== 09 End early
public class F09_EndEarly(TestApp app) : ApiTest(app)
{
    [Fact]
    public async Task TC01_ends_a_running_meeting_and_frees_the_cabin()
    {
        var alice = await App.RegisterAsync("Alice");
        var b = await ReadAsync<BookingDto>(await PostAsync(alice, "/api/bookings/quick", new QuickBookRequest(1, 60, null)));
        App.Clock.Now = At(10, 20);

        var ended = await ReadAsync<BookingDto>(await PostAsync(alice, $"/api/bookings/{b.Id}/end"));
        Assert.Equal(At(10, 20), ended.EndTime);
        Assert.Equal(BookingPhase.Completed, ended.Phase);

        // Same instant: cabin already free and the meeting gone from "My bookings" (no waiting for the minute to pass).
        var c1 = (await alice.Http.GetFromJsonAsync<List<CabinStatusDto>>("/api/cabins/status", TestApp.Json))!.Single(c => c.Cabin.Id == 1);
        Assert.False(c1.IsOccupied);
        Assert.Empty((await alice.Http.GetFromJsonAsync<List<BookingDto>>("/api/bookings/mine", TestApp.Json))!);
    }

    [Fact]
    public async Task TC02_freed_time_can_be_booked_by_someone_else()
    {
        var (_, alice, bob) = await SetupUsersAsync();
        var b = await ReadAsync<BookingDto>(await PostAsync(alice, "/api/bookings/quick", new QuickBookRequest(1, 60, null)));
        App.Clock.Now = At(10, 20);
        await PostAsync(alice, $"/api/bookings/{b.Id}/end");
        await BookOkAsync(bob, 1, At(10, 30), At(11));
    }

    [Fact]
    public async Task TC03_upcoming_meeting_cannot_be_ended()
    {
        var alice = await App.RegisterAsync("Alice");
        var b = await BookOkAsync(alice, 1, At(11), At(12));
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(alice, $"/api/bookings/{b.Id}/end")).StatusCode);
    }

    [Fact]
    public async Task TC04_other_employee_cannot_end()
    {
        var (_, alice, bob) = await SetupUsersAsync();
        var b = await ReadAsync<BookingDto>(await PostAsync(alice, "/api/bookings/quick", new QuickBookRequest(1, 60, null)));
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(bob, $"/api/bookings/{b.Id}/end")).StatusCode);
    }

    [Fact]
    public async Task TC05_admin_can_end_anyones_meeting()
    {
        var (admin, alice, _) = await SetupUsersAsync();
        var b = await ReadAsync<BookingDto>(await PostAsync(alice, "/api/bookings/quick", new QuickBookRequest(1, 60, null)));
        App.Clock.Now = At(10, 5);
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(admin, $"/api/bookings/{b.Id}/end")).StatusCode);
    }
}

// ============================================================== 10 Check-in
public class F10_CheckIn(TestApp app) : ApiTest(app)
{
    [Fact]
    public async Task TC01_check_in_within_window_succeeds()
    {
        var alice = await App.RegisterAsync("Alice");
        var b = await BookOkAsync(alice, 1, At(11), At(12));
        App.Clock.Now = At(10, 56);
        var c = await ReadAsync<BookingDto>(await PostAsync(alice, $"/api/bookings/{b.Id}/check-in"));
        Assert.Equal(At(10, 56), c.CheckedInAt);
    }

    [Fact]
    public async Task TC02_too_early_is_rejected()
    {
        var alice = await App.RegisterAsync("Alice");
        var b = await BookOkAsync(alice, 1, At(11), At(12));
        var res = await PostAsync(alice, $"/api/bookings/{b.Id}/check-in");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("5 minutes", await DetailAsync(res));
    }

    [Fact]
    public async Task TC03_after_the_meeting_ended_is_rejected()
    {
        var alice = await App.RegisterAsync("Alice");
        var b = await BookOkAsync(alice, 1, At(10, 15), At(10, 45));
        App.Clock.Now = At(10, 50);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(alice, $"/api/bookings/{b.Id}/check-in")).StatusCode);
    }

    [Fact]
    public async Task TC04_other_employee_cannot_check_in()
    {
        var (_, alice, bob) = await SetupUsersAsync();
        var b = await BookOkAsync(alice, 1, At(10, 15), At(11));
        App.Clock.Now = At(10, 15);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(bob, $"/api/bookings/{b.Id}/check-in")).StatusCode);
    }

    [Fact]
    public async Task TC05_second_check_in_keeps_the_first_time()
    {
        var alice = await App.RegisterAsync("Alice");
        var b = await BookOkAsync(alice, 1, At(11), At(12));
        App.Clock.Now = At(10, 58);
        await PostAsync(alice, $"/api/bookings/{b.Id}/check-in");
        App.Clock.Now = At(11, 10);
        var again = await ReadAsync<BookingDto>(await PostAsync(alice, $"/api/bookings/{b.Id}/check-in"));
        Assert.Equal(At(10, 58), again.CheckedInAt);
    }
}

// ============================================================== 11 Cancel
public class F11_Cancel(TestApp app) : ApiTest(app)
{
    [Fact]
    public async Task TC01_owner_cancels_and_booking_disappears_from_lists()
    {
        var alice = await App.RegisterAsync("Alice");
        var b = await BookOkAsync(alice, 1, At(11), At(12));
        Assert.Equal(HttpStatusCode.NoContent, (await alice.Http.DeleteAsync($"/api/bookings/{b.Id}")).StatusCode);

        Assert.Empty((await alice.Http.GetFromJsonAsync<List<BookingDto>>("/api/bookings/mine", TestApp.Json))!);
        var day = await alice.Http.GetFromJsonAsync<List<BookingDto>>($"/api/bookings?from={Uri.EscapeDataString(At(0).ToString("O"))}&to={Uri.EscapeDataString(At(0, 0, 1).ToString("O"))}", TestApp.Json);
        Assert.Empty(day!);
    }

    [Fact]
    public async Task TC02_cancelled_slot_can_be_rebooked()
    {
        var (_, alice, bob) = await SetupUsersAsync();
        var b = await BookOkAsync(alice, 1, At(11), At(12));
        await alice.Http.DeleteAsync($"/api/bookings/{b.Id}");
        var again = await BookOkAsync(bob, 1, At(11), At(12));
        Assert.Equal("Bob", again.BookedByName);
    }

    [Fact]
    public async Task TC03_other_employee_cannot_cancel_but_admin_can()
    {
        var (admin, alice, bob) = await SetupUsersAsync();
        var b = await BookOkAsync(alice, 1, At(11), At(12));
        Assert.Equal(HttpStatusCode.Forbidden, (await bob.Http.DeleteAsync($"/api/bookings/{b.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.Http.DeleteAsync($"/api/bookings/{b.Id}")).StatusCode);
    }

    [Fact]
    public async Task TC04_running_meeting_must_be_ended_not_cancelled()
    {
        var alice = await App.RegisterAsync("Alice");
        var b = await ReadAsync<BookingDto>(await PostAsync(alice, "/api/bookings/quick", new QuickBookRequest(1, 30, null)));
        var res = await alice.Http.DeleteAsync($"/api/bookings/{b.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains("End now", await DetailAsync(res));
    }

    [Fact]
    public async Task TC05_cancelling_twice_is_rejected()
    {
        var alice = await App.RegisterAsync("Alice");
        var b = await BookOkAsync(alice, 1, At(11), At(12));
        await alice.Http.DeleteAsync($"/api/bookings/{b.Id}");
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.Http.DeleteAsync($"/api/bookings/{b.Id}")).StatusCode);
    }
}
