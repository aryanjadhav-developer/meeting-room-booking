# API Reference Guide

The Cabin Booking API is built with ASP.NET Core 9 and provides OpenAPI / Swagger documentation accessible at `/swagger` when running in development mode.

## Authentication & Identity

| Endpoint | Method | Auth | Description |
|---|---|---|---|
| `/api/auth/register` | POST | Anonymous | Registers a new employee account (requires >= 8 character password) |
| `/api/auth/login` | POST | Anonymous | Authenticates credentials, returns JWT, and sets `cb_refresh_token` httpOnly cookie |
| `/api/auth/refresh` | POST | Anonymous | Rotates refresh token and returns a fresh JWT access token |
| `/api/auth/logout` | POST | Anonymous | Clears the refresh token cookie and database session |
| `/api/auth/forgot-password` | POST | Anonymous | Dispatches a password reset email with secure 1-hour token |
| `/api/auth/reset-password` | POST | Anonymous | Resets password using the received token |
| `/api/auth/preview-emails` | GET | Anonymous | Returns recent sent emails (for demo and local testing) |
| `/api/auth/me` | GET | Bearer | Returns the authenticated user's profile |

## Meeting Rooms & Cabins

| Endpoint | Method | Auth | Description |
|---|---|---|---|
| `/api/cabins` | GET | Anonymous | Lists active (or all) cabins with capacity, amenities, and colors |
| `/api/cabins/status` | GET | Anonymous | Returns live status for each cabin (Occupied, Current booking, Next booking) |
| `/api/cabins` | POST | Admin | Creates a new cabin |
| `/api/cabins/{id}` | PUT | Admin | Updates cabin details, capacity, or active status |

## Bookings

| Endpoint | Method | Auth | Description |
|---|---|---|---|
| `/api/bookings` | GET | Bearer | Queries bookings by date range and optional cabin ID filter |
| `/api/bookings/mine` | GET | Bearer | Returns the current user's bookings |
| `/api/bookings` | POST | Bearer | Creates a booking (protected by advisory lock & policy engine) |
| `/api/bookings/quick` | POST | Bearer | Instantly books the first available room slot |
| `/api/bookings/{id}` | PUT | Bearer | Modifies an existing booking title, time, or purpose |
| `/api/bookings/{id}/check-in` | POST | Bearer | Confirms physical check-in within the configured check-in window |
| `/api/bookings/{id}/extend` | POST | Bearer | Extends an ongoing booking if subsequent slots are unbooked |
| `/api/bookings/{id}/end` | POST | Bearer | Ends an ongoing meeting early, freeing the room immediately |
| `/api/bookings/{id}` | DELETE | Bearer | Cancels a booking |
| `/api/bookings/export/csv` | GET | Bearer | Exports booking records as CSV format (`text/csv`) |

## Administration & Settings

| Endpoint | Method | Auth | Description |
|---|---|---|---|
| `/api/config/rules` | GET | Anonymous | Returns active office hours, working days, and policy rules |
| `/api/config/rules` | PUT | Admin | Dynamically updates office hours, slot size, and advance limits |
| `/api/audit` | GET | Admin | Paged audit trail of all administrative and booking actions |
| `/api/reports/utilization` | GET | Admin | Utilization analytics, hourly load distribution, and top users |
| `/api/reports/utilization/csv` | GET | Admin | Exports utilization analytics as CSV format |
