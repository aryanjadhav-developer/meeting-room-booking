using CabinBooking.Application.Abstractions;
using CabinBooking.Application.Common;
using CabinBooking.Application.Dtos;
using CabinBooking.Domain.Entities;
using CabinBooking.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CabinBooking.Application.Services;

public class BookingService(
    IAppDbContext db,
    ICabinLock cabinLock,
    ICurrentUser currentUser,
    IBookingNotifier notifier,
    BookingRuleValidator rules,
    ILogger<BookingService> logger)
{
    private const int MaxTitleLength = 120;
    private const int MaxPurposeDetailLength = 100;
    private const int MaxNotesLength = 1000;

    // ---------------------------------------------------------------- queries

    public async Task<IReadOnlyList<BookingDto>> GetRangeAsync(DateTimeOffset from, DateTimeOffset to, int? cabinId, CancellationToken ct)
    {
        if (to <= from) throw new AppValidationException("'to' must be after 'from'.");
        if (to - from > TimeSpan.FromDays(62)) throw new AppValidationException("Please request at most 62 days at a time.");

        var query = WithDetails()
            .Where(b => b.Status != BookingStatus.Cancelled && b.StartTime < to && b.EndTime > from);
        if (cabinId is not null) query = query.Where(b => b.CabinId == cabinId);

        var list = await query.OrderBy(b => b.StartTime).ToListAsync(ct);
        return list.Select(b => b.ToDto(rules.Now)).ToList();
    }

    public async Task<IReadOnlyList<BookingDto>> GetMineAsync(bool includePast, CancellationToken ct)
    {
        var now = rules.Now;
        var query = WithDetails().Where(b => b.BookedByUserId == currentUser.UserId);
        query = includePast
            ? query.Where(b => b.StartTime > now.AddDays(-90))
            : query.Where(b => b.EndTime > now && b.Status == BookingStatus.Confirmed);

        var list = await query.OrderBy(b => b.StartTime).ToListAsync(ct);
        return list.Select(b => b.ToDto(now)).ToList();
    }

    public async Task<BookingDto> GetAsync(Guid id, CancellationToken ct) => (await LoadAsync(id, ct)).ToDto(rules.Now);

    public async Task<AvailabilityDto> GetAvailabilityAsync(int cabinId, DateOnly date, CancellationToken ct)
    {
        await EnsureCabinBookableAsync(cabinId, ct);

        if (!rules.IsWorkingDay(date))
            return new AvailabilityDto(cabinId, date, []);

        var (open, close) = rules.OfficeWindowUtc(date);
        var cursor = Max(open, RoundUpToSlot(rules.Now));

        var busy = await db.Bookings.AsNoTracking()
            .Where(b => b.CabinId == cabinId && b.Status == BookingStatus.Confirmed && b.StartTime < close && b.EndTime > open)
            .OrderBy(b => b.StartTime)
            .Select(b => new { b.StartTime, b.EndTime })
            .ToListAsync(ct);

        var free = new List<TimeRangeDto>();
        foreach (var b in busy)
        {
            if (b.StartTime > cursor) free.Add(new TimeRangeDto(cursor, b.StartTime));
            cursor = Max(cursor, b.EndTime);
        }
        if (close > cursor) free.Add(new TimeRangeDto(cursor, close));

        var min = TimeSpan.FromMinutes(rules.Rules.MinDurationMinutes);
        return new AvailabilityDto(cabinId, date, free.Where(f => f.End - f.Start >= min).ToList());
    }

    // ---------------------------------------------------------------- commands

    public Task<BookingDto> CreateAsync(CreateBookingRequest request, CancellationToken ct) =>
        CreateCoreAsync(request.CabinId, request.Title, request.Purpose, request.PurposeDetail, request.Notes,
            Truncate(request.StartTime), Truncate(request.EndTime), ct);

    /// <summary>Books a cabin starting right now (rounded down to the minute).</summary>
    public Task<BookingDto> QuickBookAsync(QuickBookRequest request, CancellationToken ct)
    {
        var start = Truncate(rules.Now);
        var title = string.IsNullOrWhiteSpace(request.Title) ? "Quick meeting" : request.Title;
        return CreateCoreAsync(request.CabinId, title, BookingPurpose.Meeting, null, null,
            start, start.AddMinutes(request.DurationMinutes), ct);
    }

    public async Task<BookingDto> UpdateAsync(Guid id, UpdateBookingRequest request, CancellationToken ct)
    {
        var start = Truncate(request.StartTime);
        var end = Truncate(request.EndTime);
        var (purpose, purposeDetail) = ValidateDetails(request.Title, request.Purpose, request.PurposeDetail, request.Notes);
        rules.ValidateNewWindow(start, end);

        var existing = await db.Bookings.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, ct)
                       ?? throw new NotFoundException("Booking not found.");
        EnsureCanModify(existing);
        if (existing.GetPhase(rules.Now) != BookingPhase.Upcoming)
            throw new AppValidationException("Only upcoming bookings can be edited. Use extend or end for a running meeting.");
        await EnsureCabinBookableAsync(request.CabinId, ct);

        await cabinLock.RunExclusiveAsync([existing.CabinId, request.CabinId], async token =>
        {
            var booking = await db.Bookings.FirstAsync(b => b.Id == id, token);
            await EnsureNoOverlapAsync(request.CabinId, start, end, id, token);

            booking.CabinId = request.CabinId;
            booking.Title = request.Title.Trim();
            booking.Purpose = purpose;
            booking.PurposeDetail = purposeDetail;
            booking.Notes = request.Notes?.Trim();
            booking.StartTime = start;
            booking.EndTime = end;
            booking.UpdatedAt = rules.Now;
            await db.SaveChangesAsync(token);
            return true;
        }, ct);

        var dto = await GetAsync(id, ct);
        await notifier.BookingUpdatedAsync(dto);
        return dto;
    }

    public async Task<BookingDto> ExtendAsync(Guid id, int minutes, CancellationToken ct)
    {
        if (minutes <= 0) throw new AppValidationException("Extension must be a positive number of minutes.");

        var existing = await db.Bookings.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, ct)
                       ?? throw new NotFoundException("Booking not found.");
        EnsureCanModify(existing);
        var phase = existing.GetPhase(rules.Now);
        if (phase is not (BookingPhase.Upcoming or BookingPhase.InProgress))
            throw new AppValidationException("Only upcoming or running bookings can be extended.");

        var newEnd = existing.EndTime.AddMinutes(minutes);
        rules.ValidateShape(existing.StartTime, newEnd);

        await cabinLock.RunExclusiveAsync([existing.CabinId], async token =>
        {
            var booking = await db.Bookings.FirstAsync(b => b.Id == id, token);
            await EnsureNoOverlapAsync(booking.CabinId, booking.EndTime, newEnd, id, token);
            booking.EndTime = newEnd;
            booking.UpdatedAt = rules.Now;
            await db.SaveChangesAsync(token);
            return true;
        }, ct);

        var dto = await GetAsync(id, ct);
        await notifier.BookingUpdatedAsync(dto);
        return dto;
    }

    /// <summary>Finishes a running meeting now and frees the cabin.</summary>
    public async Task<BookingDto> EndEarlyAsync(Guid id, CancellationToken ct)
    {
        var booking = await db.Bookings.FirstOrDefaultAsync(b => b.Id == id, ct)
                      ?? throw new NotFoundException("Booking not found.");
        EnsureCanModify(booking);
        var now = rules.Now;
        if (booking.GetPhase(now) != BookingPhase.InProgress)
            throw new AppValidationException("Only a meeting that is in progress can be ended.");

        // End exactly now so the cabin is free immediately (whole seconds; at least 1s long for the EndTime > StartTime constraint).
        var end = new DateTimeOffset(now.UtcTicks - now.UtcTicks % TimeSpan.TicksPerSecond, TimeSpan.Zero);
        if (end <= booking.StartTime) end = booking.StartTime.AddSeconds(1);
        booking.EndTime = end;
        booking.CheckedInAt ??= booking.StartTime;
        booking.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        var dto = await GetAsync(id, ct);
        await notifier.BookingUpdatedAsync(dto);
        return dto;
    }

    public async Task<BookingDto> CheckInAsync(Guid id, CancellationToken ct)
    {
        var booking = await db.Bookings.FirstOrDefaultAsync(b => b.Id == id, ct)
                      ?? throw new NotFoundException("Booking not found.");
        EnsureCanModify(booking);
        var now = rules.Now;
        if (booking.Status != BookingStatus.Confirmed || now >= booking.EndTime)
            throw new AppValidationException("This booking can no longer be checked in.");
        if (now < booking.StartTime.AddMinutes(-rules.Rules.CheckInOpensMinutesBefore))
            throw new AppValidationException($"Check-in opens {rules.Rules.CheckInOpensMinutesBefore} minutes before the start time.");

        booking.CheckedInAt ??= now;
        booking.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        var dto = await GetAsync(id, ct);
        await notifier.BookingUpdatedAsync(dto);
        return dto;
    }

    public async Task CancelAsync(Guid id, CancellationToken ct)
    {
        var booking = await db.Bookings.FirstOrDefaultAsync(b => b.Id == id, ct)
                      ?? throw new NotFoundException("Booking not found.");
        EnsureCanModify(booking);
        var now = rules.Now;
        var phase = booking.GetPhase(now);
        if (phase == BookingPhase.InProgress)
            throw new AppValidationException("This meeting has already started. Use 'End now' instead.");
        if (phase != BookingPhase.Upcoming)
            throw new AppValidationException("Only upcoming bookings can be cancelled.");

        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAt = now;
        booking.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        await notifier.BookingCancelledAsync(await GetAsync(id, ct));
    }

    /// <summary>Marks bookings that were never checked in as no-shows, freeing the cabin. Called by a background job.</summary>
    public async Task<int> ReleaseNoShowsAsync(CancellationToken ct)
    {
        if (!rules.Rules.AutoReleaseEnabled) return 0;

        var now = rules.Now;
        var cutoff = now.AddMinutes(-rules.Rules.CheckInGraceMinutes);
        var stale = await WithDetails(tracking: true)
            .Where(b => b.Status == BookingStatus.Confirmed && b.CheckedInAt == null && b.StartTime <= cutoff && b.EndTime > now)
            .ToListAsync(ct);
        if (stale.Count == 0) return 0;

        foreach (var b in stale)
        {
            b.Status = BookingStatus.NoShow;
            b.UpdatedAt = now;
        }
        await db.SaveChangesAsync(ct);

        foreach (var b in stale)
        {
            logger.LogInformation("Released booking {BookingId} in cabin {CabinId} as no-show", b.Id, b.CabinId);
            await notifier.BookingCancelledAsync(b.ToDto(now));
        }
        return stale.Count;
    }

    // ---------------------------------------------------------------- helpers

    private async Task<BookingDto> CreateCoreAsync(int cabinId, string title, BookingPurpose? requestedPurpose, string? requestedDetail,
        string? notes, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        var (purpose, purposeDetail) = ValidateDetails(title, requestedPurpose, requestedDetail, notes);
        rules.ValidateNewWindow(start, end);
        await EnsureCabinBookableAsync(cabinId, ct);

        var id = await cabinLock.RunExclusiveAsync([cabinId], async token =>
        {
            await EnsureNoOverlapAsync(cabinId, start, end, null, token);
            await EnsureDailyLimitAsync(start, token);

            var now = rules.Now;
            var booking = new Booking
            {
                CabinId = cabinId,
                BookedByUserId = currentUser.UserId,
                Title = title.Trim(),
                Purpose = purpose,
                PurposeDetail = purposeDetail,
                Notes = notes?.Trim(),
                StartTime = start,
                EndTime = end,
                Status = BookingStatus.Confirmed,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Bookings.Add(booking);
            await db.SaveChangesAsync(token);
            return booking.Id;
        }, ct);

        var dto = await GetAsync(id, ct);
        logger.LogInformation("Booking {BookingId} created for cabin {CabinId} {Start}-{End} by {UserId}",
            id, cabinId, start, end, currentUser.UserId);
        await notifier.BookingCreatedAsync(dto);
        return dto;
    }

    private async Task EnsureNoOverlapAsync(int cabinId, DateTimeOffset start, DateTimeOffset end, Guid? excludeId, CancellationToken ct)
    {
        var clash = await WithDetails()
            .Where(b => b.CabinId == cabinId
                        && b.Status == BookingStatus.Confirmed
                        && b.StartTime < end && b.EndTime > start
                        && (excludeId == null || b.Id != excludeId))
            .OrderBy(b => b.StartTime)
            .FirstOrDefaultAsync(ct);

        if (clash is not null)
            throw new ConflictException(
                $"{clash.Cabin.Name} is already booked by {clash.BookedBy.Name} from {rules.Format(clash.StartTime)} to {rules.Format(clash.EndTime)} ({clash.Title}).");
    }

    private async Task EnsureDailyLimitAsync(DateTimeOffset start, CancellationToken ct)
    {
        var limit = rules.Rules.MaxBookingsPerUserPerDay;
        if (limit <= 0 || currentUser.IsAdmin) return;

        var (dayStart, dayEnd) = rules.DayWindowUtc(DateOnly.FromDateTime(rules.ToLocal(start).DateTime));
        var count = await db.Bookings.CountAsync(b => b.BookedByUserId == currentUser.UserId
                                                      && b.Status == BookingStatus.Confirmed
                                                      && b.StartTime >= dayStart && b.StartTime < dayEnd, ct);
        if (count >= limit)
            throw new AppValidationException($"You can have at most {limit} bookings per day.");
    }

    private async Task EnsureCabinBookableAsync(int cabinId, CancellationToken ct)
    {
        var cabin = await db.Cabins.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cabinId, ct)
                    ?? throw new NotFoundException("Cabin not found.");
        if (!cabin.IsActive) throw new AppValidationException($"{cabin.Name} is currently unavailable.");
    }

    private void EnsureCanModify(Booking booking)
    {
        if (booking.BookedByUserId != currentUser.UserId && !currentUser.IsAdmin)
            throw new ForbiddenException("You can only change your own bookings.");
    }

    /// <summary>Validates project, purpose and notes. Returns the purpose plus its detail (kept only for "Other").</summary>
    private static (BookingPurpose Purpose, string? Detail) ValidateDetails(string? project, BookingPurpose? purpose, string? purposeDetail, string? notes)
    {
        if (string.IsNullOrWhiteSpace(project)) throw new AppValidationException("Project is required.");
        if (project.Trim().Length > MaxTitleLength) throw new AppValidationException($"Project name can be at most {MaxTitleLength} characters.");
        if (purpose is null || !Enum.IsDefined(purpose.Value)) throw new AppValidationException("Purpose is required.");
        if (notes?.Length > MaxNotesLength) throw new AppValidationException($"Notes can be at most {MaxNotesLength} characters.");

        if (purpose != BookingPurpose.Other) return (purpose.Value, null);

        var detail = purposeDetail?.Trim();
        if (string.IsNullOrEmpty(detail)) throw new AppValidationException("Please describe the purpose when choosing 'Other'.");
        if (detail.Length > MaxPurposeDetailLength) throw new AppValidationException($"Purpose description can be at most {MaxPurposeDetailLength} characters.");
        return (BookingPurpose.Other, detail);
    }

    private async Task<Booking> LoadAsync(Guid id, CancellationToken ct) =>
        await WithDetails().FirstOrDefaultAsync(b => b.Id == id, ct) ?? throw new NotFoundException("Booking not found.");

    private IQueryable<Booking> WithDetails(bool tracking = false)
    {
        var q = db.Bookings.Include(b => b.Cabin).Include(b => b.BookedBy).AsQueryable();
        return tracking ? q : q.AsNoTracking();
    }

    private DateTimeOffset RoundUpToSlot(DateTimeOffset value)
    {
        var slot = TimeSpan.FromMinutes(rules.Rules.SlotMinutes).Ticks;
        var ticks = (value.UtcTicks + slot - 1) / slot * slot;
        return new DateTimeOffset(ticks, TimeSpan.Zero);
    }

    private static DateTimeOffset Truncate(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, 0, TimeSpan.Zero);
    }

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;
}
