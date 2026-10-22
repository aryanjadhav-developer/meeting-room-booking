using System.Globalization;
using CabinBooking.Application.Common;
using CabinBooking.Application.Dtos;
using CabinBooking.Application.Options;
using Microsoft.Extensions.Options;

namespace CabinBooking.Application.Services;

/// <summary>Pure, clock-driven booking policy checks. Everything time related goes through the office time zone.</summary>
public class BookingRuleValidator
{
    /// <summary>Tolerance so a booking made "now" from the UI is not rejected as being in the past.</summary>
    private static readonly TimeSpan PastTolerance = TimeSpan.FromMinutes(2);

    private readonly BookingRulesOptions _rules;
    private readonly BookingRulesOptions _initialDefaults;
    private readonly TimeProvider _clock;

    public BookingRuleValidator(IOptions<BookingRulesOptions> rules, TimeProvider clock)
    {
        var src = rules.Value;
        var workingDays = src.WorkingDays.Length > 0
            ? src.WorkingDays.Distinct().ToArray()
            : BookingRulesOptions.DefaultWorkingDays;
        var projects = src.Projects.Length > 0
            ? src.Projects.Select(p => p.Trim()).Where(p => p.Length > 0).Distinct().ToArray()
            : BookingRulesOptions.DefaultProjects;
        var purposes = src.Purposes.Length > 0
            ? src.Purposes.Select(p => p.Trim()).Where(p => p.Length > 0).Distinct().ToArray()
            : BookingRulesOptions.DefaultPurposes;

        _rules = new BookingRulesOptions
        {
            TimeZoneId = src.TimeZoneId,
            OfficeStart = src.OfficeStart,
            OfficeEnd = src.OfficeEnd,
            WorkingDays = workingDays.ToArray(),
            SlotMinutes = src.SlotMinutes,
            MinDurationMinutes = src.MinDurationMinutes,
            MaxDurationMinutes = src.MaxDurationMinutes,
            MaxDaysAhead = src.MaxDaysAhead,
            CheckInOpensMinutesBefore = src.CheckInOpensMinutesBefore,
            AutoReleaseEnabled = src.AutoReleaseEnabled,
            CheckInGraceMinutes = src.CheckInGraceMinutes,
            Projects = projects.ToArray(),
            Purposes = purposes.ToArray()
        };

        _initialDefaults = new BookingRulesOptions
        {
            TimeZoneId = _rules.TimeZoneId,
            OfficeStart = _rules.OfficeStart,
            OfficeEnd = _rules.OfficeEnd,
            WorkingDays = workingDays.ToArray(),
            SlotMinutes = _rules.SlotMinutes,
            MinDurationMinutes = _rules.MinDurationMinutes,
            MaxDurationMinutes = _rules.MaxDurationMinutes,
            MaxDaysAhead = _rules.MaxDaysAhead,
            CheckInOpensMinutesBefore = _rules.CheckInOpensMinutesBefore,
            AutoReleaseEnabled = _rules.AutoReleaseEnabled,
            CheckInGraceMinutes = _rules.CheckInGraceMinutes,
            Projects = projects.ToArray(),
            Purposes = purposes.ToArray()
        };

        _clock = clock;
        Zone = TimeZoneInfo.FindSystemTimeZoneById(_rules.TimeZoneId);
    }

    public void ResetToDefaults()
    {
        _rules.OfficeStart = _initialDefaults.OfficeStart;
        _rules.OfficeEnd = _initialDefaults.OfficeEnd;
        _rules.WorkingDays = _initialDefaults.WorkingDays.ToArray();
        _rules.SlotMinutes = _initialDefaults.SlotMinutes;
        _rules.MinDurationMinutes = _initialDefaults.MinDurationMinutes;
        _rules.MaxDurationMinutes = _initialDefaults.MaxDurationMinutes;
        _rules.MaxDaysAhead = _initialDefaults.MaxDaysAhead;
        _rules.CheckInOpensMinutesBefore = _initialDefaults.CheckInOpensMinutesBefore;
        _rules.AutoReleaseEnabled = _initialDefaults.AutoReleaseEnabled;
        _rules.CheckInGraceMinutes = _initialDefaults.CheckInGraceMinutes;
        _rules.Projects = _initialDefaults.Projects.ToArray();
        _rules.Purposes = _initialDefaults.Purposes.ToArray();
    }

    public TimeZoneInfo Zone { get; }
    public BookingRulesOptions Rules => _rules;
    public DateTimeOffset Now => _clock.GetUtcNow();

    public DateTimeOffset ToLocal(DateTimeOffset value) => TimeZoneInfo.ConvertTime(value, Zone);

    public DateOnly Today => DateOnly.FromDateTime(ToLocal(Now).DateTime);

    /// <summary>UTC instants of the office opening and closing on a local calendar date.</summary>
    public (DateTimeOffset Start, DateTimeOffset End) OfficeWindowUtc(DateOnly localDate) =>
        (LocalToUtc(localDate.ToDateTime(_rules.OfficeStart)), LocalToUtc(localDate.ToDateTime(_rules.OfficeEnd)));

    /// <summary>UTC instants covering a whole local calendar date (midnight to midnight).</summary>
    public (DateTimeOffset Start, DateTimeOffset End) DayWindowUtc(DateOnly localDate) =>
        (LocalToUtc(localDate.ToDateTime(TimeOnly.MinValue)), LocalToUtc(localDate.AddDays(1).ToDateTime(TimeOnly.MinValue)));

    public bool IsWorkingDay(DateOnly localDate) => _rules.WorkingDays.Contains(localDate.DayOfWeek);

    public string Format(DateTimeOffset value) => ToLocal(value).ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Full validation for a brand-new (or rescheduled) time window.</summary>
    public void ValidateNewWindow(DateTimeOffset start, DateTimeOffset end)
    {
        ValidateShape(start, end);

        var now = Now;
        if (start < now - PastTolerance)
            throw new AppValidationException("Bookings cannot start in the past.");

        if (ToLocalDate(start) > Today.AddDays(_rules.MaxDaysAhead))
            throw new AppValidationException($"Bookings can be made at most {_rules.MaxDaysAhead} days in advance.");
    }

    /// <summary>Duration, working day and office-hours checks (no "past" check, used for extensions).</summary>
    public void ValidateShape(DateTimeOffset start, DateTimeOffset end)
    {
        if (end <= start)
            throw new AppValidationException("End time must be after start time.");

        var minutes = (end - start).TotalMinutes;
        if (minutes < _rules.MinDurationMinutes)
            throw new AppValidationException($"A booking must be at least {_rules.MinDurationMinutes} minutes long.");
        if (minutes > _rules.MaxDurationMinutes)
            throw new AppValidationException($"A booking can be at most {_rules.MaxDurationMinutes / 60.0:0.##} hours long.");

        var date = ToLocalDate(start);

        if (!IsWorkingDay(date))
            throw new AppValidationException($"Cabins cannot be booked on {date.DayOfWeek}.");

        // The office window lies within a single local day, so this also rules out bookings spanning midnight.
        var (officeStart, officeEnd) = OfficeWindowUtc(date);
        if (start < officeStart || end > officeEnd)
            throw new AppValidationException(
                $"Bookings must be within office hours ({_rules.OfficeStart:HH\\:mm}–{_rules.OfficeEnd:HH\\:mm}).");
    }

    public BookingRulesDto ToDto() => new(
        _rules.TimeZoneId, _rules.OfficeStart, _rules.OfficeEnd, _rules.WorkingDays,
        _rules.SlotMinutes, _rules.MinDurationMinutes, _rules.MaxDurationMinutes, _rules.MaxDaysAhead,
        _rules.CheckInOpensMinutesBefore, _rules.AutoReleaseEnabled, _rules.CheckInGraceMinutes, _rules.Projects, _rules.Purposes);

    public void ApplyUpdate(UpdateSettingsRequest request)
    {
        if (request.OfficeEnd <= request.OfficeStart)
            throw new AppValidationException("Office end time must be after office start time.");
        if (request.SlotMinutes < 5 || request.SlotMinutes > 120)
            throw new AppValidationException("Slot minutes must be between 5 and 120.");
        if (request.MinDurationMinutes < request.SlotMinutes)
            throw new AppValidationException("Min duration cannot be less than slot minutes.");
        if (request.MaxDurationMinutes < request.MinDurationMinutes)
            throw new AppValidationException("Max duration must be at least min duration.");

        _rules.OfficeStart = request.OfficeStart;
        _rules.OfficeEnd = request.OfficeEnd;
        _rules.WorkingDays = request.WorkingDays.Length > 0 ? request.WorkingDays.Distinct().ToArray() : BookingRulesOptions.DefaultWorkingDays;
        _rules.SlotMinutes = request.SlotMinutes;
        _rules.MinDurationMinutes = request.MinDurationMinutes;
        _rules.MaxDurationMinutes = request.MaxDurationMinutes;
        _rules.MaxDaysAhead = request.MaxDaysAhead;
        _rules.CheckInOpensMinutesBefore = request.CheckInOpensMinutesBefore;
        _rules.AutoReleaseEnabled = request.AutoReleaseEnabled;
        _rules.CheckInGraceMinutes = request.CheckInGraceMinutes;
        _rules.Projects = request.Projects.Select(p => p.Trim()).Where(p => p.Length > 0).Distinct().ToArray();
        _rules.Purposes = request.Purposes.Length > 0
            ? request.Purposes.Select(p => p.Trim()).Where(p => p.Length > 0).Distinct().ToArray()
            : BookingRulesOptions.DefaultPurposes;
    }

    private DateOnly ToLocalDate(DateTimeOffset value) => DateOnly.FromDateTime(ToLocal(value).DateTime);

    private DateTimeOffset LocalToUtc(DateTime local)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, Zone.GetUtcOffset(unspecified)).ToUniversalTime();
    }
}
