namespace CabinBooking.Application.Options;

public class BookingRulesOptions
{
    public const string SectionName = "BookingRules";

    /// <summary>IANA time zone id (also used by the browser) of the office, e.g. "Asia/Kolkata".</summary>
    public string TimeZoneId { get; set; } = "Asia/Kolkata";
    public TimeOnly OfficeStart { get; set; } = new(9, 0);
    public TimeOnly OfficeEnd { get; set; } = new(20, 0);
    /// <summary>
    /// Empty here on purpose: the configuration binder appends to (rather than replaces) a pre-filled array.
    /// When nothing is configured, <see cref="DefaultWorkingDays"/> applies.
    /// </summary>
    public DayOfWeek[] WorkingDays { get; set; } = [];

    public static readonly DayOfWeek[] DefaultWorkingDays =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday];

    /// <summary>Projects offered in the booking form (an "Other" option is always added). Empty = <see cref="DefaultProjects"/>.</summary>
    public string[] Projects { get; set; } = [];

    public static readonly string[] DefaultProjects = ["Project Alpha", "Project Beta", "Project Gamma"];

    public int SlotMinutes { get; set; } = 15;
    public int MinDurationMinutes { get; set; } = 15;
    public int MaxDurationMinutes { get; set; } = 240;
    public int MaxDaysAhead { get; set; } = 30;

    /// <summary>0 = unlimited.</summary>
    public int MaxBookingsPerUserPerDay { get; set; }

    /// <summary>How early before the start a user may check in.</summary>
    public int CheckInOpensMinutesBefore { get; set; } = 5;

    /// <summary>When enabled, bookings not checked in within <see cref="CheckInGraceMinutes"/> are released as no-shows.</summary>
    public bool AutoReleaseEnabled { get; set; }
    public int CheckInGraceMinutes { get; set; } = 10;
}
