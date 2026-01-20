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
    
    // Suppress the pending model changes warning
    options.ConfigureWarnings(warnings => 
        warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseDeveloperExceptionPage();
}
else
{
    // Temporary: Show detailed errors in production to diagnose issue
    app.UseDeveloperExceptionPage();
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

// Ensure database is created and migrations are applied
// TEMPORARILY DISABLED - Tables already exist in SQL Server
// using (var scope = app.Services.CreateScope())
// {
//     var db = scope.ServiceProvider.GetRequiredService<BillsDbContext>();
//     db.Database.Migrate();
// }

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

// POST /auth/login - Login and get JWT + Refresh token
app.MapPost("/auth/login", async (LoginRequest request, BillsDbContext db, IConfiguration config) =>
{
    var user = await db.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
    
    if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
    {
        return Results.Unauthorized();
    }

    var jwtSettings = config.GetSection("Jwt");
    var secretKey = jwtSettings["SecretKey"]!;
    var accessToken = GenerateAccessToken(user, jwtSettings, secretKey);
    var refreshToken = await GenerateRefreshTokenAsync(user.Id, db);

    return Results.Ok(new 
    { 
        accessToken,
        refreshToken = refreshToken.Token,
        expiresIn = 900 // 15 minutes in seconds
    });
})
.WithName("Login");

// POST /auth/refresh - Get new access token using refresh token
app.MapPost("/auth/refresh", async (RefreshRequest request, BillsDbContext db, IConfiguration config) =>
{
    var storedToken = await db.RefreshTokens
        .Include(rt => rt.User)
        .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken);

    if (storedToken is null || storedToken.IsRevoked || storedToken.ExpiresAt < DateTime.UtcNow)
    {
        return Results.Unauthorized();
    }

    // Revoke old refresh token (rotation for security)
    storedToken.IsRevoked = true;

    var jwtSettings = config.GetSection("Jwt");
    var secretKey = jwtSettings["SecretKey"]!;
    var accessToken = GenerateAccessToken(storedToken.User!, jwtSettings, secretKey);
    var newRefreshToken = await GenerateRefreshTokenAsync(storedToken.UserId, db);

    await db.SaveChangesAsync();

    return Results.Ok(new 
    { 
        accessToken,
        refreshToken = newRefreshToken.Token,
        expiresIn = 900
    });
})
.WithName("RefreshToken");

// POST /auth/logout - Revoke refresh token
app.MapPost("/auth/logout", async (RefreshRequest request, BillsDbContext db) =>
{
    var storedToken = await db.RefreshTokens
        .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken);

    if (storedToken is not null)
    {
        storedToken.IsRevoked = true;
        await db.SaveChangesAsync();
    }

    return Results.Ok(new { message = "Logged out successfully" });
})
.WithName("Logout");

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

// Helper methods - must be BEFORE the type declaration
static string GenerateAccessToken(User user, IConfigurationSection jwtSettings, string secretKey)
{
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
        expires: DateTime.UtcNow.AddMinutes(15),
        signingCredentials: credentials
    );

    return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
}

static async Task<RefreshToken> GenerateRefreshTokenAsync(string userId, BillsDbContext db)
{
    var refreshToken = new RefreshToken
    {
        Id = Guid.NewGuid().ToString(),
        Token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64)),
        UserId = userId,
        ExpiresAt = DateTime.UtcNow.AddDays(7),
        CreatedAt = DateTime.UtcNow,
        IsRevoked = false
    };

    db.RefreshTokens.Add(refreshToken);
    await db.SaveChangesAsync();

    return refreshToken;
}

public partial class Program { }  // ← Type declaration MUST be last
