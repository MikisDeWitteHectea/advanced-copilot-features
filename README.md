# Event Registration System

A comprehensive event management and registration system built with ASP.NET Core and Blazor.

## Overview

This application provides a complete solution for managing events, ticket types, registrations, and attendee data. It includes features for capacity management, waitlists, discount codes, cancellation policies, and data export.

## Projects

- **EventRegistration.API** - ASP.NET Core Web API backend
- **EventRegistration.UI** - Blazor Server web application
- **EventRegistration.API.Tests** - Integration test suite

## Features

- Event creation and management
- Multiple ticket types per event
- Registration workflow with validation
- Capacity management and tracking
- Waitlist functionality for sold-out events
- Discount code system (percentage and fixed amount)
- Flexible cancellation policies with refund calculations
- Export attendee data to various formats
- Comprehensive integration tests

## Getting Started

### Prerequisites

- .NET 9.0 SDK
- SQL Server (or SQL Server LocalDB)

### Running the Application

1. Clone the repository
2. Navigate to the EventRegistration.API folder
3. Update the connection string in appsettings.json if needed
4. Run the database migrations:
   ```bash
   dotnet ef database update
   ```
5. Start the API:
   ```bash
   dotnet run
   ```
6. In a separate terminal, navigate to EventRegistration.UI
7. Start the UI:
   ```bash
   dotnet run
   ```

### Running Tests

```bash
cd EventRegistration.API.Tests
dotnet test
```

## API Documentation

When running in development mode, Swagger UI is available at `https://localhost:7231/swagger`

## Technology Stack

- ASP.NET Core 9.0
- Entity Framework Core
- Blazor Server
- xUnit for testing
- SQL Server

## License

This project is for educational purposes.
