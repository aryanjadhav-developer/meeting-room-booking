# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-10-16

### Added
- Real-time meeting room booking web application with ASP.NET Core 9 and React 19.
- SignalR broadcasting for live updates (`BookingCreated`, `BookingUpdated`, `BookingCancelled`, `CabinsChanged`).
- Advisory database locks per cabin for strict concurrency and double-booking prevention.
- Multi-provider database support: SQLite for instant zero-setup execution, SQL Server for production workloads.
- Process-wide SemaphoreSlim cabin locking for SQLite (`SqliteCabinLock`).
- Configuration-based initial administrator seeding (`AdminOptions`).
- Testcontainers integration for SQL Server integration testing in containerized environments.
- Multi-stage Dockerfile and docker-compose setup (API, SQL Server, Mailpit).
- Automated CI pipeline with GitHub Actions, CodeQL static analysis, and Dependabot.
- Full test suite with 118 automated tests (28 unit tests, 90 integration tests).
