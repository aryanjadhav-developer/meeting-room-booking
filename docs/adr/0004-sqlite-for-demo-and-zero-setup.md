# ADR 0004: Dual-Provider Database Architecture (SQLite & SQL Server)

## Status
Accepted

## Context
Running full enterprise SQL Server requires either local SQL Server developer edition, local Docker engine, or external cloud infrastructure. For evaluation, code reviews, and free-tier cloud deployment (e.g., Render free web services), a zero-setup local storage engine is needed that requires no external dependencies.

## Decision
We implemented a dual-provider database architecture configured via `Database:Provider` (`Sqlite` or `SqlServer`):
1. **SQLite as Default:** `Database:Provider=Sqlite` creates a local `CabinBooking.db` file automatically on first launch with `EnsureCreatedAsync()` and runs `DemoSeeder` with sample rooms, users, and bookings.
2. **SQL Server for Production / Integration Tests:** `Database:Provider=SqlServer` uses full relational migrations (`MigrateAsync()`) and native `sp_getapplock` concurrency control.
3. **Lock Abstraction:** The `ICabinLock` interface switches between `SqlAppCabinLock` (SQL Server) and `SqliteCabinLock` (thread-safe asynchronous semaphores for SQLite).

## Consequences
### Positive
- **Instant onboarding:** Anyone can clone the repo and type `dotnet run` to run the application immediately with zero prerequisite installations.
- **₹0 Cost Cloud Demos:** Enables hosting on free cloud platforms (Render, Railway, Fly.io) without requiring paid managed database add-ons.
- **Preserved Production Fidelity:** Full SQL Server features remain 100% active when configured for production.

### Negative
- Schema design must remain compatible with both SQLite and SQL Server (e.g. avoiding provider-specific column types where standard SQL types suffice).
