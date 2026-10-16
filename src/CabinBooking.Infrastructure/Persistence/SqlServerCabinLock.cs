using System.Data;
using CabinBooking.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace CabinBooking.Infrastructure.Persistence;

/// <summary>
/// Uses SQL Server application locks (sp_getapplock) scoped to a transaction, one per cabin.
/// Two concurrent bookings for the same cabin are queued instead of both passing the overlap check.
/// </summary>
public class SqlServerCabinLock(AppDbContext db) : ICabinLock
{
    private const int LockTimeoutMs = 10_000;

    public async Task<T> RunExclusiveAsync<T>(IEnumerable<int> cabinIds, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        if (!db.Database.IsRelational())
            return await action(cancellationToken); // e.g. in-memory provider in tests

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        // Always lock in ascending order so moving a booking between cabins cannot deadlock.
        foreach (var id in cabinIds.Distinct().Order())
        {
            var resource = $"cabin-booking:{id}";
            var result = await db.Database
                .SqlQuery<int>($"""
                    DECLARE @r int;
                    EXEC @r = sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = {LockTimeoutMs};
                    SELECT @r AS [Value];
                    """)
                .ToListAsync(cancellationToken);

            if (result.Count == 0 || result[0] < 0)
                throw new TimeoutException($"Could not acquire booking lock for cabin {id}. Please try again.");
        }

        var value = await action(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return value;
    }
}
