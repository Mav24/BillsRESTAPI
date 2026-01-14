using BillsApi.Data;
using BillsApi.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel - use configuration in production, hardcoded port for development
if (builder.Environment.IsDevelopment())
{
    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ListenAnyIP(5202); // HTTP on all interfaces for development
    });
}
// Production uses Kestrel configuration from appsettings.Production.json

// Add services to the container.
builder.Services.AddOpenApi();

// Add CORS support with named policy
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
        else
        {
            var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
                                 ?? ["https://yourdomain.com"];
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
    });
});

// Add health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<BillsDbContext>();

// Add Entity Framework Core with SQLite for dev, SQL Server for production
builder.Services.AddDbContext<BillsDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
                           ?? "Data Source=bills.db";
    
    if (builder.Environment.IsDevelopment())
    {
        options.UseSqlite(connectionString);
    }
    else
    {
        options.UseSqlServer(connectionString);
    }
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Apply CORS policy
app.UseCors();

// Only redirect to HTTPS in production
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Map health check endpoint
app.MapHealthChecks("/health");

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BillsDbContext>();
    db.Database.EnsureCreated();
}

// GET /bills - Get all bills
app.MapGet("/bills", async (BillsDbContext db) =>
    await db.Bills.ToListAsync())
    .WithName("GetAllBills");

// GET /bills/{id} - Get single bill by ID
app.MapGet("/bills/{id}", async (int id, BillsDbContext db) =>
    await db.Bills.FindAsync(id) is Bill bill
        ? Results.Ok(bill)
        : Results.NotFound(new { error = "Bill not found" }))
    .WithName("GetBillById");

// POST /bills - Create a new bill
app.MapPost("/bills", async (BillInput input, BillsDbContext db) =>
{
    // Input validation
    if (string.IsNullOrWhiteSpace(input.BillName))
    {
        return Results.BadRequest(new { error = "BillName is required" });
    }
    if (input.BillName.Length > 200)
    {
        return Results.BadRequest(new { error = "BillName cannot exceed 200 characters" });
    }
    if (input.Amount < 0)
    {
        return Results.BadRequest(new { error = "Amount cannot be negative" });
    }
    if (input.AmountOverMinimum < 0)
    {
        return Results.BadRequest(new { error = "AmountOverMinimum cannot be negative" });
    }

    var bill = new Bill
    {
        BillName = input.BillName.Trim(),
        Amount = input.Amount,
        Date = input.Date,
        AmountOverMinimum = input.AmountOverMinimum
    };

    db.Bills.Add(bill);
    await db.SaveChangesAsync();

    return Results.Created($"/bills/{bill.Id}", bill);
})
.WithName("CreateBill");

// PUT /bills/{id} - Update an existing bill
app.MapPut("/bills/{id}", async (int id, BillInput input, BillsDbContext db) =>
{
    // Input validation
    if (string.IsNullOrWhiteSpace(input.BillName))
    {
        return Results.BadRequest(new { error = "BillName is required" });
    }
    if (input.BillName.Length > 200)
    {
        return Results.BadRequest(new { error = "BillName cannot exceed 200 characters" });
    }
    if (input.Amount < 0)
    {
        return Results.BadRequest(new { error = "Amount cannot be negative" });
    }
    if (input.AmountOverMinimum < 0)
    {
        return Results.BadRequest(new { error = "AmountOverMinimum cannot be negative" });
    }

    var bill = await db.Bills.FindAsync(id);

    if (bill is null)
    {
        return Results.NotFound(new { error = "Bill not found" });
    }

    bill.BillName = input.BillName.Trim();
    bill.Amount = input.Amount;
    bill.Date = input.Date;
    bill.AmountOverMinimum = input.AmountOverMinimum;

    await db.SaveChangesAsync();

    return Results.Ok(bill);
})
.WithName("UpdateBill");

// DELETE /bills/{id} - Delete a bill
app.MapDelete("/bills/{id}", async (int id, BillsDbContext db) =>
{
    var bill = await db.Bills.FindAsync(id);

    if (bill is null)
    {
        return Results.NotFound(new { error = "Bill not found" });
    }

    db.Bills.Remove(bill);
    await db.SaveChangesAsync();

    return Results.NoContent();
})
.WithName("DeleteBill");

app.Run();

// Make Program accessible for integration tests
public partial class Program { }
