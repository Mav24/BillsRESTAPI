using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using BillsApi.Data;
using BillsApi.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BillsApi.Tests;

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
            });
        });
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
