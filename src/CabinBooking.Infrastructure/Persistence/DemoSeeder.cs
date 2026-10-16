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

        // Ensure cabins exist
        if (!await db.Cabins.AnyAsync())
        {
            db.Cabins.AddRange(
                new Cabin { Id = 1, Name = "Lunch Room", Description = "Large room for big groups", Capacity = 9, CapacityLabel = "9+", Amenities = "TV,Whiteboard", Color = "#1976d2", IsActive = true },
                new Cabin { Id = 2, Name = "Conference Room", Description = "Meeting room with display", Capacity = 5, CapacityLabel = "5+", Amenities = "TV,Whiteboard", Color = "#2e7d32", IsActive = true },
                new Cabin { Id = 3, Name = "Cabin 3", Description = "Small cabin for focused discussions", Capacity = 4, CapacityLabel = "3-4", Amenities = "", Color = "#ed6c02", IsActive = true }
            );
            await db.SaveChangesAsync();
        }

        // Demo admin
        var admin = await db.Users.FirstOrDefaultAsync(u => u.Email == "demo.admin@example.com");
        if (admin is null)
        {
            admin = new User
            {
                Id = Guid.NewGuid(),
                Name = "Demo Administrator",
                Email = "demo.admin@example.com",
                Role = UserRole.Admin,
                IsActive = true,
                CreatedAt = clock.GetUtcNow()
            };
            admin.PasswordHash = passwords.Hash(admin, "DemoAdmin123!");
            db.Users.Add(admin);
        }

        // Demo users (fictional)
        var alex = await db.Users.FirstOrDefaultAsync(u => u.Email == "alex.carter@example.com");
        if (alex is null)
        {
            alex = new User
            {
                Id = Guid.NewGuid(),
                Name = "Alex Carter",
                Email = "alex.carter@example.com",
                Role = UserRole.Employee,
                IsActive = true,
                CreatedAt = clock.GetUtcNow()
            };
            alex.PasswordHash = passwords.Hash(alex, "DemoPass123!");
            db.Users.Add(alex);
        }

        var priya = await db.Users.FirstOrDefaultAsync(u => u.Email == "priya.shah@example.com");
        if (priya is null)
        {
            priya = new User
            {
                Id = Guid.NewGuid(),
                Name = "Priya Shah",
                Email = "priya.shah@example.com",
                Role = UserRole.Employee,
                IsActive = true,
                CreatedAt = clock.GetUtcNow()
            };
            priya.PasswordHash = passwords.Hash(priya, "DemoPass123!");
            db.Users.Add(priya);
        }

        var jordan = await db.Users.FirstOrDefaultAsync(u => u.Email == "jordan.lee@example.com");
        if (jordan is null)
        {
            jordan = new User
            {
                Id = Guid.NewGuid(),
                Name = "Jordan Lee",
                Email = "jordan.lee@example.com",
                Role = UserRole.Employee,
                IsActive = true,
                CreatedAt = clock.GetUtcNow()
            };
            jordan.PasswordHash = passwords.Hash(jordan, "DemoPass123!");
            db.Users.Add(jordan);
        }

        await db.SaveChangesAsync();

        // If bookings are already seeded, do not duplicate
        if (await db.Bookings.AnyAsync())
            return;

        var now = clock.GetUtcNow();
        var today = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.FromHours(5.5)); // IST base

        var demoBookings = new List<Booking>
        {
            new()
            {
                CabinId = 1,
                BookedByUserId = alex.Id,
                Title = "Project Alpha - Sprint Planning",
                Purpose = BookingPurpose.Meeting,
                StartTime = today.AddHours(10),
                EndTime = today.AddHours(11).AddMinutes(30),
                Status = BookingStatus.Confirmed,
                CreatedAt = now.AddDays(-1),
                UpdatedAt = now.AddDays(-1)
            },
            new()
            {
                CabinId = 2,
                BookedByUserId = priya.Id,
                Title = "Project Beta - Architecture Sync",
                Purpose = BookingPurpose.Discussion,
                StartTime = today.AddHours(11),
                EndTime = today.AddHours(12),
                Status = BookingStatus.Confirmed,
                CreatedAt = now.AddDays(-1),
                UpdatedAt = now.AddDays(-1)
            },
            new()
            {
                CabinId = 3,
                BookedByUserId = jordan.Id,
                Title = "Candidate Technical Interview",
                Purpose = BookingPurpose.Interview,
                StartTime = today.AddHours(14),
                EndTime = today.AddHours(15),
                Status = BookingStatus.Confirmed,
                CreatedAt = now,
                UpdatedAt = now
            },
            new()
            {
                CabinId = 1,
                BookedByUserId = priya.Id,
                Title = "Client Progress Review",
                Purpose = BookingPurpose.Meeting,
                StartTime = today.AddDays(1).AddHours(11),
                EndTime = today.AddDays(1).AddHours(12).AddMinutes(30),
                Status = BookingStatus.Confirmed,
                CreatedAt = now,
                UpdatedAt = now
            },
            new()
            {
                CabinId = 2,
                BookedByUserId = alex.Id,
                Title = "Design Jam & UX Walkthrough",
                Purpose = BookingPurpose.Other,
                PurposeDetail = "Design wireframes critique",
                StartTime = today.AddDays(1).AddHours(15),
                EndTime = today.AddDays(1).AddHours(16),
                Status = BookingStatus.Confirmed,
                CreatedAt = now,
                UpdatedAt = now
            }
        };

        db.Bookings.AddRange(demoBookings);
        await db.SaveChangesAsync();
    }
}
