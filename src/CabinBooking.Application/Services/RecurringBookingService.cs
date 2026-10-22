using System.Text.Json;
using CabinBooking.Application.Abstractions;
using CabinBooking.Application.Common;
using CabinBooking.Application.Dtos;
using CabinBooking.Domain.Entities;
using CabinBooking.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CabinBooking.Application.Services;

public class RecurringBookingService(
    IAppDbContext db,
    ICurrentUser currentUser,
    BookingRuleValidator rules,
    IAuditLogger audit,
    IEmailOutbox emailOutbox,
    ILogger<RecurringBookingService> logger)
{
    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    // ---------------------------------------------------------------- queries

    public async Task<IReadOnlyList<RecurringBookingDto>> GetAllAsync(CancellationToken ct)
    {
        var query = db.RecurringBookings
            .Include(r => r.User)
            .Include(r => r.Cabin)
            .AsNoTracking();

        if (!currentUser.IsAdmin)
            query = query.Where(r => r.UserId == currentUser.UserId);

        var list = await query.ToListAsync(ct);
        return list.OrderByDescending(r => r.CreatedAt).Select(ToDto).ToList();
    }

    // ---------------------------------------------------------------- commands

    public async Task<RecurringBookingDto> CreateAsync(CreateRecurringBookingRequest req, CancellationToken ct)
    {
        Validate(req.Title, req.DurationMinutes, req.StartDate, req.EndDate, req.DaysOfWeek, req.StartMinuteOfDay, rules);

        var cabin = await db.Cabins.AsNoTracking().FirstOrDefaultAsync(c => c.Id == req.CabinId, ct)
                    ?? throw new NotFoundException("Cabin not found.");
        if (!cabin.IsActive) throw new AppValidationException("That cabin is no longer active.");

        var now = rules.Now;
        var rb = new RecurringBooking
        {
            UserId = currentUser.UserId,
            CabinId = req.CabinId,
            Title = req.Title.Trim(),
            Purpose = req.Purpose,
            PurposeDetail = ValidatePurposeDetail(req.Purpose, req.PurposeDetail),
            Notes = req.Notes?.Trim(),
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            StartMinuteOfDay = req.StartMinuteOfDay,
            DurationMinutes = req.DurationMinutes,
            DaysOfWeekJson = JsonSerializer.Serialize(req.DaysOfWeek ?? []),
            AttendeeEmailsJson = req.AttendeeEmails is { Count: > 0 } ? JsonSerializer.Serialize(req.AttendeeEmails) : null,
            IsActive = true,
            CreatedAt = now
        };

        db.RecurringBookings.Add(rb);
        await db.SaveChangesAsync(ct);

        await audit.LogAsync("RecurringCreated", "RecurringBooking", rb.Id.ToString(),
            $"Created recurring '{rb.Title}' on {req.DaysOfWeek} in {cabin.Name}", ct);

        // Immediately create bookings for the upcoming window so they appear on the
        // timeline right away rather than waiting up to 24h for the scheduler run.
        await ScheduleDueAsync(7, ct);

        // Notify attendees they have been added to a recurring meeting.
        if (req.AttendeeEmails is { Count: > 0 })
        {
            var cabinName = cabin.Name;
            var days = Deserialize<int>(rb.DaysOfWeekJson);
            var dayNames = string.Join(", ", days.Select(d => ((DayOfWeek)d).ToString()));
            var startHhmm = $"{req.StartMinuteOfDay / 60:D2}:{req.StartMinuteOfDay % 60:D2}";
            var body = $"""
                <div style="font-family:sans-serif;max-width:600px">
                  <h2 style="color:#1976d2">You've been added to a recurring meeting</h2>
                  <table style="border-collapse:collapse;width:100%">
                    <tr><td style="padding:6px 12px;color:#555"><b>Meeting</b></td><td style="padding:6px 12px">{rb.Title}</td></tr>
                    <tr style="background:#f5f5f5"><td style="padding:6px 12px;color:#555"><b>Room</b></td><td style="padding:6px 12px">{cabinName}</td></tr>
                    <tr><td style="padding:6px 12px;color:#555"><b>Days</b></td><td style="padding:6px 12px">{dayNames}</td></tr>
                    <tr style="background:#f5f5f5"><td style="padding:6px 12px;color:#555"><b>Time</b></td><td style="padding:6px 12px">{startHhmm} ({req.DurationMinutes} min)</td></tr>
                    <tr><td style="padding:6px 12px;color:#555"><b>From</b></td><td style="padding:6px 12px">{rb.StartDate} to {rb.EndDate}</td></tr>
                  </table>
                  <p style="color:#888;font-size:12px;margin-top:24px">You will receive a reminder 15 minutes before each occurrence.</p>
                </div>
                """;
            foreach (var email in req.AttendeeEmails.Where(e => !string.IsNullOrWhiteSpace(e)).Distinct(StringComparer.OrdinalIgnoreCase))
                await emailOutbox.QueueEmailAsync(new EmailNotification(email, $"You've been added to a recurring meeting: {rb.Title}", body));
        }

        return await LoadDtoAsync(rb.Id, ct);
    }

    public async Task<RecurringBookingDto> UpdateAsync(Guid id, UpdateRecurringBookingRequest req, CancellationToken ct)
    {
        var rb = await db.RecurringBookings.FirstOrDefaultAsync(r => r.Id == id, ct)
                 ?? throw new NotFoundException("Recurring booking not found.");
        EnsureOwnerOrAdmin(rb.UserId);

        if (req.EndDate.HasValue)
        {
            if (req.EndDate.Value < rb.StartDate) throw new AppValidationException("End date cannot be before start date.");
            rb.EndDate = req.EndDate.Value;
        }
        if (req.IsActive.HasValue) rb.IsActive = req.IsActive.Value;
        if (req.DaysOfWeek is not null)
        {
            if (req.DaysOfWeek.Count == 0) throw new AppValidationException("At least one day of the week is required.");
            rb.DaysOfWeekJson = JsonSerializer.Serialize(req.DaysOfWeek);
        }
        if (req.AttendeeEmails is not null)
            rb.AttendeeEmailsJson = req.AttendeeEmails.Count > 0 ? JsonSerializer.Serialize(req.AttendeeEmails) : null;
        if (req.Notes is not null)
            rb.Notes = req.Notes.Trim();
        if (req.CabinId is > 0)
        {
            var cabin = await db.Cabins.AsNoTracking().FirstOrDefaultAsync(c => c.Id == req.CabinId.Value, ct)
                        ?? throw new NotFoundException("Cabin not found.");
            if (!cabin.IsActive) throw new AppValidationException("That cabin is no longer active.");
            rb.CabinId = req.CabinId.Value;
        }
        if (req.Title is { Length: > 0 })
        {
            var t = req.Title.Trim();
            if (t.Length < 2) throw new AppValidationException("Title must be at least 2 characters.");
            if (t.Length > 120) throw new AppValidationException("Title must be 120 characters or fewer.");
            rb.Title = t;
        }
        if (req.StartMinuteOfDay.HasValue)
        {
            if (req.StartMinuteOfDay.Value < 0 || req.StartMinuteOfDay.Value >= 1440)
                throw new AppValidationException("StartMinuteOfDay must be 0–1439.");
            rb.StartMinuteOfDay = req.StartMinuteOfDay.Value;
        }
        if (req.DurationMinutes.HasValue)
        {
            if (req.DurationMinutes.Value <= 0) throw new AppValidationException("Duration must be positive.");
            rb.DurationMinutes = req.DurationMinutes.Value;
        }

        await db.SaveChangesAsync(ct);
        await audit.LogAsync("RecurringUpdated", "RecurringBooking", id.ToString(), $"Updated recurring '{rb.Title}'", ct);
        return await LoadDtoAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var rb = await db.RecurringBookings.FirstOrDefaultAsync(r => r.Id == id, ct)
                 ?? throw new NotFoundException("Recurring booking not found.");
        EnsureOwnerOrAdmin(rb.UserId);

        db.RecurringBookings.Remove(rb);
        await db.SaveChangesAsync(ct);
        await audit.LogAsync("RecurringDeleted", "RecurringBooking", id.ToString(), $"Deleted recurring '{rb.Title}'", ct);
    }

    /// <summary>
    /// Creates individual <see cref="Booking"/> records for every active recurring schedule
    /// for the next <paramref name="daysAhead"/> days. Skips dates that already have an instance
    /// record. Skips silently on cabin conflict. Returns the count of new bookings created.
    /// </summary>
    public async Task<int> ScheduleDueAsync(int daysAhead, CancellationToken ct)
    {
        var today = rules.Today;
        var horizon = today.AddDays(daysAhead);
        var created = 0;

        // DateOnly comparisons are not translatable by the SQLite EF Core provider,
        // so filter IsActive in SQL and apply the date-range check in-process.
        var all = await db.RecurringBookings
            .Include(r => r.Instances)
            .Include(r => r.User)
            .Where(r => r.IsActive)
            .ToListAsync(ct);
        var schedules = all.Where(r => r.StartDate <= horizon && r.EndDate >= today).ToList();

        foreach (var rb in schedules)
        {
            var days = Deserialize<int>(rb.DaysOfWeekJson);
            var from = rb.StartDate > today ? rb.StartDate : today;
            var to = rb.EndDate < horizon ? rb.EndDate : horizon;
            var alreadyDone = rb.Instances.Select(i => i.Date).ToHashSet();

            // DateTimeOffset comparisons are not translatable by the SQLite EF Core provider,
            // so load confirmed bookings for this cabin into memory and check conflicts in-process.
            var cabinBookings = await db.Bookings
                .Where(b => b.CabinId == rb.CabinId && b.Status == BookingStatus.Confirmed)
                .ToListAsync(ct);

            for (var d = from; d <= to; d = d.AddDays(1))
            {
                if (!days.Contains((int)d.DayOfWeek)) continue;
                if (alreadyDone.Contains(d)) continue;

                var startUtc = ToUtc(d, rb.StartMinuteOfDay);
                var endUtc = startUtc.AddMinutes(rb.DurationMinutes);

                var conflict = cabinBookings.Any(b => b.StartTime < endUtc && b.EndTime > startUtc);

                var instance = new RecurringBookingInstance
                {
                    RecurringBookingId = rb.Id,
                    Date = d
                };

                if (!conflict)
                {
                    var booking = new Booking
                    {
                        CabinId = rb.CabinId,
                        BookedByUserId = rb.UserId,
                        Title = rb.Title,
                        Purpose = rb.Purpose,
                        PurposeDetail = rb.PurposeDetail,
                        Notes = rb.Notes,
                        StartTime = startUtc,
                        EndTime = endUtc,
                        Status = BookingStatus.Confirmed,
                        CreatedAt = rules.Now,
                        UpdatedAt = rules.Now
                    };
                    db.Bookings.Add(booking);
                    await db.SaveChangesAsync(ct);

                    instance.BookingId = booking.Id;
                    created++;
                    logger.LogInformation("Recurring scheduler created booking {BookingId} for {Date} ({Title})", booking.Id, d, rb.Title);
                }
                else
                {
                    logger.LogWarning("Recurring scheduler skipped {Date} ({Title}) — cabin {CabinId} conflict", d, rb.Title, rb.CabinId);
                }

                db.RecurringBookingInstances.Add(instance);
                await db.SaveChangesAsync(ct);
            }
        }

        return created;
    }

    // ---------------------------------------------------------------- helpers

    private DateTimeOffset ToUtc(DateOnly date, int startMinute)
    {
        var localMidnight = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var localStart = localMidnight.AddMinutes(startMinute);
        return TimeZoneInfo.ConvertTimeToUtc(localStart, rules.Zone);
    }

    private static List<T> Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<T>>(json, _jsonOpts) ?? []; }
        catch { return []; }
    }

    private static RecurringBookingDto ToDto(RecurringBooking rb) => new(
        rb.Id,
        rb.UserId,
        rb.User?.Name ?? string.Empty,
        rb.CabinId,
        rb.Cabin?.Name ?? string.Empty,
        rb.Title,
        rb.Purpose,
        rb.PurposeDetail,
        rb.Notes,
        rb.StartDate,
        rb.EndDate,
        rb.StartMinuteOfDay,
        rb.DurationMinutes,
        Deserialize<int>(rb.DaysOfWeekJson),
        Deserialize<string>(rb.AttendeeEmailsJson),
        rb.IsActive,
        rb.CreatedAt);

    private async Task<RecurringBookingDto> LoadDtoAsync(Guid id, CancellationToken ct)
    {
        var rb = await db.RecurringBookings
            .Include(r => r.User)
            .Include(r => r.Cabin)
            .AsNoTracking()
            .FirstAsync(r => r.Id == id, ct);
        return ToDto(rb);
    }

    private void EnsureOwnerOrAdmin(Guid ownerId)
    {
        if (!currentUser.IsAdmin && currentUser.UserId != ownerId)
            throw new ForbiddenException("You do not own this recurring booking.");
    }

    private static void Validate(string title, int duration, DateOnly start, DateOnly end, List<int> days, int startMinute, BookingRuleValidator rules)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length < 2)
            throw new AppValidationException("Title must be at least 2 characters.");
        if (title.Length > 120)
            throw new AppValidationException("Title must be 120 characters or fewer.");
        if (duration <= 0)
            throw new AppValidationException("Duration must be positive.");
        if (end < start)
            throw new AppValidationException("End date cannot be before start date.");
        if (days == null || days.Count == 0)
            throw new AppValidationException("At least one day of the week is required.");
        if (startMinute < 0 || startMinute >= 1440)
            throw new AppValidationException("StartMinuteOfDay must be 0–1439.");

        var officeStartMin = rules.Rules.OfficeStart.Hour * 60 + rules.Rules.OfficeStart.Minute;
        var officeEndMin = rules.Rules.OfficeEnd.Hour * 60 + rules.Rules.OfficeEnd.Minute;
        if (startMinute < officeStartMin || startMinute + duration > officeEndMin)
            throw new AppValidationException($"Recurring meeting must fall within office hours ({rules.Rules.OfficeStart:hh\\:mm}–{rules.Rules.OfficeEnd:hh\\:mm}).");

        var validDays = rules.Rules.WorkingDays;
        if (validDays.Length > 0 && !days.Any(d => validDays.Contains((DayOfWeek)d)))
            throw new AppValidationException("At least one selected day must be a working day.");
    }

    private static string? ValidatePurposeDetail(string purpose, string? detail)
    {
        if (purpose == "Other" && string.IsNullOrWhiteSpace(detail))
            throw new AppValidationException("Purpose detail is required when purpose is 'Other'.");
        return purpose == "Other" ? detail?.Trim() : null;
    }
}
