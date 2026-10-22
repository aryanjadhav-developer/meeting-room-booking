namespace CabinBooking.Application.Dtos;

public record RecurringBookingDto(
    Guid Id,
    Guid UserId,
    string UserName,
    int CabinId,
    string CabinName,
    string Title,
    string Purpose,
    string? PurposeDetail,
    string? Notes,
    DateOnly StartDate,
    DateOnly EndDate,
    int StartMinuteOfDay,
    int DurationMinutes,
    List<int> DaysOfWeek,
    List<string> AttendeeEmails,
    bool IsActive,
    DateTimeOffset CreatedAt);

public record CreateRecurringBookingRequest(
    int CabinId,
    string Title,
    string Purpose,
    string? PurposeDetail,
    string? Notes,
    DateOnly StartDate,
    DateOnly EndDate,
    int StartMinuteOfDay,
    int DurationMinutes,
    List<int> DaysOfWeek,
    List<string>? AttendeeEmails = null);

public record UpdateRecurringBookingRequest(
    DateOnly? EndDate,
    bool? IsActive,
    List<int>? DaysOfWeek,
    List<string>? AttendeeEmails,
    string? Notes,
    int? StartMinuteOfDay,
    int? DurationMinutes,
    int? CabinId = null,
    string? Title = null);
