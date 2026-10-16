namespace CabinBooking.Domain.Enums;

/// <summary>Persisted lifecycle state. Time-based phase (upcoming / in progress / completed) is derived, see <see cref="BookingPhase"/>.</summary>
public enum BookingStatus
{
    Confirmed = 0,
    Cancelled = 1,
    NoShow = 2
}

/// <summary>What a booking looks like right now, computed from status + clock.</summary>
public enum BookingPhase
{
    Upcoming = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3,
    NoShow = 4
}
