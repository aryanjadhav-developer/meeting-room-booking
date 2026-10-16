using CabinBooking.Application.Options;
using CabinBooking.Application.Services;
using Microsoft.Extensions.Options;

namespace CabinBooking.Api.Background;

/// <summary>Periodically releases bookings nobody checked in to (only when BookingRules:AutoReleaseEnabled is true).</summary>
public class NoShowReleaseService(IServiceScopeFactory scopes, IOptions<BookingRulesOptions> rules, ILogger<NoShowReleaseService> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!rules.Value.AutoReleaseEnabled)
        {
            logger.LogInformation("No-show auto release is disabled");
            return;
        }

        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
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
