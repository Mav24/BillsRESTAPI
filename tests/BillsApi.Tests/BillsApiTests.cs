using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using BillsApi.Data;
using BillsApi.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BillsApi.Tests;

/// <summary>
/// A fake authentication handler that authenticates every request as the test user.
/// </summary>
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestScheme";
    public const string TestUserId = "test-user-id";
    public const string TestUserName = "testuser";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, TestUserId),
            new Claim(ClaimTypes.Name, TestUserName)
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public class BillsApiTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly DbConnection _connection;

    public BillsApiTests(WebApplicationFactory<Program> factory)
    {
        // Create and open an in-memory SQLite connection that stays open
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove the existing DbContext registration
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<BillsDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Add SQLite in-memory database for testing (using same provider as production)
                services.AddDbContext<BillsDbContext>(options =>
                {
                    options.UseSqlite(_connection);
                });

                // Replace authentication with the test scheme so requests are authenticated
                services.AddAuthentication(TestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        TestAuthHandler.SchemeName, _ => { });
            });
        });

        // Create the test database schema and seed the test user
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BillsDbContext>();
            db.Database.EnsureCreated();

            // Seed a user that matches the identity provided by TestAuthHandler
            db.Users.Add(new User
            {
                Id = TestAuthHandler.TestUserId,
                Username = TestAuthHandler.TestUserName,
                Email = "test@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password")
            });
            db.SaveChanges();
        }

        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetAllBills_ReturnsEmptyList_WhenNoData()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/bills");

        // Assert
        response.EnsureSuccessStatusCode();
        var bills = await response.Content.ReadFromJsonAsync<List<Bill>>();
        Assert.NotNull(bills);
        Assert.Empty(bills);
    }

    [Fact]
    public async Task CreateBill_ReturnsCreatedBill()
    {
        // Arrange
        var newBill = new BillInput
        {
            BillName = "Electricity",
            Amount = 125.50m,
            Date = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            AmountOverMinimum = 20.50m
        };

        // Act
        var response = await _client.PostAsJsonAsync("/bills", newBill);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var createdBill = await response.Content.ReadFromJsonAsync<Bill>();
        Assert.NotNull(createdBill);
        Assert.True(createdBill.Id > 0);
        Assert.Equal("Electricity", createdBill.BillName);
        Assert.Equal(125.50m, createdBill.Amount);
        Assert.Equal(20.50m, createdBill.AmountOverMinimum);
    }

    [Fact]
    public async Task GetBillById_ReturnsNotFound_WhenBillDoesNotExist()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/bills/99999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateAndGetBill_ReturnsCorrectBill()
    {
        // Arrange
        var newBill = new BillInput
        {
            BillName = "Water",
            Amount = 75.00m,
            Date = new DateTime(2024, 2, 10, 0, 0, 0, DateTimeKind.Utc),
            AmountOverMinimum = 5.00m
        };

        // Act - Create
        var createResponse = await _client.PostAsJsonAsync("/bills", newBill);
        createResponse.EnsureSuccessStatusCode();
        var createdBill = await createResponse.Content.ReadFromJsonAsync<Bill>();
        Assert.NotNull(createdBill);

        // Act - Get
        var getResponse = await _client.GetAsync($"/bills/{createdBill.Id}");

        // Assert
        getResponse.EnsureSuccessStatusCode();
        var retrievedBill = await getResponse.Content.ReadFromJsonAsync<Bill>();
        Assert.NotNull(retrievedBill);
        Assert.Equal(createdBill.Id, retrievedBill.Id);
        Assert.Equal("Water", retrievedBill.BillName);
        Assert.Equal(75.00m, retrievedBill.Amount);
    }

    [Fact]
    public async Task UpdateBill_UpdatesExistingBill()
    {
        // Arrange - Create a bill first
        var newBill = new BillInput
        {
            BillName = "Gas",
            Amount = 50.00m,
            Date = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            AmountOverMinimum = 0.00m
        };

        var createResponse = await _client.PostAsJsonAsync("/bills", newBill);
        createResponse.EnsureSuccessStatusCode();
        var createdBill = await createResponse.Content.ReadFromJsonAsync<Bill>();
        Assert.NotNull(createdBill);

        // Arrange - Update data
        var updatedBill = new BillInput
        {
            BillName = "Gas",
            Amount = 65.00m,
            Date = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            AmountOverMinimum = 15.00m
        };

        // Act
        var updateResponse = await _client.PutAsJsonAsync($"/bills/{createdBill.Id}", updatedBill);

        // Assert
        updateResponse.EnsureSuccessStatusCode();
        var returnedBill = await updateResponse.Content.ReadFromJsonAsync<Bill>();
        Assert.NotNull(returnedBill);
        Assert.Equal(65.00m, returnedBill.Amount);
        Assert.Equal(15.00m, returnedBill.AmountOverMinimum);
    }

    [Fact]
    public async Task UpdateBill_ReturnsNotFound_WhenBillDoesNotExist()
    {
        // Arrange
        var updatedBill = new BillInput
        {
            BillName = "Unknown",
            Amount = 100.00m,
            Date = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            AmountOverMinimum = 0.00m
        };

        // Act
        var response = await _client.PutAsJsonAsync("/bills/99999", updatedBill);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteBill_ReturnsNoContent_WhenBillExists()
    {
        // Arrange - Create a bill first
        var newBill = new BillInput
        {
            BillName = "Internet",
            Amount = 80.00m,
            Date = new DateTime(2024, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            AmountOverMinimum = 0.00m
        };

        var createResponse = await _client.PostAsJsonAsync("/bills", newBill);
        createResponse.EnsureSuccessStatusCode();
        var createdBill = await createResponse.Content.ReadFromJsonAsync<Bill>();
        Assert.NotNull(createdBill);

        // Act
        var deleteResponse = await _client.DeleteAsync($"/bills/{createdBill.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Verify deletion
        var getResponse = await _client.GetAsync($"/bills/{createdBill.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteBill_ReturnsNotFound_WhenBillDoesNotExist()
    {
        // Arrange & Act
        var response = await _client.DeleteAsync("/bills/99999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
