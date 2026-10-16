# ADR 0002: Direct EF Core Usage vs Generic Repository Pattern

## Status
Accepted

## Context
Many .NET applications wrap Entity Framework Core with generic `IRepository<T>` and `IUnitOfWork` interfaces. We evaluated whether to introduce a repository layer or use `AppDbContext` directly in application services.

## Decision
We chose to inject and use `AppDbContext` directly within application services (`BookingService`, `CabinService`, `UserService`, `ReportService`, `SettingsService`) without generic repository wrappers:
1. `DbContext` in EF Core is already an implementation of the Repository and Unit of Work patterns.
2. Wrapping EF Core in generic repositories often abstracts away powerful LINQ capabilities (`Include`, projections with `.Select()`, `AsNoTracking()`, execution strategies, batch operations).
3. Mocking generic repositories in tests leads to brittle tests that test mock setups rather than real database queries (e.g. EF translation bugs, null handling, SQL compatibility).
4. Our testing strategy relies on real database instances via **Testcontainers** for SQL Server and **in-memory SQLite** for unit tests, rendering repository mocks unnecessary.

## Consequences
### Positive
- **High query efficiency:** Services can project directly into DTOs via `.Select()`, minimizing memory footprint and network transfer.
- **Full LINQ fidelity:** Complex queries like utilization reporting and time slot overlap checks leverage database engine indexing without abstraction leaks.
- **Reduced boilerplate:** Eliminates hundreds of lines of pass-through repository interfaces and classes.

### Negative
- Application services have a direct dependency on Entity Framework Core abstractions (mitigated by placing domain entities in `CabinBooking.Domain`).
