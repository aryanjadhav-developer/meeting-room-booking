using CabinBooking.Domain.Enums;

namespace CabinBooking.Application.Dtos;

// ---------- Users / Auth ----------
public record UserDto(Guid Id, string Name, string Email, UserRole Role, bool IsActive);

public record RegisterRequest(string Name, string Email, string Password);

public record LoginRequest(string Email, string Password);

public record AuthResponse(string Token, DateTimeOffset ExpiresAt, UserDto User, string? RefreshToken = null);

public record RefreshTokenRequest(string? RefreshToken = null);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string Email, string Token, string NewPassword);

public record UpdateUserRequest(UserRole Role, bool IsActive);

public record CreateUserRequest(string Name, string Email, string Password, UserRole Role = UserRole.Employee);

/// <summary>Minimal employee entry for the attendee picker (visible to every signed-in user).</summary>
public record DirectoryUserDto(Guid Id, string Name, string Email);

public record ProjectDto(int Id, string Name, Guid[] MemberUserIds);

public record UpsertProjectRequest(string Name, Guid[]? MemberUserIds);

// ---------- Cabins ----------
public record CabinDto(int Id, string Name, string? Description, int Capacity, string[] Amenities, string Color, bool IsActive, string? CapacityLabel);

public record UpsertCabinRequest(string Name, string? Description, int Capacity, string[] Amenities, string Color, bool IsActive, string? CapacityLabel = null);

public record CabinStatusDto(CabinDto Cabin, bool IsOccupied, BookingDto? Current, BookingDto? Next);

// ---------- Bookings ----------
public record BookingDto(
    Guid Id,
    int CabinId,
    string CabinName,
    Guid BookedByUserId,
    string BookedByName,
    string BookedByEmail,
    string Title,
    BookingPurpose Purpose,
    string? PurposeDetail,
    string? Notes,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    int DurationMinutes,
    BookingStatus Status,
    BookingPhase Phase,
    DateTimeOffset? CheckedInAt,
    DateTimeOffset CreatedAt);

/// <summary><paramref name="Title"/> is the project name. <paramref name="Purpose"/> is required (nullable only so a missing value can be reported).</summary>
public record CreateBookingRequest(int CabinId, string Title, BookingPurpose? Purpose, string? Notes, DateTimeOffset StartTime, DateTimeOffset EndTime, string? PurposeDetail = null, List<string>? AttendeeEmails = null);

public record UpdateBookingRequest(int CabinId, string Title, BookingPurpose? Purpose, string? Notes, DateTimeOffset StartTime, DateTimeOffset EndTime, string? PurposeDetail = null, List<string>? AttendeeEmails = null);

public record QuickBookRequest(int CabinId, int DurationMinutes, string? Title);

public record ExtendBookingRequest(int Minutes);

public record TimeRangeDto(DateTimeOffset Start, DateTimeOffset End);

public record AvailabilityDto(int CabinId, DateOnly Date, IReadOnlyList<TimeRangeDto> FreeSlots);

public record BookingRulesDto(
    string TimeZoneId,
    TimeOnly OfficeStart,
    TimeOnly OfficeEnd,
    DayOfWeek[] WorkingDays,
    int SlotMinutes,
    int MinDurationMinutes,
    int MaxDurationMinutes,
    int MaxDaysAhead,
    int CheckInOpensMinutesBefore,
    bool AutoReleaseEnabled,
    int CheckInGraceMinutes,
    string[] Projects);

// ---------- Reports ----------
public record CabinUtilizationDto(int CabinId, string CabinName, int BookingCount, double BookedHours, double AvailableHours, double UtilizationPercent, int NoShowCount, int CancelledCount);

public record HourlyLoadDto(int Hour, int BookingCount);

public record TopUserDto(Guid UserId, string Name, string Email, int BookingCount, double BookedHours);

public record UtilizationReportDto(DateOnly From, DateOnly To, IReadOnlyList<CabinUtilizationDto> Cabins, IReadOnlyList<HourlyLoadDto> HourlyLoad, IReadOnlyList<TopUserDto> TopUsers);

// ---------- Audit ----------
public record AuditEntryDto(Guid Id, Guid? UserId, string UserName, string Action, string EntityType, string EntityId, string Details, DateTimeOffset Timestamp);

// ---------- Settings ----------
public record UpdateSettingsRequest(
    TimeOnly OfficeStart,
    TimeOnly OfficeEnd,
    DayOfWeek[] WorkingDays,
    int SlotMinutes,
    int MinDurationMinutes,
    int MaxDurationMinutes,
    int MaxDaysAhead,
    int CheckInOpensMinutesBefore,
    bool AutoReleaseEnabled,
    int CheckInGraceMinutes,
    string[] Projects);
