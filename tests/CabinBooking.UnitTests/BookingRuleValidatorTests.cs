using CabinBooking.Application.Common;
using static CabinBooking.UnitTests.BookingFixture;

namespace CabinBooking.UnitTests;

public class BookingRuleValidatorTests
{
    private readonly BookingFixture _f = new();

    [Fact]
    public void Accepts_valid_window_within_office_hours() =>
        _f.Rules.ValidateNewWindow(At(11), At(12));

    [Fact]
    public void Accepts_booking_that_ends_exactly_at_office_close() =>
        _f.Rules.ValidateNewWindow(At(19), At(20));

    [Theory]
    [InlineData(8, 30, 9, 30)]   // starts before opening
    [InlineData(19, 30, 20, 30)] // ends after closing
    public void Rejects_outside_office_hours(int sh, int sm, int eh, int em) =>
        Assert.Throws<AppValidationException>(() => _f.Rules.ValidateNewWindow(At(sh, sm), At(eh, em)));

    [Fact]
    public void Rejects_end_before_start() =>
        Assert.Throws<AppValidationException>(() => _f.Rules.ValidateNewWindow(At(12), At(11)));

    [Fact]
    public void Rejects_too_short() =>
        Assert.Throws<AppValidationException>(() => _f.Rules.ValidateNewWindow(At(12), At(12, 10)));

    [Fact]
    public void Rejects_too_long() =>
        Assert.Throws<AppValidationException>(() => _f.Rules.ValidateNewWindow(At(11), At(15, 15)));

    [Fact]
    public void Rejects_past_start() =>
        Assert.Throws<AppValidationException>(() => _f.Rules.ValidateNewWindow(At(9), At(9, 30)));

    [Fact]
    public void Rejects_too_far_ahead() =>
        Assert.Throws<AppValidationException>(() => _f.Rules.ValidateNewWindow(At(11, 0, 40), At(12, 0, 40)));

    [Fact]
    public void Rejects_non_working_day()
    {
        // 18 Oct 2026 is a Sunday.
        var ex = Assert.Throws<AppValidationException>(() => _f.Rules.ValidateNewWindow(At(11, 0, 4), At(12, 0, 4)));
        Assert.Contains("Sunday", ex.Message);
    }

    [Fact]
    public void Office_window_is_converted_from_office_time_zone_to_utc()
    {
        var (open, close) = _f.Rules.OfficeWindowUtc(new DateOnly(2026, 10, 14));
        Assert.Equal(new DateTimeOffset(2026, 10, 14, 3, 30, 0, TimeSpan.Zero), open);
        Assert.Equal(new DateTimeOffset(2026, 10, 14, 14, 30, 0, TimeSpan.Zero), close);
    }
}
