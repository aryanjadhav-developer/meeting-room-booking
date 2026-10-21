using CabinBooking.Domain.Enums;

namespace CabinBooking.Domain.Entities;

public class Booking
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int CabinId { get; set; }
    public Cabin Cabin { get; set; } = null!;

    public Guid BookedByUserId { get; set; }
    public User BookedBy { get; set; } = null!;

    /// <summary>Project name (one of the configured projects, or a custom name when "Other" was chosen).</summary>
    public string Title { get; set; } = string.Empty;
    public BookingPurpose Purpose { get; set; }
    /// <summary>Free-text description, required when <see cref="Purpose"/> is Other.</summary>
    public string? PurposeDetail { get; set; }
    public string? Notes { get; set; }

    /// <summary>Always stored in UTC.</summary>
    public DateTimeOffset StartTime { get; set; }
    /// <summary>Always stored in UTC. Exclusive.</summary>
    public DateTimeOffset EndTime { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;
    public DateTimeOffset? CheckedInAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public DateTimeOffset? ReminderSentAt { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public TimeSpan Duration => EndTime - StartTime;

    /// <summary>Half-open interval overlap: [StartTime, EndTime) vs [start, end).</summary>
    public bool Overlaps(DateTimeOffset start, DateTimeOffset end) => StartTime < end && EndTime > start;

    public BookingPhase GetPhase(DateTimeOffset now) => Status switch
    {
        BookingStatus.Cancelled => BookingPhase.Cancelled,
        BookingStatus.NoShow => BookingPhase.NoShow,
        _ when now < StartTime => BookingPhase.Upcoming,
        _ when now < EndTime => BookingPhase.InProgress,
        _ => BookingPhase.Completed
    };
}
