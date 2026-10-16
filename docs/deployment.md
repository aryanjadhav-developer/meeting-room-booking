# Deployment & Hosting Guide

This application is containerized with Docker and can be hosted anywhere from a single Windows/Linux server to free cloud platforms like Render.

## 1. Zero-Setup Local Run (SQLite)

Prerequisites: [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

```bash
# Clone the repository
git clone https://github.com/aryanjadhav-developer/meeting-room-booking.git
cd meeting-room-booking

# Run with SQLite default (database created and seeded automatically)
dotnet run --project src/CabinBooking.Api
```
The API and client will be available at `http://localhost:5080`.

## 2. Docker Compose (Full Stack with SQL Server & Mailpit)

Prerequisites: [Docker Desktop](https://www.docker.com/)

```bash
docker compose up --build
```
Services started:
- **Web Application & API:** `http://localhost:8080`
- **SQL Server 2022:** `localhost:1433`
- **Mailpit Web UI (Email Testing):** `http://localhost:8025`

## 3. Render Free Web Service

1. Create a free account at [Render.com](https://render.com).
2. Connect your GitHub repository.
3. Choose **Blueprint** deployment: Render will automatically detect [`render.yaml`](file:///C:/Users/Zingworks/Documents/CabinBooking/render.yaml).
4. Environment variables configured automatically:
   - `Database__Provider=Sqlite`
   - `Demo__Enabled=true`
   - `RateLimiting__Enabled=true`
   - `ASPNETCORE_URLS=http://0.0.0.0:10000`
5. Click **Apply**: Render builds the Docker image and serves the application over automatic HTTPS.

> **Note on Free Tier:** Free instances spin down after 15 minutes of inactivity. When a new request arrives, the service wakes up in ~30–50 seconds.
