using CabinBooking.Application.Abstractions;
using CabinBooking.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CabinBooking.Infrastructure.Audit;

public class AuditLogger(IAppDbContext db, ICurrentUser currentUser) : IAuditLogger
{
    public async Task LogAsync(string action, string entityType, string entityId, string details, CancellationToken cancellationToken = default)
    {
        try
        {
            Guid? userId = null;
            string userName = "System";

            try
            {
                if (currentUser.UserId != Guid.Empty)
                {
                    userId = currentUser.UserId;
                    var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId.Value, cancellationToken);
                    if (user != null) userName = user.Name;
                }
            }
            catch
            {
                // Unauthenticated or background context
            }

            var entry = new AuditEntry
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                UserName = userName,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Details = details,
                Timestamp = DateTimeOffset.UtcNow
            };

            db.AuditEntries.Add(entry);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Audit failure must never fail the underlying user transaction
        }
    }
}
