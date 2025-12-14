using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;
using Xunit;
using Wex.CorporatePayments.Api;
using Wex.CorporatePayments.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Wex.CorporatePayments.Application.Commands;

namespace Wex.CorporatePayments.Tests.Integration;

public class PurchasesControllerIdempotencyTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _databasePath;

    public PurchasesControllerIdempotencyTests(WebApplicationFactory<Program> factory)
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"test_db_{Guid.NewGuid()}.db");
        
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Remove the existing DbContext configuration
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // Add test database configuration
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseSqlite($"Data Source={_databasePath}");
                });
            });
        });
    }

    [Fact]
    public async Task CreatePurchase_WithValidCommand_ShouldReturn201Created()
    {
        // Arrange
        var client = _factory.CreateClient();
        
        // Ensure database is created
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Database.EnsureCreated();
        }

        var command = new StorePurchaseCommand
        {
            Description = "Test Purchase",
            TransactionDate = DateTime.UtcNow,
            Amount = 100.50m,
            Currency = "USD",
            IdempotencyKey = null
        };

        var json = JsonSerializer.Serialize(command);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/purchases", content);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        
        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(responseContent);
        
        Assert.True(result.TryGetProperty("Id", out var idProperty));
        Assert.NotEqual(Guid.Empty, Guid.Parse(idProperty.GetString()!));

        // Verify the purchase was actually created in the database
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var purchase = await context.Purchases.FindAsync(Guid.Parse(idProperty.GetString()!));
            
            Assert.NotNull(purchase);
            Assert.Equal("Test Purchase", purchase.Description);
            Assert.Equal(100.50m, purchase.OriginalAmount.Amount);
        }
    }

    [Fact]
    public async Task CreatePurchase_WithSameIdempotencyKey_ShouldReturn409Conflict()
    {
        // Arrange
        var client = _factory.CreateClient();
        var idempotencyKey = $"test-key-{Guid.NewGuid()}";
        
        // Ensure database is created
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Database.EnsureCreated();
        }

        var command = new StorePurchaseCommand
        {
            Description = "Test Purchase",
            TransactionDate = DateTime.UtcNow,
            Amount = 100.50m,
            Currency = "USD",
            IdempotencyKey = idempotencyKey
        };

        var json = JsonSerializer.Serialize(command);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        // Act - First request should succeed
        var firstResponse = await client.PostAsync("/api/purchases", content);
        
        // Assert - First request should return 201
        Assert.Equal(System.Net.HttpStatusCode.Created, firstResponse.StatusCode);
        
        var firstResponseContent = await firstResponse.Content.ReadAsStringAsync();
        var firstResult = JsonSerializer.Deserialize<JsonElement>(firstResponseContent);
        Assert.True(firstResult.TryGetProperty("Id", out var firstIdProperty));
        var firstPurchaseId = Guid.Parse(firstIdProperty.GetString()!);

        // Act - Second request with same idempotency key should return 409
        var secondResponse = await client.PostAsync("/api/purchases", content);

        // Assert - Second request should return 409
        Assert.Equal(System.Net.HttpStatusCode.Conflict, secondResponse.StatusCode);
        
        var secondResponseContent = await secondResponse.Content.ReadAsStringAsync();
        var secondResult = JsonSerializer.Deserialize<JsonElement>(secondResponseContent);
        
        Assert.True(secondResult.TryGetProperty("Code", out var codeProperty));
        Assert.Equal("IDEMPOTENCY_CONFLICT", codeProperty.GetString());
        
        Assert.True(secondResult.TryGetProperty("IdempotencyKey", out var keyProperty));
        Assert.Equal(idempotencyKey, keyProperty.GetString());

        // Verify only one purchase was created in the database
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var purchases = await context.Purchases
                .Where(p => p.IdempotencyKey == idempotencyKey)
                .ToListAsync();
            
            Assert.Single(purchases);
            Assert.Equal(firstPurchaseId, purchases[0].Id);
        }
    }

    [Fact]
    public async Task CreatePurchase_WithDifferentIdempotencyKeys_ShouldCreateMultiplePurchases()
    {
        // Arrange
        var client = _factory.CreateClient();
        var idempotencyKey1 = $"test-key-1-{Guid.NewGuid()}";
        var idempotencyKey2 = $"test-key-2-{Guid.NewGuid()}";
        
        // Ensure database is created
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Database.EnsureCreated();
        }

        var command1 = new StorePurchaseCommand
        {
            Description = "Test Purchase 1",
            TransactionDate = DateTime.UtcNow,
            Amount = 100.50m,
            Currency = "USD",
            IdempotencyKey = idempotencyKey1
        };

        var command2 = new StorePurchaseCommand
        {
            Description = "Test Purchase 2",
            TransactionDate = DateTime.UtcNow,
            Amount = 200.75m,
            Currency = "USD",
            IdempotencyKey = idempotencyKey2
        };

        var json1 = JsonSerializer.Serialize(command1);
        var json2 = JsonSerializer.Serialize(command2);
        var content1 = new StringContent(json1, System.Text.Encoding.UTF8, "application/json");
        var content2 = new StringContent(json2, System.Text.Encoding.UTF8, "application/json");

        // Act
        var response1 = await client.PostAsync("/api/purchases", content1);
        var response2 = await client.PostAsync("/api/purchases", content2);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.Created, response1.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.Created, response2.StatusCode);

        var response1Content = await response1.Content.ReadAsStringAsync();
        var response2Content = await response2.Content.ReadAsStringAsync();
        var result1 = JsonSerializer.Deserialize<JsonElement>(response1Content);
        var result2 = JsonSerializer.Deserialize<JsonElement>(response2Content);
        
        Assert.True(result1.TryGetProperty("Id", out var id1Property));
        Assert.True(result2.TryGetProperty("Id", out var id2Property));
        
        var purchaseId1 = Guid.Parse(id1Property.GetString()!);
        var purchaseId2 = Guid.Parse(id2Property.GetString()!);

        Assert.NotEqual(purchaseId1, purchaseId2);

        // Verify both purchases were created in the database
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var purchases = await context.Purchases
                .Where(p => p.IdempotencyKey == idempotencyKey1 || p.IdempotencyKey == idempotencyKey2)
                .ToListAsync();
            
            Assert.Equal(2, purchases.Count);
        }
    }

    [Fact]
    public async Task CreatePurchase_WithoutIdempotencyKey_ShouldCreateMultiplePurchases()
    {
        // Arrange
        var client = _factory.CreateClient();
        
        // Ensure database is created
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Database.EnsureCreated();
        }

        var command = new StorePurchaseCommand
        {
            Description = "Test Purchase",
            TransactionDate = DateTime.UtcNow,
            Amount = 100.50m,
            Currency = "USD",
            IdempotencyKey = null
        };

        var json = JsonSerializer.Serialize(command);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        // Act
        var response1 = await client.PostAsync("/api/purchases", content);
        var response2 = await client.PostAsync("/api/purchases", content);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.Created, response1.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.Created, response2.StatusCode);

        var response1Content = await response1.Content.ReadAsStringAsync();
        var response2Content = await response2.Content.ReadAsStringAsync();
        var result1 = JsonSerializer.Deserialize<JsonElement>(response1Content);
        var result2 = JsonSerializer.Deserialize<JsonElement>(response2Content);
        
        Assert.True(result1.TryGetProperty("Id", out var id1Property));
        Assert.True(result2.TryGetProperty("Id", out var id2Property));
        
        var purchaseId1 = Guid.Parse(id1Property.GetString()!);
        var purchaseId2 = Guid.Parse(id2Property.GetString()!);

        Assert.NotEqual(purchaseId1, purchaseId2);

        // Verify both purchases were created in the database (no idempotency check when key is null)
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var purchases = await context.Purchases
                .Where(p => p.Description == "Test Purchase")
                .ToListAsync();
            
            Assert.Equal(2, purchases.Count);
        }
    }

    [Fact]
    public async Task CreatePurchase_WithEmptyIdempotencyKey_ShouldCreateMultiplePurchases()
    {
        // Arrange
        var client = _factory.CreateClient();
        
        // Ensure database is created
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Database.EnsureCreated();
        }

        var command = new StorePurchaseCommand
        {
            Description = "Test Purchase",
            TransactionDate = DateTime.UtcNow,
            Amount = 100.50m,
            Currency = "USD",
            IdempotencyKey = ""
        };

        var json = JsonSerializer.Serialize(command);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        // Act
        var response1 = await client.PostAsync("/api/purchases", content);
        var response2 = await client.PostAsync("/api/purchases", content);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.Created, response1.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.Created, response2.StatusCode);

        var response1Content = await response1.Content.ReadAsStringAsync();
        var response2Content = await response2.Content.ReadAsStringAsync();
        var result1 = JsonSerializer.Deserialize<JsonElement>(response1Content);
        var result2 = JsonSerializer.Deserialize<JsonElement>(response2Content);
        
        Assert.True(result1.TryGetProperty("Id", out var id1Property));
        Assert.True(result2.TryGetProperty("Id", out var id2Property));
        
        var purchaseId1 = Guid.Parse(id1Property.GetString()!);
        var purchaseId2 = Guid.Parse(id2Property.GetString()!);

        Assert.NotEqual(purchaseId1, purchaseId2);

        // Verify both purchases were created in the database (no idempotency check when key is empty)
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var purchases = await context.Purchases
                .Where(p => p.Description == "Test Purchase")
                .ToListAsync();
            
            Assert.Equal(2, purchases.Count);
        }
    }

    private void Dispose()
    {
        // Clean up test database
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }
}
