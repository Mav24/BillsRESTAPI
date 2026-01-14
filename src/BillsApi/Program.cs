using BillsApi.Data;
using BillsApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Load secrets from separate file (not tracked by Git)
builder.Configuration.AddJsonFile("appsettings.Secrets.json", optional: true, reloadOnChange: true);

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

// Add JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey is not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
        };
    });

builder.Services.AddAuthorization();

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

// Add authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

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

// POST /auth/register - Register a new user
app.MapPost("/auth/register", async (RegisterRequest request, BillsDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length < 3)
    {
        return Results.BadRequest(new { error = "Username must be at least 3 characters" });
    }
    if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
    {
        return Results.BadRequest(new { error = "Password must be at least 6 characters" });
    }

    // Check if username already exists
    if (await db.Users.AnyAsync(u => u.Username == request.Username))
    {
        return Results.BadRequest(new { error = "Username already exists" });
    }

    var user = new User
    {
        Id = Guid.NewGuid().ToString(),
        Username = request.Username,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
    };

    db.Users.Add(user);
    await db.SaveChangesAsync();

    return Results.Ok(new { message = "User registered successfully" });
})
.WithName("Register");

// POST /auth/login - Login and get JWT token
app.MapPost("/auth/login", async (LoginRequest request, BillsDbContext db, IConfiguration config) =>
{
    var user = await db.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
    
    if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
    {
        return Results.Unauthorized();
    }

    var jwtSettings = config.GetSection("Jwt");
    var secretKey = jwtSettings["SecretKey"]!;
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
    var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var claims = new[]
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id),
        new Claim(ClaimTypes.Name, user.Username)
    };

    var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
        issuer: jwtSettings["Issuer"],
        audience: jwtSettings["Audience"],
        claims: claims,
        expires: DateTime.UtcNow.AddHours(24),
        signingCredentials: credentials
    );

    var tokenString = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);

    return Results.Ok(new { token = tokenString });
})
.WithName("Login");

// GET /bills - Get all bills for the authenticated user
app.MapGet("/bills", async (BillsDbContext db, ClaimsPrincipal user) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    return await db.Bills.Where(b => b.UserId == userId).ToListAsync();
})
    .RequireAuthorization()
    .WithName("GetAllBills");

// GET /bills/{id} - Get single bill by ID (only if owned by user)
app.MapGet("/bills/{id}", async (int id, BillsDbContext db, ClaimsPrincipal user) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var bill = await db.Bills.FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);
    return bill is not null
        ? Results.Ok(bill)
        : Results.NotFound(new { error = "Bill not found" });
})
    .RequireAuthorization()
    .WithName("GetBillById");

// POST /bills - Create a new bill
app.MapPost("/bills", async (BillInput input, BillsDbContext db, ClaimsPrincipal user) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;

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
        UserId = userId,
        BillName = input.BillName.Trim(),
        Amount = input.Amount,
        Date = input.Date,
        AmountOverMinimum = input.AmountOverMinimum
    };

    db.Bills.Add(bill);
    await db.SaveChangesAsync();

    return Results.Created($"/bills/{bill.Id}", bill);
})
.RequireAuthorization()
.WithName("CreateBill");

// PUT /bills/{id} - Update an existing bill (only if owned by user)
app.MapPut("/bills/{id}", async (int id, BillInput input, BillsDbContext db, ClaimsPrincipal user) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;

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

    var bill = await db.Bills.FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);

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
.RequireAuthorization()
.WithName("UpdateBill");

// DELETE /bills/{id} - Delete a bill (only if owned by user)
app.MapDelete("/bills/{id}", async (int id, BillsDbContext db, ClaimsPrincipal user) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var bill = await db.Bills.FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);

    if (bill is null)
    {
        return Results.NotFound(new { error = "Bill not found" });
    }

    db.Bills.Remove(bill);
    await db.SaveChangesAsync();

    return Results.NoContent();
})
.RequireAuthorization()
.WithName("DeleteBill");

app.Run();

// Make Program accessible for integration tests
public partial class Program { }
