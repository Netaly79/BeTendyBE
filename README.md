# BeTendlyBE

BeTendlyBE is a backend API for booking services between clients and masters, built with ASP.NET Core and Entity Framework Core.

## Features

- User registration and authentication (JWT, refresh tokens)
- Master and client profiles
- Service management (CRUD for services)
- Booking creation, cancellation, and availability checks
- Email notifications (password reset, etc.)
- Validation and error handling with ProblemDetails
- Swagger/OpenAPI documentation

## Project Structure

- `Controllers/` — API endpoints (Auth, Booking, Profile, Services, etc.)
- `Services/` — Business logic (BookingService, MasterService, EmailService, etc.)
- `Data/` — Entity Framework Core DbContext and migrations
- `Domain/` — Entity models (User, Master, Service, Booking, etc.)
- `DTO/` — Data transfer objects for requests/responses
- `Contracts/` — Shared contracts and query objects
- `Helpers/` — Validation and utility classes
- `Infrastructure/` — Identity and other infrastructure helpers
- `wwwroot/` — Static files (e.g., avatars)

## Getting Started

1. **Configure the database:**
   - Set the connection string in `appsettings.json` or via environment variables.
2. **Run migrations:**
   ```sh
   dotnet ef database update
   ```