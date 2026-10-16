# Implementation Plan — Meeting Room Booking (Portfolio Project)

> **For the AI/dev agent implementing this (e.g. Antigravity):** read this whole file first.
> It describes an existing, working app and the steps to turn it into a polished, free,
> public GitHub portfolio project. Everything is **free** (no credit card). This is a
> **personal project** — there is **no company involved**. Do not add any company name,
> employer, office, colleague, or "used at X" references anywhere.

---

## 0. Context — what already exists

A full-stack meeting-room booking web app, already built and working.

**Repo root:** `C:\Users\Zingworks\Documents\CabinBooking\`
(The folder is named `CabinBooking` and namespaces are `CabinBooking.*`. The *GitHub repo*
will be named `meeting-room-booking`. Keep the code namespaces as `CabinBooking` to avoid
massive churn — repo name and namespace do not need to match. Renaming namespaces is optional
and low priority; if done, do it as its own commit with a global find/replace + test run.)

### Tech stack (current)
- **Backend:** .NET 9, ASP.NET Core Web API (C#), Clean Architecture
- **Real-time:** SignalR (`/hubs/bookings`)
- **ORM/DB:** EF Core 9 + SQL Server (migrations in Infrastructure)
- **Auth:** custom JWT (`JwtTokenService`) + password hashing via `PasswordHasher<User>`
  (from `Microsoft.Extensions.Identity.Core`). NOTE: this is **not** full ASP.NET Core Identity yet.
- **Frontend:** React 19 + TypeScript + Vite, MUI, TanStack Query, `@microsoft/signalr`, dayjs
- **Tests:** xUnit — **28 unit + 90 integration = 118 passing**. Integration tests run against a
  real SQL Server test DB (`CabinBookingDb_Tests`) via a `WebApplicationFactory`.

### Solution layout
```
CabinBooking.sln
src/
  CabinBooking.Domain/            # Entities/, Enums/  — no dependencies
    Entities/        User.cs, Cabin.cs, Booking.cs
    Enums/           UserRole.cs, BookingPurpose.cs, BookingStatus.cs (also BookingPhase)
  CabinBooking.Application/       # business logic, no infra
    Services/        BookingService.cs, CabinService.cs, AuthService.cs, UserService.cs,
                     ReportService.cs, BookingRuleValidator.cs
    Dtos/            Dtos.cs, Mapping.cs
    Options/         BookingRulesOptions.cs, AuthOptions.cs
    Abstractions/    Abstractions.cs (IAppDbContext, ICabinLock, ICurrentUser,
                     IBookingNotifier, IPasswordService, ITokenService)
    Common/          AppExceptions.cs
    DependencyInjection.cs
  CabinBooking.Infrastructure/
    Persistence/     AppDbContext.cs, SqlServerCabinLock.cs, Migrations/
    Security/        PasswordService.cs
    DependencyInjection.cs
  CabinBooking.Api/
    Controllers/     AuthController, CabinsController, BookingsController, AdminControllers
    Auth/            JwtTokenService.cs, HttpCurrentUser.cs, AccountStatusCheck.cs
    Realtime/        BookingHub.cs (+ SignalRBookingNotifier)
    Background/      NoShowReleaseService.cs
    Errors/          AppExceptionHandler.cs
    Program.cs, appsettings.json, appsettings.Development.json, wwwroot/ (built client)
tests/
  CabinBooking.UnitTests/
  CabinBooking.IntegrationTests/   # Infrastructure/TestApp.cs + *Tests.cs
client/                            # React app
  src/api/ (client.ts, types.ts)  src/auth/  src/components/  src/pages/
  src/hooks/  src/realtime/  src/utils/
deploy/                            # publish.ps1, install-service.ps1, update.ps1, uninstall-service.ps1
docs/                              # this file; PDF overview lives in "../Cabin Booking Document"
```

### Domain model (do not break)
- **User**: Id (Guid), Name, Email (unique), PasswordHash, Role (`Employee`/`Admin`), IsActive, CreatedAt
- **Cabin** (a room): Id (int), Name (unique), Description, Capacity, **CapacityLabel** (e.g. "9+"),
  Amenities (comma string), Color, IsActive
- **Booking**: Id (Guid), CabinId, BookedByUserId, **Title** (= project name), Purpose (enum),
  **PurposeDetail** (required when Purpose=Other), Notes, StartTime/EndTime (UTC),
  Status (`Confirmed`/`Cancelled`/`NoShow`), CheckedInAt, CreatedAt/UpdatedAt/CancelledAt, RowVersion.
  Phase (Upcoming/InProgress/Completed/Cancelled/NoShow) is **computed from the clock**, not stored.

### Key behaviours already implemented (keep them green)
- No double-booking: overlap = `newStart < existingEnd && newEnd > existingStart`; back-to-back allowed.
  Enforced with a per-cabin SQL Server **application lock** (`sp_getapplock`) via `ICabinLock`.
- Real-time: after any change, `IBookingNotifier` broadcasts `BookingCreated/Updated/Cancelled/CabinsChanged`.
- Booking rules in `BookingRulesOptions` (office hours, working days, min/max duration, max days ahead,
  projects list, check-in window, no-show auto-release, per-user daily limit).
- Instant deactivation + role changes via `AccountStatusCheck` (re-checks account on every request).
- Project (Title) and Purpose are **compulsory**; Purpose=Other requires PurposeDetail.

---

## 1. Golden rules for the implementer
1. **Never break the 118 existing tests.** Run `dotnet test` after every phase; keep it green.
2. **No company/employer/office references** anywhere (README, code, comments, demo data, commits).
   This is a personal project. Demo people are fictional (e.g. "Alex Carter", "Priya Shah").
3. **No secrets in git, ever.** Scrub before the first commit (see Phase 1).
4. **Backwards-compatible data.** Existing password hashes and schema must keep working.
5. **Small, logical commits** via feature branches + PRs. Conventional Commits style
   (`feat:`, `fix:`, `test:`, `docs:`, `chore:`). No fake/back-dated history.
6. **Free only.** No paid services, no credit-card signups.

---

## 2. Decisions already made
| Item | Value |
|---|---|
| GitHub repo name | `meeting-room-booking` (public) |
| Licence | MIT |
| Code namespaces | stay `CabinBooking.*` (repo name may differ) |
| Primary DB | SQL Server (local dev + full feature set) |
| Zero-setup + demo DB | **SQLite** (so `dotnet run` and the live demo need no external DB) |
| Live demo host | **Render.com** free web service (Docker), HTTPS included |
| Email (local) | **Mailpit** (fake inbox, via Docker) |
| Email (demo) | in-app "sent mail" preview; no paid SMTP |
| CI | GitHub Actions (free, unlimited for public repos) |

---

## PHASE 1 — Clean, safe repository  *(start here)*

**Goal:** a public-safe repo with no secrets, consistent formatting, honest first commits.

### Tasks & locations
1. **Secrets out of source.**
   - Move the connection string + `Jwt:SigningKey` out of
     `src/CabinBooking.Api/appsettings.Development.json`.
   - Use **.NET user-secrets** for local dev:
     `dotnet user-secrets init` (in CabinBooking.Api), then set
     `ConnectionStrings:CabinBooking` and `Jwt:SigningKey`.
   - `appsettings.json` keeps **empty placeholders** only (as it already does).
   - **Delete** `appsettings.Development.json` from the repo and add it to `.gitignore`
     (it already is ignored — verify). Add `appsettings.Development.example.json` with placeholders.
2. **Scrub identifiers.** Grep the whole repo for and remove/replace:
   - `DESKTOP-687U7PH`, any `192.168.*`, the SQL login `Aryan`/`aryan`, any real names/emails.
   - Check: `README.md`, `deploy/*.ps1`, `appsettings*`, seed data in
     `src/CabinBooking.Infrastructure/Persistence/AppDbContext.cs`, test helpers.
   - Deploy scripts: make them generic (use `localhost`, a placeholder DB name, env vars).
3. **Admin bootstrap (replace "first account = admin").** In `AuthService.RegisterAsync`,
   remove the "first user becomes Admin" logic. Instead seed an admin from config
   (`AdminOptions { Email, Password }`) at startup if no admin exists. Document it in README.
   Update the affected test (`F01_Registration.TC01`).
4. **Formatting/quality.**
   - Add `.editorconfig` at root (C# + TS rules).
   - Enable .NET analyzers; set `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in a
     `Directory.Build.props` at `src/`.
   - `client/`: add ESLint + Prettier configs; `npm run lint` script.
   - Run `dotnet format` and `npx prettier --write`.
5. **Repo files:** `LICENSE` (MIT, your name), `.gitattributes` (`* text=auto eol=lf`),
   verify `.gitignore` covers `bin/ obj/ node_modules/ publish/ backups/ appsettings.Development.json
   src/CabinBooking.Api/wwwroot/`.
6. **First commits & push.** `git init` (if not already), commit in logical chunks, add remote,
   push to the empty `meeting-room-booking` repo.

**Secret scan before pushing:** run `git grep -iE "password|secret|192\.168|DESKTOP-|BEGIN RSA"`
and a tool like `gitleaks detect` (free). Must be clean.

**Done when:** fresh clone builds; secret scan clean; `dotnet test` green (118).

---

## PHASE 2 — One-command run (SQLite + Docker)

**Goal:** anyone can run it instantly; tests run anywhere.

### Tasks & locations
1. **Multi-provider database.** In `src/CabinBooking.Infrastructure/DependencyInjection.cs`,
   choose the provider from config `Database:Provider` = `SqlServer` | `Sqlite`.
   - Add package `Microsoft.EntityFrameworkCore.Sqlite`.
   - Keep SQL Server migrations in `Persistence/Migrations/SqlServer/`; add a parallel SQLite
     migration set in `Persistence/Migrations/Sqlite/` (use `--output-dir` and a `--context`
     or provider-specific migrations assembly). Simpler alternative for the demo only:
     `db.Database.EnsureCreated()` under SQLite when `Database:Provider=Sqlite` and a
     `Demo:Enabled=true` flag — but prefer real migrations so it mirrors production.
2. **Cabin lock per provider.** `ICabinLock` already abstracts this.
   - Keep `SqlServerCabinLock` (`sp_getapplock`).
   - Add `SqliteCabinLock`: a process-wide `SemaphoreSlim` keyed by cabin id
     (SQLite is single-writer, so this is correct for the single-instance demo).
   - Register the right one based on `Database:Provider`.
   - (Optional future: `PostgresCabinLock` using `pg_advisory_xact_lock` — not needed for free hosting with SQLite.)
3. **Zero-setup dev.** With `Database:Provider=Sqlite` + a local file path, `dotnet run` must work
   with no external DB. Make SQLite the default in `appsettings.json` so cloning → running is trivial;
   SQL Server stays available via user-secrets/env for full-fidelity local dev.
4. **Demo seeding.** Add `Demo:Enabled` flag → on startup seed fictional users, rooms, and a week of
   bookings (idempotent). Put seeding in `Infrastructure/Persistence/DemoSeeder.cs`.
5. **Docker.**
   - `Dockerfile` (root): multi-stage — build React (`client`) → build .NET → copy client into
     `wwwroot` → runtime image. Final image runs the API; defaults to SQLite + Demo for the public image.
   - `docker-compose.yml` (root): services = `app` (build .), `sqlserver`
     (mcr.microsoft.com/mssql/server:2022), `mailpit` (axllent/mailpit). `app` uses SQL Server here.
   - `.dockerignore`.
6. **Testcontainers.** In `tests/CabinBooking.IntegrationTests`, replace the hard-coded SQL Server
   connection in `Infrastructure/TestApp.cs` with **Testcontainers for .NET**
   (`Testcontainers.MsSql`) so integration tests spin up their own SQL Server. Keep a fallback env var
   `CABINBOOKING_TEST_DB` to use an existing server. This makes tests run in CI without a local DB.

**Done when:** `git clone` → `dotnet run` works (SQLite); `docker compose up` gives the full stack
at `http://localhost:8080`; `dotnet test` passes with no pre-existing SQL Server (Testcontainers).

---

## PHASE 3 — CI/CD & GitHub workflow

**Goal:** green automated checks on every push; professional repo hygiene.

### Tasks & locations (`.github/`)
1. `.github/workflows/ci.yml`: on push/PR → restore, build (warnings as errors),
   `dotnet test` (Testcontainers needs Docker — use `ubuntu-latest` which has Docker),
   `cd client && npm ci && npm run lint && npm run build`.
2. `.github/workflows/codeql.yml` (C# + JS) + `.github/dependabot.yml` (nuget, npm, github-actions).
3. **Badges** in README: build status, test count, coverage, licence.
4. **Branch protection** on `main` (PRs only, CI required) — set in GitHub settings.
5. Templates: `.github/pull_request_template.md`, `.github/ISSUE_TEMPLATE/`.
6. Tag `v1.0.0`; create `CHANGELOG.md` (Keep a Changelog format).

**Done when:** CI is green on a PR; badge shows in README.

---

## PHASE 4 — Security & auth upgrade

**Goal:** production-grade authentication; matches a .NET backend profile.

### Tasks
1. **Adopt ASP.NET Core Identity** (`Microsoft.AspNetCore.Identity.EntityFrameworkCore`).
   - Make `User : IdentityUser<Guid>` (migrate carefully) **or** keep the custom `User` and use
     Identity's `UserManager`/`PasswordHasher` + stores. Existing hashes already come from
     `PasswordHasher<User>`, so they remain valid — verify with a migration test.
   - Keep JWT issuance; add Identity for lockout/reset/confirmation.
2. Account **lockout** (5 fails → 15 min).
3. **Forgot/reset password** by email: endpoints + React pages
   (`client/src/pages/ForgotPasswordPage.tsx`, `ResetPasswordPage.tsx`). Use **MailKit**;
   `IEmailSender` abstraction with a Mailpit/SMTP impl + a demo "preview" impl.
4. Optional **email confirmation** (config toggle).
5. **Refresh tokens** in httpOnly secure cookies; add `/api/auth/refresh` + real logout.
6. **Rate limiting** (built-in `AddRateLimiter`) on `/api/auth/*`.
7. **Security headers** middleware (CSP, HSTS, X-Content-Type-Options, X-Frame-Options).
8. ~15 tests: lockout, reset flow, refresh/expiry, rate limits.

**Done when:** new tests pass; a user created before this change can still log in.

---

## PHASE 5 — Real-world features
1. **Audit log** — `AuditEntry` entity + `IAuditLogger`; record create/update/cancel with actor+timestamp;
   admin screen `client/src/pages/admin` tab.
2. **Email + .ics calendar invite** on book/change/cancel, sent from a background queue
   (`IHostedService` + channel). Templates in `Infrastructure/Email/`.
3. **Admin-editable settings** — move projects/office-hours/holidays from `BookingRulesOptions` into a
   `Settings` table with an admin UI; keep config as defaults/fallback.
4. **CSV export** for bookings + reports (`text/csv` endpoints + download buttons).
5. *(optional)* **Recurring bookings** (daily/weekly) with per-occurrence clash handling.

Each = its own branch, tests, README update.

---

## PHASE 6 — Quality & observability
1. **Serilog** (console + file, request-id enrichment) — `Program.cs`.
2. **FluentValidation** — replace manual checks in `BookingRuleValidator`/services with validators.
3. **Swagger** descriptions/examples from XML doc comments (`<GenerateDocumentationFile>`).
4. **Frontend tests** — Vitest + React Testing Library: booking form validation, `utils/time.ts`,
   `utils/format.ts`, permission rendering.
5. **Playwright** e2e — `client/e2e/`: login → book → second context sees live update. Runs in CI.
6. **Coverage** — Coverlet; publish % + badge; target ≥80% backend.

---

## PHASE 7 — Free live demo (Render.com)
1. Deploy the Docker image to a **Render free web service**; set env:
   `Database__Provider=Sqlite`, `Demo__Enabled=true`, `Jwt__SigningKey` (Render env var),
   `ASPNETCORE_URLS=http://0.0.0.0:10000`. HTTPS is automatic.
2. Auto-deploy from GitHub `main` (Render dashboard). Secrets only in Render env, never in code.
3. **Demo mode:** login page buttons "Try as Admin" / "Try as Employee"; reseed on startup
   (SQLite resets when the instance sleeps — desirable); emails go to the in-app preview.
4. README: note the ~30s cold-start wake on the free tier.

**You (human) do:** create the Render account; connect GitHub. No card required.

**Done when:** public HTTPS URL works; live updates sync across two browsers.

---

## PHASE 8 — Documentation & presentation
1. **README.md** (rewrite): hero GIF (live update across two windows) → badges → live-demo link →
   features → screenshots (reuse `../Cabin Booking Document/source/images/`) → architecture diagram →
   tech stack → quick start (`dotnet run` and `docker compose up`) → testing → roadmap. **No company refs.**
2. **ADRs** in `docs/adr/` — short "why" notes: SQL app-lock for concurrency; EF Core used directly
   (not Repository/UoW) because DbContext already is UoW; SignalR choice; clean architecture; SQLite-for-demo.
3. `docs/` — architecture.md, api.md, deployment.md (+ keep the PDF).
4. `CONTRIBUTING.md` — run, test, PR flow.
5. **GitHub profile** (separate `aryanjadhav-developer` README repo): pin this project; add
   React, TypeScript, SignalR, Docker, GitHub Actions to the stack; make it the featured project.
6. CV/LinkedIn bullet (below).

---

## Timeline (part-time)
| Week | Phases | Outcome |
|---|---|---|
| 1 | 1–3 | Clean public repo, one-command run, green CI |
| 2 | 4, start 5 | Production-grade auth |
| 3 | 5, 6 | Audit log, email/invites, full test suite |
| 4 | 7, 8 | Live demo + polished README + profile update |

## Free toolchain (all ₹0, no card)
GitHub · GitHub Actions · CodeQL · Dependabot · Docker Desktop · SQLite · SQL Server Developer (local) ·
Testcontainers · Render.com · ASP.NET Identity · MailKit + Mailpit · Serilog · FluentValidation · Swagger ·
Vitest · React Testing Library · Playwright · Coverlet · xUnit · gitleaks.

## Command reference
```bash
# build & test
dotnet build
dotnet test
cd client && npm ci && npm run lint && npm run build

# run (zero-setup, SQLite)
dotnet run --project src/CabinBooking.Api     # Database:Provider=Sqlite default

# full stack locally
docker compose up

# secret scan
gitleaks detect --source .
```

## Resume / LinkedIn line (final, no company)
> Built a real-time meeting-room booking system (ASP.NET Core, SignalR, EF Core, SQL Server/SQLite,
> React + TypeScript): concurrency-safe booking via DB advisory locks, ASP.NET Identity auth,
> 130+ automated tests, CI/CD, Docker, and a free live demo.

## Risks & mitigations
| Risk | Mitigation |
|---|---|
| Secret leaks into git history | gitleaks before first push; rotate immediately if leaked (history rewrite alone is not enough) |
| Breaking the 118 tests | run `dotnet test` after every change; one feature per PR |
| Identity migration breaks old logins | add a test that an old-format hash still verifies before merging Phase 4 |
| Render cold starts | note in README; acceptable for a demo |
| Over-engineering | no microservices/K8s/CQRS; every addition has a clear "why" (record in ADRs) |

---
*This plan supersedes earlier drafts. Implement phases in order; keep tests green; keep it free; keep it personal (no company references).*
