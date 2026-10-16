# Meeting Room Booking

[![CI](https://github.com/aryanjadhav-developer/meeting-room-booking/actions/workflows/ci.yml/badge.svg)](https://github.com/aryanjadhav-developer/meeting-room-booking/actions/workflows/ci.yml)
[![CodeQL](https://github.com/aryanjadhav-developer/meeting-room-booking/actions/workflows/codeql.yml/badge.svg)](https://github.com/aryanjadhav-developer/meeting-room-booking/actions/workflows/codeql.yml)
![Tests](https://img.shields.io/badge/tests-118%20passing-brightgreen)
![.NET 9](https://img.shields.io/badge/.NET-9.0-blue)
![React 19](https://img.shields.io/badge/React-19-61dafb)
![TypeScript](https://img.shields.io/badge/TypeScript-6.0-blue)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Book meeting rooms and discussion cabins in real time. Features live synchronization, DB advisory locks to prevent double-booking, multi-provider database support (SQLite zero-setup & SQL Server), and automated CI/CD.

| Layer | Tech |
|---|---|
| Backend | ASP.NET Core 9 Web API, EF Core 9, SQL Server |
| Real-time | SignalR (`/hubs/bookings`) |
| Auth | Email + password, JWT bearer tokens (PBKDF2 password hashes) |
| Frontend | React 19 + TypeScript + Vite, MUI, TanStack Query, `@microsoft/signalr`, dayjs |
| Tests | xUnit (rules, overlap, extend/end/cancel, no-show release) |

## Run it locally

Prerequisites: .NET 9 SDK, Node 20+, SQL Server.

```bash
# 1. API (creates/migrates the CabinBookingDb database on startup)
cd src/CabinBooking.Api
dotnet run --launch-profile http          # http://localhost:5080  (Swagger: /swagger)

# 2. Frontend with hot reload (proxies /api and /hubs to :5080)
cd client
npm install
npm run dev                               # http://localhost:5173
```

**An initial Admin account is seeded from configuration** (`Admin:Email`, `Admin:Password`). New registrations create Employee accounts, and admins can promote them from **Admin → Users**.

### Single-server deployment
`npm run build:api` builds the React app into `src/CabinBooking.Api/wwwroot`. The API then serves the UI, REST endpoints and SignalR from one port, so you only need to deploy one app.

```bash
cd client && npm run build:api
cd ../src/CabinBooking.Api && dotnet publish -c Release -o ../../publish
```

In production, set these through environment variables or user-secrets instead of `appsettings.Development.json`:
`ConnectionStrings__CabinBooking`, `Jwt__SigningKey` (32+ random characters).

### Run as a Windows service (starts with Windows, no terminal needed)
Scripts are in `deploy/`. Run the ones marked *admin* in PowerShell opened with **Run as administrator**:

| Script | What it does |
|---|---|
| `publish.ps1` | Builds the web app + server into `publish/` and creates `publish/appsettings.Production.json` (DB connection, JWT key, `http://0.0.0.0:5080`) on first run |
| `install-service.ps1` *(admin)* | Installs and starts the `CabinBooking` service (auto start, restart on crash) and opens port 5080 in the firewall |
| `update.ps1` *(admin)* | After code changes: stop service → publish → start service (DB migrations apply on start) |
| `uninstall-service.ps1` *(admin)* | Removes the service and firewall rule (database untouched) |

```powershell
powershell -ExecutionPolicy Bypass -File ".\deploy\install-service.ps1"
```
Service logs: Event Viewer → Windows Logs → Application (source `CabinBooking`). Swagger is disabled in the published (Production) build.

## Configuration (`appsettings.json`)

| Setting | Default | Meaning |
|---|---|---|
| `Admin:Email` / `Password` | `""` / `""` | Initial administrator credentials seeded at startup if no admin exists |
| `BookingRules:TimeZoneId` | `Asia/Kolkata` | Office time zone (IANA). All times are shown in this zone |
| `BookingRules:OfficeStart` / `OfficeEnd` | `09:00` / `20:00` | Bookable hours |
| `BookingRules:WorkingDays` | Mon–Sat | Days on which cabins can be booked |
| `BookingRules:Min/MaxDurationMinutes` | 15 / 240 | Booking length limits |
| `BookingRules:MaxDaysAhead` | 30 | How far ahead people can book |
| `BookingRules:MaxBookingsPerUserPerDay` | 0 (unlimited) | Anti-hogging limit (admins are exempt) |
| `BookingRules:AutoReleaseEnabled` | `false` | Release bookings nobody checked in to… |
| `BookingRules:CheckInGraceMinutes` | 10 | …this many minutes after the start |
| `Auth:AllowedEmailDomains` | `[]` | e.g. `["example.com"]` to restrict who can register |

## Architecture

```
client (React) ──REST /api──▶ CabinBooking.Api ──▶ Application (rules, services) ──▶ Infrastructure (EF Core / SQL Server)
       ▲                            │
       └────── SignalR /hubs ◀──────┘  BookingCreated / BookingUpdated / BookingCancelled / CabinsChanged
```

```
src/
  CabinBooking.Domain/          Entities (Cabin, User, Booking) and enums. No dependencies
  CabinBooking.Application/     BookingService, CabinService, AuthService, ReportService, BookingRuleValidator, DTOs
  CabinBooking.Infrastructure/  AppDbContext, migrations, SqlServerCabinLock, password hashing
  CabinBooking.Api/             Controllers, SignalR hub + notifier, JWT, no-show background job, ProblemDetails errors
tests/CabinBooking.UnitTests/   xUnit tests
client/                         React SPA
```

### How double-booking is prevented
Two bookings overlap when `newStart < existingEnd AND newEnd > existingStart`. Back-to-back bookings (one ends at 12:00, the next starts at 12:00) are allowed.
Each write takes a per-cabin SQL Server application lock (`sp_getapplock`) inside a transaction, then checks for overlaps and inserts. Simultaneous requests for the same cabin are queued, so exactly one wins and the others get **409 Conflict** with a message like *"Cabin 2 is already booked by Alex from 11:00 to 12:00 (Sprint planning)"*. This was verified with 6 parallel requests against the real database: 1 × 201 and 5 × 409.

### Real-time flow
1. A client calls the REST API, for example `POST /api/bookings`.
2. The service commits to the database, then broadcasts through `IBookingNotifier`, which is implemented with SignalR.
3. Every connected browser invalidates its TanStack Query cache and re-renders: the live board, the timeline and My bookings. Other people's actions also show a toast.
4. After a reconnect the client refetches, so nothing is missed. The header chip shows the connection state: Live, Reconnecting or Offline.

## API

| Method | Route | Notes |
|---|---|---|
| POST | `/api/auth/register`, `/api/auth/login` | Returns a JWT |
| GET | `/api/auth/me` | |
| GET | `/api/config/rules` | Office hours and limits for the UI |
| GET | `/api/cabins` · `/api/cabins/status` · `/api/cabins/{id}/availability?date=` | Live status = occupied now, current and next booking |
| POST/PUT | `/api/cabins[/{id}]` | Admin |
| GET | `/api/bookings?from=&to=&cabinId=` · `/api/bookings/mine?includePast=` | |
| POST | `/api/bookings` · `/api/bookings/quick` | Create, or book now for N minutes |
| PUT | `/api/bookings/{id}` | Edit or reschedule (upcoming only) |
| POST | `/api/bookings/{id}/extend` · `/end` · `/check-in` | |
| DELETE | `/api/bookings/{id}` | Cancel (soft, frees the slot) |
| GET | `/api/users` · PUT `/api/users/{id}` | Admin: roles and deactivation |
| GET | `/api/reports/utilization?from=&to=` | Admin: utilisation %, busiest hours, top bookers |
| GET | `/health` | Health check |

## Roadmap ideas
- Microsoft 365 / Entra ID single sign-on and Outlook calendar sync (Microsoft Graph)
- E-mail or Teams reminders 10 minutes before a meeting
- Recurring bookings (for example, daily stand-up)
- Kiosk mode: a tablet outside each cabin showing live status with tap-to-check-in
