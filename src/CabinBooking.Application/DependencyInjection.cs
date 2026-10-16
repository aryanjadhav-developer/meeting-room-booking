using CabinBooking.Application.Services;
using CabinBooking.Application.Validation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CabinBooking.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<BookingRuleValidator>();
        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
        services.AddScoped<BookingService>();
        services.AddScoped<CabinService>();
        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<ReportService>();
        services.AddScoped<AuditService>();
        services.AddScoped<SettingsService>();
        return services;
    }
}
