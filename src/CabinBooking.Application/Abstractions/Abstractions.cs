using CabinBooking.Application.Dtos;
using CabinBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CabinBooking.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Cabin> Cabins { get; }
    DbSet<Booking> Bookings { get; }
    DbSet<AuditEntry> AuditEntries { get; }
    DbSet<SystemSetting> Settings { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Serialises writes per cabin so two simultaneous requests can never both pass the overlap check.
/// The action runs inside a database transaction that is committed when it returns.
/// </summary>
public interface ICabinLock
{
    Task<T> RunExclusiveAsync<T>(IEnumerable<int> cabinIds, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken);
}

public interface ICurrentUser
{
    Guid UserId { get; }
    bool IsAdmin { get; }
}

/// <summary>Pushes booking changes to connected clients (implemented with SignalR in the API).</summary>
public interface IBookingNotifier
{
    Task BookingCreatedAsync(BookingDto booking);
    Task BookingUpdatedAsync(BookingDto booking);
    Task BookingCancelledAsync(BookingDto booking);
    Task CabinsChangedAsync();
}

public interface IPasswordService
{
    string Hash(User user, string password);
    bool Verify(User user, string password);
}

public interface ITokenService
{
    (string Token, DateTimeOffset ExpiresAt) CreateToken(User user);
}

public interface IEmailSender
{
    Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default);
    Task SendEmailWithAttachmentAsync(string toEmail, string subject, string body, string attachmentContent, string attachmentFileName, string contentType, CancellationToken cancellationToken = default);
}

public interface IAuditLogger
{
    Task LogAsync(string action, string entityType, string entityId, string details, CancellationToken cancellationToken = default);
}

public record EmailNotification(string ToEmail, string Subject, string Body, string? IcsAttachmentContent = null, string? IcsAttachmentFileName = null);

public interface IEmailOutbox
{
    ValueTask QueueEmailAsync(EmailNotification notification);
}
