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

// Configure SMTP email sender
builder.Services.Configure<BillsApi.Services.SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));

// Use SmtpEmailSender as the IEmailSender
builder.Services.AddSingleton<BillsApi.Services.IEmailSender, BillsApi.Services.SmtpEmailSender>();

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

// Serve static files from wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

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
    // Validate email
    if (string.IsNullOrWhiteSpace(request.Email))
    {
        return Results.BadRequest(new { error = "Email is required" });
    }

    // Check if username or email already exists
    if (await db.Users.AnyAsync(u => u.Username == request.Username))
    {
        return Results.BadRequest(new { error = "Username already exists" });
    }

    if (await db.Users.AnyAsync(u => u.Email == request.Email))
    {
        return Results.BadRequest(new { error = "Email already exists" });
    }

    var user = new User
    {
        Id = Guid.NewGuid().ToString(),
        Username = request.Username,
        Email = request.Email,
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

// POST /auth/forgot-password - send reset email
app.MapPost("/auth/forgot-password", async (ForgotPasswordRequest request, BillsDbContext db, BillsApi.Services.IEmailSender emailSender, IConfiguration config, ILogger<Program> logger) =>
{
    const string genericMessage = "If an account with that email exists, you will receive a password reset email.";

    var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
    if (user is null)
    {
        return Results.Ok(new { message = genericMessage });
    }

    // generate token
    var tokenBytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
    var token = Convert.ToBase64String(tokenBytes);

    using var sha = System.Security.Cryptography.SHA256.Create();
    var tokenHash = BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(token))).Replace("-", "").ToLowerInvariant();

    var reset = new BillsApi.Models.PasswordResetToken
    {
        Id = Guid.NewGuid().ToString(),
        UserId = user.Id,
        TokenHash = tokenHash,
        ExpiresAt = DateTime.UtcNow.AddHours(1),
        Used = false
    };

    db.PasswordResetTokens.Add(reset);
    await db.SaveChangesAsync();

    var baseUrl = config["Auth:ResetPasswordBaseUrl"] ?? "";
    var resetUrl = $"{baseUrl}?token={Uri.EscapeDataString(token)}&uid={Uri.EscapeDataString(user.Id)}";

    var html = $"<p>If you requested a password reset, click the link below (expires in 1 hour):</p><p><a href=\"{resetUrl}\">Reset password</a></p>";

    try
    {
        await emailSender.SendAsync(request.Email, "Password reset", html);
    }
    catch (Exception ex)
    {
        // Do not reveal email errors to the caller. Log for diagnostics.
        logger.LogError(ex, "Failed to send password reset email to {Email}", request.Email);
    }

    return Results.Ok(new { message = genericMessage });
})
.WithName("ForgotPassword");

// POST /auth/reset-password - accept token and new password
app.MapPost("/auth/reset-password", async (ResetPasswordRequest request, BillsDbContext db) =>
{
    var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId);
    if (user is null)
        return Results.BadRequest(new { message = "Invalid token or user." });

    using var sha = System.Security.Cryptography.SHA256.Create();
    var tokenHash = BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(request.Token))).Replace("-", "").ToLowerInvariant();

    var tokenEntry = await db.PasswordResetTokens
        .Where(t => t.UserId == user.Id && !t.Used && t.ExpiresAt > DateTime.UtcNow && t.TokenHash == tokenHash)
        .FirstOrDefaultAsync();

    if (tokenEntry is null)
        return Results.BadRequest(new { message = "Invalid or expired token." });

    // Update password
    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
    tokenEntry.Used = true;

    await db.SaveChangesAsync();

    return Results.Ok(new { message = "Password updated." });
})
.WithName("ResetPassword");

// DELETE /auth/account - Delete authenticated user account and all associated data
app.MapDelete("/auth/account", async (BillsDbContext db, ClaimsPrincipal user) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var userEntity = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);

    if (userEntity is null)
    {
        return Results.NotFound(new { error = "User not found" });
    }

    // Save household ID if user is in one (for cleanup check after deletion)
    var householdId = userEntity.HouseholdId;

    // Delete all bills for this user
    var bills = await db.Bills.Where(b => b.UserId == userId).ToListAsync();
    db.Bills.RemoveRange(bills);

    // Delete all refresh tokens
    var refreshTokens = await db.RefreshTokens.Where(rt => rt.UserId == userId).ToListAsync();
    db.RefreshTokens.RemoveRange(refreshTokens);

    // Delete all password reset tokens
    var resetTokens = await db.PasswordResetTokens.Where(rt => rt.UserId == userId).ToListAsync();
    db.PasswordResetTokens.RemoveRange(resetTokens);

    // Delete the user
    db.Users.Remove(userEntity);

    // If user was in a household, check if it's now empty and delete if so
    if (householdId is not null)
    {
        // Exclude the current user being deleted from the count
        var remainingMembers = await db.Users.CountAsync(u => u.HouseholdId == householdId && u.Id != userId);
        if (remainingMembers == 0)
        {
            var household = await db.Households.FindAsync(householdId.Value);
            if (household is not null)
            {
                db.Households.Remove(household);
            }
        }
    }

    await db.SaveChangesAsync();

    return Results.Ok(new { message = "Account and all associated data deleted successfully" });
})
.RequireAuthorization()
.WithName("DeleteAccount");

// GET /bills - Get all bills for the authenticated user OR their household
app.MapGet("/bills", async (BillsDbContext db, ClaimsPrincipal user) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var userEntity = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);

    if (userEntity is null)
    {
        return Results.NotFound(new { error = "User not found" });
    }

    // If user is in a household, return all household bills
    if (userEntity.HouseholdId is not null)
    {
        return Results.Ok(await db.Bills
            .Where(b => b.HouseholdId == userEntity.HouseholdId)
            .ToListAsync());
    }

    // Otherwise, return only their personal bills
    return Results.Ok(await db.Bills.Where(b => b.UserId == userId).ToListAsync());
})
.RequireAuthorization()
.WithName("GetAllBills");

// GET /bills/{id} - Get single bill (must be owned by user OR in their household)
app.MapGet("/bills/{id}", async (int id, BillsDbContext db, ClaimsPrincipal user) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var userEntity = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);

    if (userEntity is null)
    {
        return Results.NotFound(new { error = "User not found" });
    }

    Bill? bill;

    // If in household, can access any household bill
    if (userEntity.HouseholdId is not null)
    {
        bill = await db.Bills.FirstOrDefaultAsync(b => 
            b.Id == id && b.HouseholdId == userEntity.HouseholdId);
    }
    else
    {
        bill = await db.Bills.FirstOrDefaultAsync(b => 
            b.Id == id && b.UserId == userId);
    }

    return bill is not null
        ? Results.Ok(bill)
        : Results.NotFound(new { error = "Bill not found" });
})
.RequireAuthorization()
.WithName("GetBillById");
// POST /bills - Create a new bill (associates with household if user is in one)
app.MapPost("/bills", async (BillInput input, BillsDbContext db, ClaimsPrincipal user) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var userEntity = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);

    if (userEntity is null)
    {
        return Results.NotFound(new { error = "User not found" });
    }

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
        HouseholdId = userEntity.HouseholdId, // Automatically associates with household
        BillName = input.BillName.Trim(),
        Amount = input.Amount,
        Date = input.Date,
        AmountOverMinimum = input.AmountOverMinimum,
        IsPaid = input.IsPaid,
        PaidDate = input.PaidDate
    };

    db.Bills.Add(bill);
    await db.SaveChangesAsync();

    return Results.Created($"/bills/{bill.Id}", bill);
})
.RequireAuthorization()
.WithName("CreateBill");
// PUT /bills/{id} - Update bill (must be in same household or owned by user)
app.MapPut("/bills/{id}", async (int id, BillInput input, BillsDbContext db, ClaimsPrincipal user) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var userEntity = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);

    if (userEntity is null)
    {
        return Results.NotFound(new { error = "User not found" });
    }

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

    Bill? bill;

    // If in household, can update any household bill
    if (userEntity.HouseholdId is not null)
    {
        bill = await db.Bills.FirstOrDefaultAsync(b => 
            b.Id == id && b.HouseholdId == userEntity.HouseholdId);
    }
    else
    {
        bill = await db.Bills.FirstOrDefaultAsync(b => 
            b.Id == id && b.UserId == userId);
    }

    if (bill is null)
    {
        return Results.NotFound(new { error = "Bill not found" });
    }

    bill.BillName = input.BillName.Trim();
    bill.Amount = input.Amount;
    bill.Date = input.Date;
    bill.AmountOverMinimum = input.AmountOverMinimum;
    bill.IsPaid = input.IsPaid;
    bill.PaidDate = input.PaidDate;

    await db.SaveChangesAsync();

    return Results.Ok(bill);
})
.RequireAuthorization()
.WithName("UpdateBill");
// DELETE /bills/{id} - Delete bill (must be in same household or owned by user)
app.MapDelete("/bills/{id}", async (int id, BillsDbContext db, ClaimsPrincipal user) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var userEntity = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);

    if (userEntity is null)
    {
        return Results.NotFound(new { error = "User not found" });
    }

    Bill? bill;

    // If in household, can delete any household bill
    if (userEntity.HouseholdId is not null)
    {
        bill = await db.Bills.FirstOrDefaultAsync(b => 
            b.Id == id && b.HouseholdId == userEntity.HouseholdId);
    }
    else
    {
        bill = await db.Bills.FirstOrDefaultAsync(b => 
            b.Id == id && b.UserId == userId);
    }

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

// ============ HOUSEHOLD ENDPOINTS ============

// GET /households/my-household - Get current user's household
app.MapGet("/households/my-household", async (BillsDbContext db, ClaimsPrincipal user) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var userEntity = await db.Users
        .Include(u => u.Household)
        .ThenInclude(h => h!.Members)
        .FirstOrDefaultAsync(u => u.Id == userId);

    if (userEntity?.Household is null)
    {
        return Results.Ok(new { household = (object?)null });
    }

    var householdDto = new
    {
        id = userEntity.Household.Id,
        name = userEntity.Household.Name,
        createdAt = userEntity.Household.CreatedAt,
        members = userEntity.Household.Members.Select(m => new
        {
            id = m.Id,
            username = m.Username,
            email = m.Email
        }).ToList()
    };

    return Results.Ok(new { household = householdDto });
})
.RequireAuthorization()
.WithName("GetMyHousehold");

// POST /households - Create new household
app.MapPost("/households", async (CreateHouseholdRequest request, BillsDbContext db, ClaimsPrincipal user) =>
{
    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest(new { error = "Household name is required" });
    }

    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var userEntity = await db.Users.FindAsync(userId);

    if (userEntity is null)
    {
        return Results.NotFound(new { error = "User not found" });
    }

    if (userEntity.HouseholdId is not null)
    {
        return Results.BadRequest(new { error = "You are already in a household. Leave your current household first." });
    }

    var household = new Household
    {
        Id = Guid.NewGuid(),
        Name = request.Name.Trim(),
        CreatedAt = DateTime.UtcNow
    };

    db.Households.Add(household);
    userEntity.HouseholdId = household.Id;

    // Migrate existing bills to household
    var userBills = await db.Bills.Where(b => b.UserId == userId).ToListAsync();
    foreach (var bill in userBills)
    {
        bill.HouseholdId = household.Id;
    }

    await db.SaveChangesAsync();

    return Results.Created($"/households/my-household", new
    {
        id = household.Id,
        name = household.Name,
        createdAt = household.CreatedAt,
        members = new[]
        {
            new { id = userEntity.Id, username = userEntity.Username, email = userEntity.Email }
        }
    });
})
.RequireAuthorization()
.WithName("CreateHousehold");

// POST /households/invite - Send invitation email to user
app.MapPost("/households/invite", async (InviteRequest request, BillsDbContext db, ClaimsPrincipal user, BillsApi.Services.IEmailSender emailSender, IConfiguration config, ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(request.Email))
    {
        return Results.BadRequest(new { error = "Email is required" });
    }

    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var currentUser = await db.Users
        .Include(u => u.Household)
        .FirstOrDefaultAsync(u => u.Id == userId);

    if (currentUser?.HouseholdId is null)
    {
        return Results.BadRequest(new { error = "You must be in a household to invite others" });
    }

    // Check if user exists
    var invitedUser = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
    
    if (invitedUser is null)
    {
        return Results.BadRequest(new { error = "No user found with that email. They must register first." });
    }

    if (invitedUser.HouseholdId is not null)
    {
        return Results.BadRequest(new { error = "That user is already in a household" });
    }

    // Check for existing pending invitation
    var existingInvitation = await db.HouseholdInvitations
        .FirstOrDefaultAsync(i => i.Email == request.Email && 
                                  i.HouseholdId == currentUser.HouseholdId.Value && 
                                  !i.Accepted && 
                                  i.ExpiresAt > DateTime.UtcNow);
    
    if (existingInvitation is not null)
    {
        return Results.BadRequest(new { error = "An invitation has already been sent to this email" });
    }

    // Generate invitation token
    var tokenBytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
    var token = Convert.ToBase64String(tokenBytes);

    using var sha = System.Security.Cryptography.SHA256.Create();
    var tokenHash = BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(token))).Replace("-", "").ToLowerInvariant();

    var invitation = new HouseholdInvitation
    {
        Id = Guid.NewGuid().ToString(),
        HouseholdId = currentUser.HouseholdId.Value,
        Email = request.Email,
        TokenHash = tokenHash,
        InvitedByUserId = userId,
        CreatedAt = DateTime.UtcNow,
        ExpiresAt = DateTime.UtcNow.AddDays(7),
        Accepted = false
    };

    db.HouseholdInvitations.Add(invitation);
    await db.SaveChangesAsync();

    // Send invitation email
    var baseUrl = config["Auth:HouseholdInvitationBaseUrl"] ?? "https://bills.dukesducks.ca/billsapi";
    var acceptUrl = $"{baseUrl}/household-invitation?token={Uri.EscapeDataString(token)}";

    var html = $@"
        <h2>You've been invited to join {currentUser.Household!.Name}!</h2>
        <p>{currentUser.Username} has invited you to join their household on Bills Tracker.</p>
        <p>By accepting, you'll be able to share and view bills together.</p>
        <p><a href=""{acceptUrl}"" style=""display: inline-block; padding: 10px 20px; background-color: #007bff; color: white; text-decoration: none; border-radius: 5px;"">Accept Invitation</a></p>
        <p>Or copy and paste this link: {acceptUrl}</p>
        <p><em>This invitation expires in 7 days.</em></p>
    ";

    try
    {
        await emailSender.SendAsync(request.Email, $"Invitation to join {currentUser.Household.Name}", html);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to send household invitation email to {Email}", request.Email);
        return Results.Problem("Failed to send invitation email");
    }

    return Results.Ok(new { message = $"Invitation sent to {request.Email}" });
})
.RequireAuthorization()
.WithName("InviteToHousehold");

// GET /household-invitation - Display invitation acceptance page
app.MapGet("/household-invitation", async (HttpContext context, BillsDbContext db, string? token) =>
{
    if (string.IsNullOrWhiteSpace(token))
    {
        return Results.Content(GetInvitationErrorPage("Invalid invitation link"), "text/html");
    }

    // Hash the token to look up in database
    using var sha = System.Security.Cryptography.SHA256.Create();
    var tokenHash = BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(token))).Replace("-", "").ToLowerInvariant();

    var invitation = await db.HouseholdInvitations
        .Include(i => i.Household)
        .Include(i => i.InvitedByUser)
        .FirstOrDefaultAsync(i => i.TokenHash == tokenHash);

    if (invitation is null || invitation.ExpiresAt < DateTime.UtcNow || invitation.Accepted)
    {
        return Results.Content(GetInvitationErrorPage("This invitation is invalid or has expired"), "text/html");
    }

    return Results.Content(GetInvitationAcceptancePage(invitation.Household!.Name, invitation.InvitedByUser!.Username, token), "text/html");
})
.WithName("HouseholdInvitationPage");

// POST /households/accept-invitation - Accept household invitation
app.MapPost("/households/accept-invitation", async (AcceptInvitationRequest request, BillsDbContext db, ClaimsPrincipal user) =>
{
    if (string.IsNullOrWhiteSpace(request.Token))
    {
        return Results.BadRequest(new { error = "Token is required" });
    }

    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var currentUser = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);

    if (currentUser is null)
    {
        return Results.NotFound(new { error = "User not found" });
    }

    if (currentUser.HouseholdId is not null)
    {
        return Results.BadRequest(new { error = "You are already in a household" });
    }

    // Hash token and find invitation
    using var sha = System.Security.Cryptography.SHA256.Create();
    var tokenHash = BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(request.Token))).Replace("-", "").ToLowerInvariant();

    var invitation = await db.HouseholdInvitations
        .Include(i => i.Household)
        .FirstOrDefaultAsync(i => i.TokenHash == tokenHash && 
                                  i.Email == currentUser.Email && 
                                  !i.Accepted && 
                                  i.ExpiresAt > DateTime.UtcNow);

    if (invitation is null)
    {
        return Results.BadRequest(new { error = "Invalid or expired invitation" });
    }

    // Accept invitation - add user to household
    currentUser.HouseholdId = invitation.HouseholdId;
    invitation.Accepted = true;
    invitation.AcceptedAt = DateTime.UtcNow;

    // Migrate user's bills to household
    var userBills = await db.Bills.Where(b => b.UserId == userId).ToListAsync();
    foreach (var bill in userBills)
    {
        bill.HouseholdId = invitation.HouseholdId;
    }

    await db.SaveChangesAsync();

    return Results.Ok(new { 
        message = "Successfully joined household!", 
        householdName = invitation.Household!.Name 
    });
})
.RequireAuthorization()
.WithName("AcceptHouseholdInvitation");

// POST /households/leave - Leave current household
app.MapPost("/households/leave", async (BillsDbContext db, ClaimsPrincipal user) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var userEntity = await db.Users
        .Include(u => u.Household)
        .ThenInclude(h => h!.Members)
        .FirstOrDefaultAsync(u => u.Id == userId);

    if (userEntity?.HouseholdId is null)
    {
        return Results.BadRequest(new { error = "You are not in a household" });
    }

    var householdId = userEntity.HouseholdId.Value;
    userEntity.HouseholdId = null;

    // Keep their bills but remove household association
    var userBills = await db.Bills.Where(b => b.UserId == userId).ToListAsync();
    foreach (var bill in userBills)
    {
        bill.HouseholdId = null;
    }

    // Check if household is now empty
    var remainingMembers = await db.Users.CountAsync(u => u.HouseholdId == householdId);
    if (remainingMembers == 0)
    {
        var household = await db.Households.FindAsync(householdId);
        if (household is not null)
        {
            db.Households.Remove(household);
        }
    }

    await db.SaveChangesAsync();

    return Results.Ok(new { message = "Left household successfully" });
})
.RequireAuthorization()
.WithName("LeaveHousehold");

// DELETE /households/members/{memberId} - Remove member from household
app.MapDelete("/households/members/{memberId}", async (string memberId, BillsDbContext db, ClaimsPrincipal user) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var currentUser = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);

    if (currentUser?.HouseholdId is null)
    {
        return Results.BadRequest(new { error = "You are not in a household" });
    }

    var memberToRemove = await db.Users.FirstOrDefaultAsync(u => u.Id == memberId);

    if (memberToRemove is null)
    {
        return Results.NotFound(new { error = "Member not found" });
    }

    if (memberToRemove.HouseholdId != currentUser.HouseholdId)
    {
        return Results.BadRequest(new { error = "That user is not in your household" });
    }

    memberToRemove.HouseholdId = null;

    // Keep their bills but remove household association
    var memberBills = await db.Bills.Where(b => b.UserId == memberId).ToListAsync();
    foreach (var bill in memberBills)
    {
        bill.HouseholdId = null;
    }

    await db.SaveChangesAsync();

    return Results.Ok(new { message = "Member removed from household successfully" });
})
.RequireAuthorization()
.WithName("RemoveMember");

// DELETE /households - Delete the entire household (creator only)
app.MapDelete("/households", async (BillsDbContext db, ClaimsPrincipal user) =>
{
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
    var currentUser = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);

    if (currentUser?.HouseholdId is null)
    {
        return Results.BadRequest(new { error = "You are not in a household" });
    }

    var household = await db.Households
        .Include(h => h.Members)
        .FirstOrDefaultAsync(h => h.Id == currentUser.HouseholdId);

    if (household is null)
    {
        return Results.NotFound(new { error = "Household not found" });
    }

    // Remove household association from all members
    foreach (var member in household.Members)
    {
        member.HouseholdId = null;
    }

    // Remove household association from all bills
    var householdBills = await db.Bills.Where(b => b.HouseholdId == household.Id).ToListAsync();
    foreach (var bill in householdBills)
    {
        bill.HouseholdId = null;
    }

    // Delete the household (invitations will cascade delete automatically)
    db.Households.Remove(household);
    await db.SaveChangesAsync();

    return Results.Ok(new { message = "Household deleted successfully. All members have been returned to personal mode." });
})
.RequireAuthorization()
.WithName("DeleteHousehold");

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

static string GetInvitationAcceptancePage(string householdName, string inviterUsername, string token)
{
    return $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Household Invitation</title>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            display: flex;
            justify-content: center;
            align-items: center;
            min-height: 100vh;
            margin: 0;
            padding: 20px;
        }}
        .container {{
            background: white;
            border-radius: 10px;
            box-shadow: 0 10px 40px rgba(0,0,0,0.2);
            max-width: 500px;
            padding: 40px;
            text-align: center;
        }}
        h1 {{
            color: #333;
            margin-bottom: 20px;
        }}
        p {{
            color: #666;
            line-height: 1.6;
            margin-bottom: 30px;
        }}
        .household-name {{
            font-weight: bold;
            color: #667eea;
            font-size: 1.2em;
        }}
        .accept-btn {{
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            border: none;
            padding: 15px 40px;
            font-size: 16px;
            border-radius: 5px;
            cursor: pointer;
            transition: transform 0.2s;
        }}
        .accept-btn:hover {{
            transform: scale(1.05);
        }}
        .message {{
            margin-top: 20px;
            padding: 15px;
            border-radius: 5px;
            display: none;
        }}
        .success {{
            background-color: #d4edda;
            color: #155724;
        }}
        .error {{
            background-color: #f8d7da;
            color: #721c24;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <h1>🏡 Household Invitation</h1>
        <p>{inviterUsername} has invited you to join</p>
        <p class=""household-name"">{householdName}</p>
        <p>By accepting, you'll be able to share and view bills together.</p>
        <button class=""accept-btn"" onclick=""acceptInvitation()"">Accept Invitation</button>
        <div id=""message"" class=""message""></div>
        <p style=""margin-top: 30px; font-size: 0.9em; color: #999;"">
            Need to login? Please login in your Bills Tracker app first, then come back to this page.
        </p>
    </div>

    <script>
        async function acceptInvitation() {{
            const btn = document.querySelector('.accept-btn');
            const msgDiv = document.getElementById('message');
            
            btn.disabled = true;
            btn.textContent = 'Processing...';
            
            try {{
                const response = await fetch('/households/accept-invitation', {{
                    method: 'POST',
                    headers: {{
                        'Content-Type': 'application/json'
                    }},
                    body: JSON.stringify({{ token: '{token}' }})
                }});
                
                const data = await response.json();
                
                if (response.ok) {{
                    msgDiv.className = 'message success';
                    msgDiv.style.display = 'block';
                    msgDiv.textContent = '✓ ' + data.message + ' You can now close this page and return to the app.';
                    btn.style.display = 'none';
                }} else {{
                    msgDiv.className = 'message error';
                    msgDiv.style.display = 'block';
                    msgDiv.textContent = '✗ ' + (data.error || 'Failed to accept invitation');
                    btn.disabled = false;
                    btn.textContent = 'Try Again';
                }}
            }} catch (error) {{
                msgDiv.className = 'message error';
                msgDiv.style.display = 'block';
                msgDiv.textContent = '✗ Network error. Please make sure you\'re logged into the app.';
                btn.disabled = false;
                btn.textContent = 'Try Again';
            }}
        }}
    </script>
</body>
</html>";
}

static string GetInvitationErrorPage(string errorMessage)
{
    return $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Invitation Error</title>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            display: flex;
            justify-content: center;
            align-items: center;
            min-height: 100vh;
            margin: 0;
            padding: 20px;
        }}
        .container {{
            background: white;
            border-radius: 10px;
            box-shadow: 0 10px 40px rgba(0,0,0,0.2);
            max-width: 500px;
            padding: 40px;
            text-align: center;
        }}
        h1 {{
            color: #721c24;
            margin-bottom: 20px;
        }}
        p {{
            color: #666;
            line-height: 1.6;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <h1>⚠️ Invitation Error</h1>
        <p>{errorMessage}</p>
        <p>Please contact the person who sent you the invitation for a new link.</p>
    </div>
</body>
</html>";
}

// End of program

// Add these request record types at the bottom with other records
record CreateHouseholdRequest(string Name);
record InviteRequest(string Email);
