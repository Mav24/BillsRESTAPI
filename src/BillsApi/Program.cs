using BillsApi.Data;
using BillsApi.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();

// Add CORS support
builder.Services.AddCors();

// Add Entity Framework Core with SQLite
builder.Services.AddDbContext<BillsDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") 
                      ?? "Data Source=bills.db"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Configure CORS - allows all origins for development
// IMPORTANT: For production, restrict CORS to specific origins
app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

app.UseHttpsRedirection();

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
    var bill = new Bill
    {
        BillName = input.BillName,
        Amount = input.Amount,
        Date = input.Date,
        AmountOverMinimum = input.AmountOverMinimum
    };

    db.Bills.Add(bill);
    await db.SaveChangesAsync();

    return Results.Created($"/bills/{bill.Id}", bill);
})
.WithName("CreateBill");

// PUT /bills/{id} - Update an existing bill (Optional)
app.MapPut("/bills/{id}", async (int id, BillInput input, BillsDbContext db) =>
{
    var bill = await db.Bills.FindAsync(id);

    if (bill is null)
    {
        return Results.NotFound(new { error = "Bill not found" });
    }

    bill.BillName = input.BillName;
    bill.Amount = input.Amount;
    bill.Date = input.Date;
    bill.AmountOverMinimum = input.AmountOverMinimum;

    await db.SaveChangesAsync();

    return Results.Ok(bill);
})
.WithName("UpdateBill");

// DELETE /bills/{id} - Delete a bill (Optional)
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
