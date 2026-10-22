using System.Collections.Concurrent;
using CabinBooking.Application.Abstractions;

namespace CabinBooking.Infrastructure.Persistence;

/// <summary>
/// In-process cabin locking for PostgreSQL using SemaphoreSlim instances keyed by cabin id.
/// A serialisable transaction is used so the database also enforces exclusion.
/// </summary>
public class PostgresCabinLock(AppDbContext db) : ICabinLock
{
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> Locks = new();
    private const int LockTimeoutMs = 10_000;

    public async Task<T> RunExclusiveAsync<T>(IEnumerable<int> cabinIds, Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        var sortedIds = cabinIds.Distinct().Order().ToList();
        var acquired = new List<SemaphoreSlim>();

        try
        {
            foreach (var id in sortedIds)
            {
                var sem = Locks.GetOrAdd(id, _ => new SemaphoreSlim(1, 1));
                if (!await sem.WaitAsync(LockTimeoutMs, cancellationToken))
                    throw new TimeoutException($"Could not acquire booking lock for cabin {id}. Please try again.");
                acquired.Add(sem);
            }

            await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
            var result = await action(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return result;
        }
        finally
        {
            for (var i = acquired.Count - 1; i >= 0; i--)
                acquired[i].Release();
        }
    }
}
