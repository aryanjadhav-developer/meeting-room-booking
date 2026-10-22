using CabinBooking.Application.Services;

namespace CabinBooking.Api.Background;

/// <summary>Periodically releases bookings nobody checked in to (only when BookingRules:AutoReleaseEnabled is true).</summary>
public class NoShowReleaseService(IServiceScopeFactory scopes, BookingRuleValidator ruleValidator, ILogger<NoShowReleaseService> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (!ruleValidator.Rules.AutoReleaseEnabled)
            {
                logger.LogDebug("No-show auto release is disabled — skipping tick");
                continue;
            }

            try
            {
                using var scope = scopes.CreateScope();
                var bookings = scope.ServiceProvider.GetRequiredService<BookingService>();
                await bookings.ReleaseNoShowsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "No-show release run failed");
            }
        }
    }
}
