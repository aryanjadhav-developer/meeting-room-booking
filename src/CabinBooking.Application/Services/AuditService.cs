using CabinBooking.Application.Abstractions;
using CabinBooking.Application.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CabinBooking.Application.Services;

public class AuditService(IAppDbContext db)
{
    public async Task<IReadOnlyList<AuditEntryDto>> GetEntriesAsync(int take = 50, int skip = 0, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 200);
        skip = Math.Max(0, skip);

        var entries = await db.AuditEntries.AsNoTracking().ToListAsync(ct);
        return entries
            .OrderByDescending(a => a.Timestamp)
            .Skip(skip)
            .Take(take)
            .Select(a => new AuditEntryDto(a.Id, a.UserId, a.UserName, a.Action, a.EntityType, a.EntityId, a.Details, a.Timestamp))
            .ToList();
    }

    public async Task<int> ClearAsync(CancellationToken ct = default)
    {
        var count = await db.AuditEntries.CountAsync(ct);
        await db.AuditEntries.ExecuteDeleteAsync(ct);
        return count;
    }
}
