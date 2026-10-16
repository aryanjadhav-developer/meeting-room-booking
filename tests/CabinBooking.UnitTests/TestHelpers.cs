using CabinBooking.Application.Abstractions;
using CabinBooking.Application.Dtos;
using CabinBooking.Application.Options;
using CabinBooking.Application.Services;
using CabinBooking.Domain.Entities;
using CabinBooking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CabinBooking.UnitTests;

public sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}

public sealed class TestUser : ICurrentUser
{
    public Guid UserId { get; set; }
    public bool IsAdmin { get; set; }
}

public sealed class RecordingNotifier : IBookingNotifier
{
    public List<string> Events { get; } = [];
    public Task BookingCreatedAsync(BookingDto booking) { Events.Add("created"); return Task.CompletedTask; }
    public Task BookingUpdatedAsync(BookingDto booking) { Events.Add("updated"); return Task.CompletedTask; }
    public Task BookingCancelledAsync(BookingDto booking) { Events.Add("cancelled"); return Task.CompletedTask; }
    public Task CabinsChangedAsync() { Events.Add("cabins"); return Task.CompletedTask; }
}

/// <summary>Wires a BookingService against an in-memory database. Office: Asia/Kolkata (UTC+05:30), 09:00–20:00.</summary>
public sealed class BookingFixture
{
    public static readonly TimeSpan Ist = TimeSpan.FromHours(5.5);

    /// <summary>Wednesday 14 Oct 2026, 10:00 IST.</summary>
    public static readonly DateTimeOffset DefaultNow = new(2026, 10, 14, 10, 0, 0, Ist);

    public FixedClock Clock { get; } = new(DefaultNow);
    public TestUser CurrentUser { get; } = new();
    public RecordingNotifier Notifier { get; } = new();
    public AppDbContext Db { get; }
    public BookingRuleValidator Rules { get; }
    public BookingService Service { get; }
    public User Alice { get; }
    public User Bob { get; }

    public BookingFixture(BookingRulesOptions? options = null)
    {
        Db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        Alice = new User { Name = "Alice", Email = "alice@example.com", PasswordHash = "x" };
        Bob = new User { Name = "Bob", Email = "bob@example.com", PasswordHash = "x" };
        Db.Users.AddRange(Alice, Bob);
        Db.Cabins.AddRange(
            new Cabin { Id = 1, Name = "Cabin 1", Capacity = 4 },
            new Cabin { Id = 2, Name = "Cabin 2", Capacity = 6 },
            new Cabin { Id = 3, Name = "Cabin 3", Capacity = 8, IsActive = false });
        Db.SaveChanges();

        CurrentUser.UserId = Alice.Id;
        Rules = new BookingRuleValidator(Microsoft.Extensions.Options.Options.Create(options ?? new BookingRulesOptions()), Clock);
        Service = new BookingService(Db, new SqlServerCabinLock(Db), CurrentUser, Notifier, Rules, NullLogger<BookingService>.Instance);
    }

    public void ActAs(User user, bool admin = false)
    {
        CurrentUser.UserId = user.Id;
        CurrentUser.IsAdmin = admin;
    }

    /// <summary>A local IST time on the default day (14 Oct 2026).</summary>
    public static DateTimeOffset At(int hour, int minute = 0, int dayOffset = 0) =>
        new DateTimeOffset(2026, 10, 14, hour, minute, 0, Ist).AddDays(dayOffset);

    public static CreateBookingRequest Request(int cabinId, DateTimeOffset start, DateTimeOffset end, string title = "Sync") =>
        new(cabinId, title, Domain.Enums.BookingPurpose.Meeting, null, start, end);
}
