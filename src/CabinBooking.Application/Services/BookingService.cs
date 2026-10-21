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
    IAuditLogger audit,
    IEmailOutbox emailOutbox,
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

        var query = WithDetails();
        if (cabinId is not null) query = query.Where(b => b.CabinId == cabinId);

        var list = (await query.ToListAsync(ct))
            .Where(b => b.StartTime < to && b.EndTime > from && b.Status != BookingStatus.Cancelled)
            .OrderBy(b => b.StartTime).ToList();
        return list.Select(b => b.ToDto(rules.Now)).ToList();
    }

    public async Task<IReadOnlyList<BookingDto>> GetMineAsync(bool includePast, CancellationToken ct)
    {
        var now = rules.Now;
        var cutoff = now.AddDays(-90);
        var all = await WithDetails().Where(b => b.BookedByUserId == currentUser.UserId).ToListAsync(ct);
        var list = all
            .Where(b => includePast ? b.StartTime > cutoff : b.EndTime > now && b.Status == BookingStatus.Confirmed)
            .OrderBy(b => b.StartTime).ToList();
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

        var busy = (await db.Bookings.AsNoTracking()
            .Where(b => b.CabinId == cabinId)
            .ToListAsync(ct))
            .Where(b => b.StartTime < close && b.EndTime > open && b.Status == BookingStatus.Confirmed)
            .OrderBy(b => b.StartTime)
            .Select(b => new { b.StartTime, b.EndTime }).ToList();

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
            Truncate(request.StartTime), Truncate(request.EndTime), request.AttendeeEmails, ct);

    /// <summary>Books a cabin starting right now (rounded down to the minute).</summary>
    public Task<BookingDto> QuickBookAsync(QuickBookRequest request, CancellationToken ct)
    {
        var start = Truncate(rules.Now);
        var title = string.IsNullOrWhiteSpace(request.Title) ? "Quick meeting" : request.Title;
        return CreateCoreAsync(request.CabinId, title, BookingPurpose.Meeting, null, null,
            start, start.AddMinutes(request.DurationMinutes), null, ct);
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
        await audit.LogAsync("BookingUpdated", "Booking", id.ToString(), $"Updated booking '{dto.Title}' in {dto.CabinName}", ct);
        var ics = IcsCalendarBuilder.BuildIcs(dto.Id, dto.Title, dto.CabinName, "noreply@meetingrooms.local", dto.BookedByEmail, dto.StartTime, dto.EndTime, dto.Notes, "REQUEST");
        var updatedBody = $@"<div style=""font-family:sans-serif;max-width:600px"">
  <h2 style=""color:#1976d2"">Booking Updated</h2>
  <table style=""border-collapse:collapse;width:100%"">
    <tr><td style=""padding:6px 12px;color:#555""><b>Meeting</b></td><td style=""padding:6px 12px"">{dto.Title}</td></tr>
    <tr style=""background:#f5f5f5""><td style=""padding:6px 12px;color:#555""><b>Room</b></td><td style=""padding:6px 12px"">{dto.CabinName}</td></tr>
    <tr><td style=""padding:6px 12px;color:#555""><b>Booked by</b></td><td style=""padding:6px 12px"">{dto.BookedByName} ({dto.BookedByEmail})</td></tr>
    <tr style=""background:#f5f5f5""><td style=""padding:6px 12px;color:#555""><b>Date &amp; Time</b></td><td style=""padding:6px 12px"">{rules.Format(dto.StartTime)} – {rules.Format(dto.EndTime)}</td></tr>
    <tr><td style=""padding:6px 12px;color:#555""><b>Duration</b></td><td style=""padding:6px 12px"">{dto.DurationMinutes} minutes</td></tr>
    {(string.IsNullOrWhiteSpace(dto.Notes) ? "" : $"<tr style=\"background:#f5f5f5\"><td style=\"padding:6px 12px;color:#555\"><b>Notes</b></td><td style=\"padding:6px 12px\">{dto.Notes}</td></tr>")}
  </table>
  <p style=""color:#888;font-size:12px;margin-top:24px"">The calendar invite attached to this email has been updated.</p>
</div>";
        await emailOutbox.QueueEmailAsync(new EmailNotification(dto.BookedByEmail, $"Booking Updated: {dto.Title} ({dto.CabinName})", updatedBody, ics, $"meeting_{dto.Id:N}.ics"));
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
        await audit.LogAsync("BookingExtended", "Booking", id.ToString(), $"Extended booking '{dto.Title}' by {minutes} mins", ct);
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
        await audit.LogAsync("BookingEndedEarly", "Booking", id.ToString(), $"Ended meeting early for '{dto.Title}'", ct);
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
        await audit.LogAsync("BookingCheckedIn", "Booking", id.ToString(), $"Checked in for '{dto.Title}'", ct);
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

        var dto = await GetAsync(id, ct);
        await notifier.BookingCancelledAsync(dto);
        await audit.LogAsync("BookingCancelled", "Booking", id.ToString(), $"Cancelled booking '{booking.Title}'", ct);
        var ics = IcsCalendarBuilder.BuildIcs(dto.Id, dto.Title, dto.CabinName, "noreply@meetingrooms.local", dto.BookedByEmail, dto.StartTime, dto.EndTime, dto.Notes, "CANCEL");
        var cancelBody = $@"<div style=""font-family:sans-serif;max-width:600px"">
  <h2 style=""color:#d32f2f"">Booking Cancelled</h2>
  <table style=""border-collapse:collapse;width:100%"">
    <tr><td style=""padding:6px 12px;color:#555""><b>Meeting</b></td><td style=""padding:6px 12px"">{dto.Title}</td></tr>
    <tr style=""background:#f5f5f5""><td style=""padding:6px 12px;color:#555""><b>Room</b></td><td style=""padding:6px 12px"">{dto.CabinName}</td></tr>
    <tr><td style=""padding:6px 12px;color:#555""><b>Booked by</b></td><td style=""padding:6px 12px"">{dto.BookedByName} ({dto.BookedByEmail})</td></tr>
    <tr style=""background:#f5f5f5""><td style=""padding:6px 12px;color:#555""><b>Was scheduled</b></td><td style=""padding:6px 12px"">{rules.Format(dto.StartTime)} – {rules.Format(dto.EndTime)}</td></tr>
  </table>
  <p style=""color:#888;font-size:12px;margin-top:24px"">This meeting has been cancelled. The calendar invite attached removes it from your calendar.</p>
</div>";
        await emailOutbox.QueueEmailAsync(new EmailNotification(dto.BookedByEmail, $"Booking Cancelled: {dto.Title} ({dto.CabinName})", cancelBody, ics, $"cancel_{dto.Id:N}.ics"));
    }

    /// <summary>Marks bookings that were never checked in as no-shows, freeing the cabin. Called by a background job.</summary>
    public async Task<int> ReleaseNoShowsAsync(CancellationToken ct)
    {
        if (!rules.Rules.AutoReleaseEnabled) return 0;

        var now = rules.Now;
        var cutoff = now.AddMinutes(-rules.Rules.CheckInGraceMinutes);
        var stale = (await WithDetails(tracking: true).ToListAsync(ct))
            .Where(b => b.CheckedInAt == null && b.StartTime <= cutoff && b.EndTime > now && b.Status == BookingStatus.Confirmed).ToList();
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
        string? notes, DateTimeOffset start, DateTimeOffset end, List<string>? attendeeEmails, CancellationToken ct)
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
        await audit.LogAsync("BookingCreated", "Booking", id.ToString(), $"Booked '{dto.Title}' in {dto.CabinName}", ct);
        var ics = IcsCalendarBuilder.BuildIcs(dto.Id, dto.Title, dto.CabinName, "noreply@meetingrooms.local", dto.BookedByEmail, dto.StartTime, dto.EndTime, dto.Notes, "REQUEST");
        var confirmedSubject = $"Booking Confirmed: {dto.Title} ({dto.CabinName})";
        var confirmedBody = BuildConfirmedBody(dto, attendeeEmails, isOrganizer: true);
        await emailOutbox.QueueEmailAsync(new EmailNotification(dto.BookedByEmail, confirmedSubject, confirmedBody, ics, $"meeting_{dto.Id:N}.ics"));
        if (attendeeEmails is { Count: > 0 })
        {
            var attendeeBody = BuildConfirmedBody(dto, attendeeEmails, isOrganizer: false);
            foreach (var email in attendeeEmails.Where(e => !string.IsNullOrWhiteSpace(e) && !e.Equals(dto.BookedByEmail, StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase))
                await emailOutbox.QueueEmailAsync(new EmailNotification(email, confirmedSubject, attendeeBody, ics, $"meeting_{dto.Id:N}.ics"));
        }
        return dto;
    }

    private async Task EnsureNoOverlapAsync(int cabinId, DateTimeOffset start, DateTimeOffset end, Guid? excludeId, CancellationToken ct)
    {
        var clash = (await WithDetails()
            .Where(b => b.CabinId == cabinId && (excludeId == null || b.Id != excludeId))
            .ToListAsync(ct))
            .Where(b => b.Status == BookingStatus.Confirmed && b.StartTime < end && b.EndTime > start)
            .OrderBy(b => b.StartTime)
            .FirstOrDefault();

        if (clash is not null)
            throw new ConflictException(
                $"{clash.Cabin.Name} is already booked by {clash.BookedBy.Name} from {rules.Format(clash.StartTime)} to {rules.Format(clash.EndTime)} ({clash.Title}).");
    }

    private async Task EnsureDailyLimitAsync(DateTimeOffset start, CancellationToken ct)
    {
        var limit = rules.Rules.MaxBookingsPerUserPerDay;
        if (limit <= 0 || currentUser.IsAdmin) return;

        var (dayStart, dayEnd) = rules.DayWindowUtc(DateOnly.FromDateTime(rules.ToLocal(start).DateTime));
        var count = (await db.Bookings.AsNoTracking()
            .Where(b => b.BookedByUserId == currentUser.UserId)
            .ToListAsync(ct))
            .Count(b => b.Status == BookingStatus.Confirmed && b.StartTime >= dayStart && b.StartTime < dayEnd);
        if (count >= limit)
            throw new AppValidationException($"You can have at most {limit} bookings per day.");
    }

    private string BuildConfirmedBody(BookingDto dto, List<string>? attendeeEmails, bool isOrganizer)
    {
        var attendeeList = attendeeEmails is { Count: > 0 }
            ? string.Join(", ", attendeeEmails)
            : "—";
        var role = isOrganizer ? "You have booked this room." : $"You have been invited to this meeting by <b>{dto.BookedByName}</b>.";
        return $@"<div style=""font-family:sans-serif;max-width:600px"">
  <h2 style=""color:#2e7d32"">Booking Confirmed</h2>
  <p>{role}</p>
  <table style=""border-collapse:collapse;width:100%"">
    <tr><td style=""padding:6px 12px;color:#555""><b>Meeting</b></td><td style=""padding:6px 12px"">{dto.Title}</td></tr>
    <tr style=""background:#f5f5f5""><td style=""padding:6px 12px;color:#555""><b>Room</b></td><td style=""padding:6px 12px"">{dto.CabinName}</td></tr>
    <tr><td style=""padding:6px 12px;color:#555""><b>Booked by</b></td><td style=""padding:6px 12px"">{dto.BookedByName} ({dto.BookedByEmail})</td></tr>
    <tr style=""background:#f5f5f5""><td style=""padding:6px 12px;color:#555""><b>Date &amp; Time</b></td><td style=""padding:6px 12px"">{rules.Format(dto.StartTime)} – {rules.Format(dto.EndTime)}</td></tr>
    <tr><td style=""padding:6px 12px;color:#555""><b>Duration</b></td><td style=""padding:6px 12px"">{dto.DurationMinutes} minutes</td></tr>
    <tr style=""background:#f5f5f5""><td style=""padding:6px 12px;color:#555""><b>Purpose</b></td><td style=""padding:6px 12px"">{dto.Purpose}{(string.IsNullOrWhiteSpace(dto.PurposeDetail) ? "" : $" – {dto.PurposeDetail}")}</td></tr>
    <tr><td style=""padding:6px 12px;color:#555""><b>Attendees</b></td><td style=""padding:6px 12px"">{attendeeList}</td></tr>
    {(string.IsNullOrWhiteSpace(dto.Notes) ? "" : $"<tr style=\"background:#f5f5f5\"><td style=\"padding:6px 12px;color:#555\"><b>Notes</b></td><td style=\"padding:6px 12px\">{dto.Notes}</td></tr>")}
  </table>
  <p style=""color:#888;font-size:12px;margin-top:24px"">A calendar invite is attached. Add it to your calendar to get a reminder.</p>
</div>";
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
