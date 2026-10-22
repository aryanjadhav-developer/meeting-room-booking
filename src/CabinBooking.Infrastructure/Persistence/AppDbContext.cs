using CabinBooking.Application.Abstractions;
using CabinBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CabinBooking.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Cabin> Cabins => Set<Cabin>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<SystemSetting> Settings => Set<SystemSetting>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<RecurringBooking> RecurringBookings => Set<RecurringBooking>();
    public DbSet<RecurringBookingInstance> RecurringBookingInstances => Set<RecurringBookingInstance>();

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            foreach (var entry in ChangeTracker.Entries<Booking>())
            {
                if (entry.State is EntityState.Added or EntityState.Modified)
                {
                    entry.Entity.RowVersion = Guid.NewGuid().ToByteArray();
                }
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.RefreshToken).HasMaxLength(200);
            e.Property(x => x.PasswordResetToken).HasMaxLength(200);
        });

        modelBuilder.Entity<AuditEntry>(e =>
        {
            e.ToTable("AuditEntries");
            e.HasKey(x => x.Id);
            e.Property(x => x.UserName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Action).HasMaxLength(100).IsRequired();
            e.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
            e.Property(x => x.EntityId).HasMaxLength(100).IsRequired();
            e.Property(x => x.Details).HasMaxLength(2000);
            e.HasIndex(x => x.Timestamp);
        });

        modelBuilder.Entity<SystemSetting>(e =>
        {
            e.ToTable("Settings");
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(100);
            e.Property(x => x.Value).IsRequired();
        });

        modelBuilder.Entity<Project>(e =>
        {
            e.ToTable("Projects");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<ProjectMember>(e =>
        {
            e.ToTable("ProjectMembers");
            e.HasKey(x => new { x.ProjectId, x.UserId });
            e.HasOne(x => x.Project).WithMany(p => p.Members).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Cabin>(e =>
        {
            e.ToTable("Cabins");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(60).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Description).HasMaxLength(300);
            e.Property(x => x.Amenities).HasMaxLength(300);
            e.Property(x => x.Color).HasMaxLength(7).IsRequired();
            e.Property(x => x.CapacityLabel).HasMaxLength(20);

            e.HasData(
                new Cabin { Id = 1, Name = "Lunch Room", Description = "Large room for big groups", Capacity = 9, CapacityLabel = "9+", Amenities = "TV,Whiteboard", Color = "#1976d2" },
                new Cabin { Id = 2, Name = "Conference Room", Description = "Meeting room with display", Capacity = 5, CapacityLabel = "5+", Amenities = "TV,Whiteboard", Color = "#2e7d32" },
                new Cabin { Id = 3, Name = "Cabin 3", Description = "Small cabin for focused discussions", Capacity = 4, CapacityLabel = "3-4", Amenities = "", Color = "#ed6c02" });
        });

        modelBuilder.Entity<Booking>(e =>
        {
            e.ToTable("Bookings", t =>
            {
                // SQL Server / SQLite use [brackets]; PostgreSQL requires "double quotes".
                var sql = Database.ProviderName?.Contains("Npgsql") == true
                    ? "\"EndTime\" > \"StartTime\""
                    : "[EndTime] > [StartTime]";
                t.HasCheckConstraint("CK_Bookings_EndAfterStart", sql);
            });
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(120).IsRequired();
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.Property(x => x.Purpose).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.PurposeDetail).HasMaxLength(100);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            if (Database.IsSqlite())
                e.Property(x => x.RowVersion).IsRequired().ValueGeneratedNever();
            else
                e.Property(x => x.RowVersion).IsRowVersion();
            e.Ignore(x => x.Duration);

            e.HasOne(x => x.Cabin).WithMany(c => c.Bookings).HasForeignKey(x => x.CabinId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.BookedBy).WithMany(u => u.Bookings).HasForeignKey(x => x.BookedByUserId).OnDelete(DeleteBehavior.Restrict);

            // Serves the overlap check (cabin + time range) and the timeline queries.
            e.HasIndex(x => new { x.CabinId, x.Status, x.StartTime, x.EndTime });
            e.HasIndex(x => new { x.BookedByUserId, x.StartTime });
        });

        modelBuilder.Entity<RecurringBooking>(e =>
        {
            e.ToTable("RecurringBookings");
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(120).IsRequired();
            e.Property(x => x.Purpose).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.PurposeDetail).HasMaxLength(100);
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.Property(x => x.DaysOfWeekJson).HasMaxLength(100).IsRequired();
            e.Property(x => x.AttendeeEmailsJson).HasMaxLength(2000);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Cabin).WithMany().HasForeignKey(x => x.CabinId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.UserId, x.IsActive });
        });

        modelBuilder.Entity<RecurringBookingInstance>(e =>
        {
            e.ToTable("RecurringBookingInstances");
            e.HasKey(x => new { x.RecurringBookingId, x.Date });
            e.HasOne(x => x.RecurringBooking).WithMany(r => r.Instances).HasForeignKey(x => x.RecurringBookingId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
