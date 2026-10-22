namespace CabinBooking.Domain.Entities;

public class RecurringBooking
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public int CabinId { get; set; }
    public Cabin Cabin { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string? PurposeDetail { get; set; }
    public string? Notes { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    /// <summary>Minutes since midnight in the office timezone (e.g. 9*60 = 540 for 09:00).</summary>
    public int StartMinuteOfDay { get; set; }
    public int DurationMinutes { get; set; }

    /// <summary>JSON int[] using System.DayOfWeek values (0=Sunday…6=Saturday).</summary>
    public string DaysOfWeekJson { get; set; } = "[]";

    /// <summary>JSON string[] of attendee email addresses.</summary>
    public string? AttendeeEmailsJson { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }

    public List<RecurringBookingInstance> Instances { get; set; } = [];
}

public class RecurringBookingInstance
{
    public Guid RecurringBookingId { get; set; }
    public RecurringBooking RecurringBooking { get; set; } = null!;
    public DateOnly Date { get; set; }
    /// <summary>Null when the slot was skipped due to a cabin conflict.</summary>
    public Guid? BookingId { get; set; }
}
