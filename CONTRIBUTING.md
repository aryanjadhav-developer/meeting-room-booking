# Contributing to Meeting Room / Cabin Booking

Thank you for your interest in contributing!

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js 22 LTS](https://nodejs.org/) & npm
- [Docker Desktop](https://www.docker.com/) (optional, for Testcontainers and SQL Server integration tests)

## Getting Started

1. Fork and clone the repository:
   ```bash
   git clone https://github.com/aryanjadhav-developer/meeting-room-booking.git
   cd meeting-room-booking
   ```

2. Run the automated backend tests:
   ```bash
   dotnet test
   ```

3. Setup and verify the client:
   ```bash
   cd client
   npm install
   npm run lint
   npm test
   npm run build
   ```

## Development Guidelines

- **Clean Architecture:** Keep business rules in `CabinBooking.Application` and pure entities in `CabinBooking.Domain`.
- **Zero Secrets:** Never commit passwords, tokens, or private keys. Always run `gitleaks detect` before pushing.
- **Maintain Test Coverage:** Every new feature or bug fix must include corresponding unit and/or integration tests.
- **Pull Requests:** Open a PR targeting `main`. Ensure all GitHub Actions CI checks are green.
