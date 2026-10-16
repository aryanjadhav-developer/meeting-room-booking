using CabinBooking.Application.Abstractions;
using CabinBooking.Domain.Entities;
using CabinBooking.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CabinBooking.Infrastructure.Persistence;

public static class DemoSeeder
{
    public static async Task SeedDemoAsync(IServiceProvider services)
    {
        var config = services.GetRequiredService<IConfiguration>();
        if (!string.Equals(config["Demo:Enabled"], "true", StringComparison.OrdinalIgnoreCase))
            return;

        var db = services.GetRequiredService<AppDbContext>();
        var clock = services.GetRequiredService<TimeProvider>();
        var passwords = services.GetRequiredService<IPasswordService>();

        if (!await db.Cabins.AnyAsync())
        {
            db.Cabins.AddRange(
                new Cabin { Id = 1, Name = "Lunch Room", Description = "Large room for big groups", Capacity = 9, CapacityLabel = "9+", Amenities = "TV,Whiteboard", Color = "#1976d2", IsActive = true },
                new Cabin { Id = 2, Name = "Conference Room", Description = "Meeting room with display", Capacity = 5, CapacityLabel = "5+", Amenities = "TV,Whiteboard", Color = "#2e7d32", IsActive = true },
                new Cabin { Id = 3, Name = "Cabin 3", Description = "Small cabin for focused discussions", Capacity = 4, CapacityLabel = "3-4", Amenities = "", Color = "#ed6c02", IsActive = true }
            );
            await db.SaveChangesAsync();
        }

        if (!await db.Users.AnyAsync(u => u.Email == "aryanjadhav4305@gmail.com"))
        {
            var admin = new User
            {
                Id = Guid.NewGuid(),
                Name = "Aryan Jadhav",
                Email = "aryanjadhav4305@gmail.com",
                Role = UserRole.Admin,
                IsActive = true,
                CreatedAt = clock.GetUtcNow()
            };
            admin.PasswordHash = passwords.Hash(admin, "Admin@123");
            db.Users.Add(admin);
            await db.SaveChangesAsync();
        }
    }
}
