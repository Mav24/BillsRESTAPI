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

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/bills` | GET | Get all bills |
| `/bills/{id}` | GET | Get a specific bill by ID |
| `/bills` | POST | Create a new bill |
| `/bills/{id}` | PUT | Update an existing bill |
| `/bills/{id}` | DELETE | Delete a bill |

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

### Get All Bills

```bash
curl http://localhost:5202/bills
```

### Create a Bill

```bash
curl -X POST http://localhost:5202/bills \
  -H "Content-Type: application/json" \
  -d '{
    "billName": "Electricity",
    "amount": 125.50,
    "date": "2024-01-15T00:00:00",
    "amountOverMinimum": 20.50
  }'
```

### Get a Bill by ID

```bash
curl http://localhost:5202/bills/1
```

### Update a Bill

```bash
curl -X PUT http://localhost:5202/bills/1 \
  -H "Content-Type: application/json" \
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
