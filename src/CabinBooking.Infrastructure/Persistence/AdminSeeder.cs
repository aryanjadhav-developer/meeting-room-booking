using CabinBooking.Application.Abstractions;
using CabinBooking.Application.Options;
using CabinBooking.Domain.Entities;
using CabinBooking.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CabinBooking.Infrastructure.Persistence;

public static class AdminSeeder
{
    public static async Task SeedAdminAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var adminOptions = services.GetRequiredService<IOptions<AdminOptions>>().Value;

        if (string.IsNullOrWhiteSpace(adminOptions.Email) || string.IsNullOrWhiteSpace(adminOptions.Password))
            return;

        var email = adminOptions.Email.Trim().ToLowerInvariant();
        var adminExists = await db.Users.AnyAsync(u => u.Role == UserRole.Admin);
        if (adminExists)
            return;

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user != null)
        {
            user.Role = UserRole.Admin;
            await db.SaveChangesAsync();
            return;
        }

        var passwords = services.GetRequiredService<IPasswordService>();
        var clock = services.GetRequiredService<TimeProvider>();

        var admin = new User
        {
            Id = Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(adminOptions.Name) ? "Administrator" : adminOptions.Name.Trim(),
            Email = email,
            Role = UserRole.Admin,
            IsActive = true,
            CreatedAt = clock.GetUtcNow()
        };
        admin.PasswordHash = passwords.Hash(admin, adminOptions.Password);

        db.Users.Add(admin);
        await db.SaveChangesAsync();
    }
}
