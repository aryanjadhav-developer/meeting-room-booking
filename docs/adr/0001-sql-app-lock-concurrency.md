# ADR 0001: SQL Application Locks for Concurrency Control

## Status
Accepted

## Context
In meeting room and cabin reservation systems, two users concurrently booking the same room for overlapping time intervals is the classic double-booking race condition. Traditional optimistic concurrency (e.g., row versioning) only detects conflicts when updating an existing row; it does not protect when two concurrent transactions insert two distinct rows that overlap in time.

Serializable isolation level prevents phantom reads, but incurs heavy lock contention, frequent deadlocks, and severe performance penalties across the entire database table.

## Decision
We implemented per-room advisory application locks using SQL Server's `sp_getapplock` (and an equivalent per-resource lock abstraction `ICabinLock` with SQLite write serialization):
- When creating, updating, checking in, or extending a booking for room `Cabin:{id}`, the transaction acquires an exclusive application lock:
  `sp_getapplock @Resource = 'Cabin:1', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 5000`
- Within the locked critical section, the service queries existing overlapping active bookings and verifies business policy rules.
- If overlapping bookings exist, the transaction aborts and returns an HTTP 409 Conflict with the clash details.
- Once the insert/update is committed, the application lock is automatically released with transaction commit/rollback.

## Consequences
### Positive
- **Zero double bookings:** Concurrency safety is mathematically guaranteed at the database engine level.
- **Minimal lock footprint:** Locking is scoped strictly to the specific cabin being booked (`Cabin:{id}`), allowing independent bookings across other rooms to execute in full parallel.
- **Zero deadlocks:** Standardized resource key ordering prevents cyclic locking dependency.

### Negative
- Requires database provider support: SQL Server natively supports `sp_getapplock`. For SQLite in demo/development mode, we implemented a coordinated semaphore-backed lock (`SqliteCabinLock`).
