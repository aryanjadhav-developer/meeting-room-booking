using CabinBooking.Application.Common;
using CabinBooking.Application.Dtos;
using CabinBooking.Application.Options;
using CabinBooking.Domain.Enums;
using static CabinBooking.UnitTests.BookingFixture;

namespace CabinBooking.UnitTests;

public class BookingServiceTests
{
    private readonly BookingFixture _f = new();
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task Create_stores_booking_and_broadcasts()
    {
        var b = await _f.Service.CreateAsync(Request(1, At(11), At(12)), Ct);

        Assert.Equal("Alice", b.BookedByName);
        Assert.Equal(60, b.DurationMinutes);
        Assert.Equal(BookingPhase.Upcoming, b.Phase);
        Assert.Equal(["created"], _f.Notifier.Events);
    }

    [Theory]
    [InlineData(11, 30, 12, 30)] // overlaps the end
    [InlineData(10, 30, 11, 15)] // overlaps the start
    [InlineData(11, 15, 11, 45)] // inside
    [InlineData(10, 30, 12, 30)] // surrounds
    public async Task Overlapping_booking_in_same_cabin_is_rejected(int sh, int sm, int eh, int em)
    {
        await _f.Service.CreateAsync(Request(1, At(11), At(12)), Ct);
        _f.ActAs(_f.Bob);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => _f.Service.CreateAsync(Request(1, At(sh, sm), At(eh, em)), Ct));
        Assert.Contains("Alice", ex.Message);
    }

    [Fact]
    public async Task Back_to_back_bookings_are_allowed()
    {
        await _f.Service.CreateAsync(Request(1, At(11), At(12)), Ct);
        await _f.Service.CreateAsync(Request(1, At(12), At(13)), Ct);
        await _f.Service.CreateAsync(Request(1, At(10, 30), At(11)), Ct);
    }

    [Fact]
    public async Task Same_time_in_another_cabin_is_allowed()
    {
        await _f.Service.CreateAsync(Request(1, At(11), At(12)), Ct);
        await _f.Service.CreateAsync(Request(2, At(11), At(12)), Ct);
    }

    [Fact]
    public async Task Inactive_cabin_cannot_be_booked() =>
        await Assert.ThrowsAsync<AppValidationException>(() => _f.Service.CreateAsync(Request(3, At(11), At(12)), Ct));

    [Fact]
    public async Task Cancelled_booking_frees_the_slot()
    {
        var b = await _f.Service.CreateAsync(Request(1, At(11), At(12)), Ct);
        await _f.Service.CancelAsync(b.Id, Ct);

        _f.ActAs(_f.Bob);
        var again = await _f.Service.CreateAsync(Request(1, At(11), At(12)), Ct);
        Assert.Equal("Bob", again.BookedByName);
    }

    [Fact]
    public async Task Only_owner_or_admin_can_cancel()
    {
        var b = await _f.Service.CreateAsync(Request(1, At(11), At(12)), Ct);

        _f.ActAs(_f.Bob);
        await Assert.ThrowsAsync<ForbiddenException>(() => _f.Service.CancelAsync(b.Id, Ct));

        _f.ActAs(_f.Bob, admin: true);
        await _f.Service.CancelAsync(b.Id, Ct);
    }

    [Fact]
    public async Task Extend_is_blocked_by_next_booking()
    {
        var mine = await _f.Service.CreateAsync(Request(1, At(11), At(12)), Ct);
        _f.ActAs(_f.Bob);
        await _f.Service.CreateAsync(Request(1, At(12, 15), At(13)), Ct);

        _f.ActAs(_f.Alice);
        var extended = await _f.Service.ExtendAsync(mine.Id, 15, Ct);
        Assert.Equal(At(12, 15), extended.EndTime);
        await Assert.ThrowsAsync<ConflictException>(() => _f.Service.ExtendAsync(mine.Id, 15, Ct));
    }

    [Fact]
    public async Task Quick_book_starts_now_and_end_early_frees_cabin()
    {
        var b = await _f.Service.QuickBookAsync(new QuickBookRequest(1, 60, null), Ct);
        Assert.Equal(At(10), b.StartTime);
        Assert.Equal(BookingPhase.InProgress, b.Phase);

        _f.Clock.Now = At(10, 20);
        var ended = await _f.Service.EndEarlyAsync(b.Id, Ct);
        Assert.Equal(At(10, 20), ended.EndTime);
        Assert.Equal(BookingPhase.Completed, ended.Phase);

        _f.ActAs(_f.Bob);
        await _f.Service.CreateAsync(Request(1, At(10, 30), At(11)), Ct);
    }

    [Fact]
    public async Task Running_meeting_cannot_be_cancelled_or_edited()
    {
        var b = await _f.Service.QuickBookAsync(new QuickBookRequest(1, 30, null), Ct);
        await Assert.ThrowsAsync<AppValidationException>(() => _f.Service.CancelAsync(b.Id, Ct));
        await Assert.ThrowsAsync<AppValidationException>(() =>
            _f.Service.UpdateAsync(b.Id, new UpdateBookingRequest(1, "x", BookingPurpose.Call, null, At(11), At(12)), Ct));
    }

    [Fact]
    public async Task Update_can_move_booking_without_clashing_with_itself()
    {
        var b = await _f.Service.CreateAsync(Request(1, At(11), At(12)), Ct);
        var moved = await _f.Service.UpdateAsync(b.Id, new UpdateBookingRequest(1, "Moved", BookingPurpose.Discussion, null, At(11, 30), At(12, 30)), Ct);

        Assert.Equal(At(11, 30), moved.StartTime);
        Assert.Equal("Moved", moved.Title);
    }

    [Fact]
    public async Task Availability_returns_gaps_between_bookings()
    {
        await _f.Service.CreateAsync(Request(1, At(11), At(12)), Ct);
        await _f.Service.CreateAsync(Request(1, At(14), At(15)), Ct);

        var a = await _f.Service.GetAvailabilityAsync(1, new DateOnly(2026, 10, 14), Ct);

        Assert.Equal(
            [(At(10), At(11)), (At(12), At(14)), (At(15), At(20))],
            a.FreeSlots.Select(s => (s.Start, s.End)).ToList());
    }

    [Fact]
    public async Task No_shows_are_released_when_enabled()
    {
        var f = new BookingFixture(new BookingRulesOptions { AutoReleaseEnabled = true, CheckInGraceMinutes = 10 });
        var b = await f.Service.CreateAsync(Request(1, At(10, 30), At(11, 30)), Ct);

        f.Clock.Now = At(10, 35);
        Assert.Equal(0, await f.Service.ReleaseNoShowsAsync(Ct));

        f.Clock.Now = At(10, 41);
        Assert.Equal(1, await f.Service.ReleaseNoShowsAsync(Ct));
        Assert.Equal(BookingStatus.NoShow, (await f.Service.GetAsync(b.Id, Ct)).Status);
    }

    [Fact]
    public async Task Checked_in_booking_is_not_released()
    {
        var f = new BookingFixture(new BookingRulesOptions { AutoReleaseEnabled = true });
        var b = await f.Service.CreateAsync(Request(1, At(10, 30), At(11, 30)), Ct);

        f.Clock.Now = At(10, 31);
        await f.Service.CheckInAsync(b.Id, Ct);
        f.Clock.Now = At(11);
        Assert.Equal(0, await f.Service.ReleaseNoShowsAsync(Ct));
    }
}
