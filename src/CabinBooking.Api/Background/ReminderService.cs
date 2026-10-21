using CabinBooking.Application.Abstractions;
using CabinBooking.Application.Services;
using CabinBooking.Domain.Enums;
using CabinBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CabinBooking.Api.Background;

public class ReminderService(
    IServiceScopeFactory scopeFactory,
    IEmailOutbox emailOutbox,
    BookingRuleValidator rules,
    ILogger<ReminderService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Reminder service started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }

            await SendRemindersAsync(stoppingToken);
        }
    }

    private async Task SendRemindersAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var now = DateTimeOffset.UtcNow;
            var windowStart = now.AddMinutes(10);
            var windowEnd = now.AddMinutes(16);

            var bookings = await db.Bookings
                .Include(b => b.BookedBy)
                .Include(b => b.Cabin)
                .Where(b =>
                    b.Status == BookingStatus.Confirmed &&
                    b.ReminderSentAt == null &&
                    b.StartTime >= windowStart &&
                    b.StartTime <= windowEnd)
                .ToListAsync(ct);

            if (bookings.Count == 0) return;

            foreach (var booking in bookings)
            {
                var tz = rules.Zone;
                var localStart = TimeZoneInfo.ConvertTimeFromUtc(booking.StartTime.UtcDateTime, tz);
                var subject = $"Reminder: {booking.Title} starts in 15 minutes";
                var body = $@"<div style=""font-family:sans-serif;max-width:600px"">
  <h2 style=""color:#1976d2"">Meeting Starting Soon</h2>
  <table style=""border-collapse:collapse;width:100%"">
    <tr><td style=""padding:6px 12px;color:#555""><b>Meeting</b></td><td style=""padding:6px 12px"">{booking.Title}</td></tr>
    <tr style=""background:#f5f5f5""><td style=""padding:6px 12px;color:#555""><b>Room</b></td><td style=""padding:6px 12px"">{booking.Cabin?.Name}</td></tr>
    <tr><td style=""padding:6px 12px;color:#555""><b>Starts at</b></td><td style=""padding:6px 12px"">{localStart:HH:mm}</td></tr>
    <tr style=""background:#f5f5f5""><td style=""padding:6px 12px;color:#555""><b>Duration</b></td><td style=""padding:6px 12px"">{(int)(booking.EndTime - booking.StartTime).TotalMinutes} minutes</td></tr>
  </table>
  <p style=""color:#888;font-size:12px;margin-top:24px"">You are receiving this reminder because you have a booking starting soon.</p>
</div>";

                await emailOutbox.QueueEmailAsync(new EmailNotification(
                    booking.BookedBy.Email,
                    subject,
                    body));

                booking.ReminderSentAt = now;
            }

            await db.SaveChangesAsync(ct);
            logger.LogInformation("Sent {Count} reminder email(s).", bookings.Count);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Error in reminder service.");
        }
    }
}
