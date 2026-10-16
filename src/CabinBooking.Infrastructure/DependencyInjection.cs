using CabinBooking.Application.Abstractions;
using CabinBooking.Infrastructure.Persistence;
using CabinBooking.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CabinBooking.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("CabinBooking")
            ?? throw new InvalidOperationException("Connection string 'CabinBooking' is not configured.");

        // No retrying execution strategy: SqlServerCabinLock opens its own transaction, which retry strategies don't allow.
        services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<ICabinLock, SqlServerCabinLock>();
        services.AddSingleton<IPasswordService, PasswordService>();
        return services;
    }
}
