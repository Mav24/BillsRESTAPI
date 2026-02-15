# Bills REST API

A simple REST API for managing bills, built with ASP.NET Core Minimal APIs and Entity Framework Core with SQLite.

This API is designed to work with the MonthlyBillCalculator application.

## Getting Started

### Prerequisites

- .NET 10 SDK or later

### Running the API

```bash
cd src/BillsApi
dotnet run
```

The API will start on `http://localhost:5202` by default.

### Running Tests

```bash
dotnet test
```

## API Endpoints

### Authentication Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/auth/register` | POST | Register a new user |
| `/auth/login` | POST | Login and get JWT + Refresh token |
| `/auth/refresh` | POST | Get new access token using refresh token |
| `/auth/logout` | POST | Revoke refresh token |
| `/auth/forgot-password` | POST | Send password reset email |
| `/auth/reset-password` | POST | Reset password with token |
| `/auth/email` | PATCH | Update user's email address (requires auth) |
| `/auth/account` | DELETE | Delete user account and all associated data (requires auth) |

### Bill Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/bills` | GET | Get all bills (requires auth) |
| `/bills/{id}` | GET | Get a specific bill by ID (requires auth) |
| `/bills` | POST | Create a new bill (requires auth) |
| `/bills/{id}` | PUT | Update an existing bill (requires auth) |
| `/bills/{id}` | DELETE | Delete a bill (requires auth) |

## Data Model

### Bill

```json
{
  "id": 1,
  "billName": "Electricity",
  "amount": 125.50,
  "date": "2024-01-15T00:00:00",
  "amountOverMinimum": 20.50
}
```

| Field | Type | Description |
|-------|------|-------------|
| `id` | integer | Unique identifier (auto-generated) |
| `billName` | string | Name/description of the bill |
| `amount` | decimal | The full bill amount |
| `date` | datetime | Date of the bill (ISO 8601 format) |
| `amountOverMinimum` | decimal | Amount over the minimum threshold |

## Example Requests

### Authentication Examples

#### Register a User

```bash
curl -X POST http://localhost:5202/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "username": "testuser",
    "email": "test@example.com",
    "password": "password123"
  }'
```

#### Login

```bash
curl -X POST http://localhost:5202/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "username": "testuser",
    "password": "password123"
  }'
```

Response:
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "abc123...",
  "expiresIn": 900
}
```

#### Update Email

```bash
curl -X PATCH http://localhost:5202/auth/email \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  -d '{
    "email": "newemail@example.com",
    "currentPassword": "password123"
  }'
```

Response:
```json
{
  "message": "Email updated successfully",
  "email": "newemail@example.com"
}
```

### Bill Examples

#### Get All Bills

```bash
curl http://localhost:5202/bills \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"
```

#### Create a Bill

```bash
curl -X POST http://localhost:5202/bills \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  -d '{
    "billName": "Electricity",
    "amount": 125.50,
    "date": "2024-01-15T00:00:00",
    "amountOverMinimum": 20.50
  }'
```

#### Get a Bill by ID

```bash
curl http://localhost:5202/bills/1 \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"
```

#### Update a Bill

```bash
curl -X PUT http://localhost:5202/bills/1 \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN" \
  -d '{
    "billName": "Electricity",
    "amount": 135.50,
    "date": "2024-01-15T00:00:00",
    "amountOverMinimum": 30.50
  }'
```

### Delete a Bill

```bash
curl -X DELETE http://localhost:5202/bills/1
```

## Configuration

### Database

The API uses SQLite by default. The database file (`bills.db`) is created automatically in the application directory.

To change the database connection, modify `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=bills.db"
  }
}
```

### CORS

CORS is configured to allow all origins by default for development. For production, update the CORS configuration in `Program.cs`:

```csharp
app.UseCors(policy => policy
    .WithOrigins("https://your-app-domain.com")
    .WithMethods("GET", "POST", "PUT", "DELETE")
    .WithHeaders("Content-Type", "Accept"));
```

## Project Structure

```
BillsRESTAPI/
├── src/
│   └── BillsApi/
│       ├── Data/
│       │   └── BillsDbContext.cs    # EF Core DbContext
│       ├── Models/
│       │   ├── Bill.cs              # Bill entity model
│       │   └── BillInput.cs         # Input DTO for creating/updating
│       ├── Properties/
│       │   └── launchSettings.json
│       ├── Program.cs               # API endpoints and configuration
│       ├── appsettings.json
│       └── BillsApi.csproj
├── tests/
│   └── BillsApi.Tests/
│       ├── BillsApiTests.cs         # Integration tests
│       └── BillsApi.Tests.csproj
├── API_SPECIFICATION.md             # Original specification
├── BillsApi.sln
└── README.md
```

## OpenAPI Documentation

OpenAPI documentation is available at `/openapi/v1.json` when running in development mode.

## License

This project is provided as-is for educational purposes.
