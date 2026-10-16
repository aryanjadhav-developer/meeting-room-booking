using CabinBooking.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CabinBooking.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<BookingRuleValidator>();
        services.AddScoped<BookingService>();
        services.AddScoped<CabinService>();
        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<ReportService>();
        return services;
    }
}
