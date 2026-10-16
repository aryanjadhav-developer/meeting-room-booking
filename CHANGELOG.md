# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.2.0] - 2026-10-16

### Added
- Serilog structured logging with console output and rolling file sink (`logs/cabinbooking-.log`).
- FluentValidation request validators for all auth, cabin, booking, and administrative settings endpoints.
- Automatic FluentValidation action filter populating ProblemDetails and ValidationProblemDetails.
- OpenAPI / Swagger XML comments documentation generation across API and Application layers.
- Frontend unit and component testing suite using Vitest, React Testing Library, and jsdom (19 passing tests).
- Code coverage collection with Coverlet across unit and integration test assemblies.
- Render.com Blueprint (`render.yaml`) for 1-click cloud deployment on the free tier.
- In-app demo email outbox viewer (`/api/auth/preview-emails` dialog in Navbar) for viewing sent reset links and calendar invites.
- Quick 1-click demo login buttons ("Try as Admin", "Try as Employee") on the login page.
- 5 Architecture Decision Records (ADRs) covering concurrency locks, direct EF Core, SignalR, SQLite, and token rotation.
- Comprehensive technical documentation (`docs/architecture.md`, `docs/api.md`, `docs/deployment.md`, `CONTRIBUTING.md`).

## [1.1.0] - 2026-10-16

### Added
- ASP.NET Core Identity authentication upgrade with single-use refresh token rotation in HttpOnly secure cookies.
- Brute-force protection: 5 consecutive failed login attempts trigger an automatic 15-minute account lockout.
- Password recovery via MailKit e-mail with cryptographically secure tokens.
- Frontend authentication pages: `ForgotPasswordPage.tsx`, `ResetPasswordPage.tsx`, and automatic 401 refresh interceptor.
- Built-in fixed-window rate limiter for `/api/auth/*` endpoints.
- Strict security headers (CSP, HSTS, X-Content-Type-Options, X-Frame-Options, Referrer-Policy).
- Immutable audit log system (`AuditEntry` entity, `IAuditLogger`, admin audit viewer tab).
- Asynchronous RFC 5545 `.ics` calendar invitation generation and email dispatch via background `Channel<T>` worker.
- Database-backed dynamic system settings (`SystemSetting` entity, `SettingsService`, admin settings tab).
- CSV export endpoints and UI download buttons for booking records and cabin utilization reports.
- 22 new integration tests (140 tests total, 100% green).

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
