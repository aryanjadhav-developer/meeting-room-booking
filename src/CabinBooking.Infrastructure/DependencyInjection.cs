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
        var provider = configuration["Database:Provider"] ?? "Sqlite";
        var connectionString = configuration.GetConnectionString("CabinBooking");

        if (string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Connection string 'CabinBooking' is required when Database:Provider is SqlServer.");

            // No retrying execution strategy: SqlServerCabinLock opens its own transaction, which retry strategies don't allow.
            services.AddDbContext<AppDbContext>(o =>
            {
                o.UseSqlServer(connectionString);
                o.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
            });
            services.AddScoped<ICabinLock, SqlServerCabinLock>();
        }
        else
        {
            // Default: SQLite provider for zero-setup execution
            var sqliteConn = string.IsNullOrWhiteSpace(connectionString)
                ? "Data Source=CabinBooking.db"
                : connectionString;

            services.AddDbContext<AppDbContext>(o =>
            {
                o.UseSqlite(sqliteConn);
                o.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
            });
            services.AddScoped<ICabinLock, SqliteCabinLock>();
        }

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddSingleton<Email.EmailSender>();
        services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<Email.EmailSender>());
        services.AddSingleton<Email.IPreviewEmailStore>(sp => sp.GetRequiredService<Email.EmailSender>());
        return services;
    }
}
