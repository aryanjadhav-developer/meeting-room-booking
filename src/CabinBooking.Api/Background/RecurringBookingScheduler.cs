using CabinBooking.Application.Services;

namespace CabinBooking.Api.Background;

public class RecurringBookingScheduler(
    IServiceScopeFactory scopeFactory,
    ILogger<RecurringBookingScheduler> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private const int DaysAhead = 7;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Recurring booking scheduler started.");
        // Run once at startup, then every 24 hours.
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunAsync(stoppingToken);
            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<RecurringBookingService>();
            var count = await service.ScheduleDueAsync(DaysAhead, ct);
            logger.LogInformation("Recurring scheduler created {Count} bookings for the next {Days} days.", count, DaysAhead);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Error in recurring booking scheduler.");
        }
    }
}
