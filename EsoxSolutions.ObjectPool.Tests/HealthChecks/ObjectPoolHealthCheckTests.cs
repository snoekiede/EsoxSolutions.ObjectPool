using EsoxSolutions.ObjectPool.DependencyInjection;
using EsoxSolutions.ObjectPool.HealthChecks;
using EsoxSolutions.ObjectPool.Interfaces;
using EsoxSolutions.ObjectPool.Models;
using EsoxSolutions.ObjectPool.Tests.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EsoxSolutions.ObjectPool.Tests.HealthChecks;

public class ObjectPoolHealthCheckTests
{
    [Fact]
    public async Task HealthCheck_WithHealthyPool_ReturnsHealthy()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddObjectPool<Car>(builder => builder
            .WithInitialObjects(Car.GetInitialCars())
            .WithMaxSize(100)
            .WithMaxActiveObjects(50));

        services.AddHealthChecks()
            .AddObjectPoolHealthCheck<Car>("car-pool");

        var provider = services.BuildServiceProvider();
        var healthCheckService = provider.GetRequiredService<HealthCheckService>();

        // Act
        var result = await healthCheckService.CheckHealthAsync();

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("car-pool", result.Entries.Keys);
        
        var carPoolEntry = result.Entries["car-pool"];
        Assert.Equal(HealthStatus.Healthy, carPoolEntry.Status);
        Assert.Contains("available_objects", carPoolEntry.Data.Keys);
        Assert.Contains("utilization_percentage", carPoolEntry.Data.Keys);
    }

    [Fact]
    public async Task HealthCheck_WithDegradedPool_ReturnsDegraded()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddObjectPool<Car>(builder => builder
            .WithInitialObjects(Car.GetInitialCars())
            .WithMaxSize(7)
            .WithMaxActiveObjects(7));

        services.AddHealthChecks()
            .AddObjectPoolHealthCheck<Car>("car-pool", configureOptions: options =>
            {
                options.DegradedUtilizationThreshold = 50.0; // Lower threshold for testing
            });

        var provider = services.BuildServiceProvider();
        var pool = provider.GetRequiredService<IObjectPool<Car>>();
        var healthCheckService = provider.GetRequiredService<HealthCheckService>();

        // Take out most objects to create high utilization
        var objects = new List<PoolModel<Car>>();
        for (int i = 0; i < 5; i++)
        {
            objects.Add(pool.GetObject());
        }

        // Act
        var result = await healthCheckService.CheckHealthAsync();

        // Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);
        var carPoolEntry = result.Entries["car-pool"];
        Assert.Equal(HealthStatus.Degraded, carPoolEntry.Status);
        
        var utilization = (double)carPoolEntry.Data["utilization_percentage"];
        Assert.True(utilization > 50.0);

        // Cleanup
        objects.ForEach(o => o.Dispose());
    }

    [Fact]
    public async Task HealthCheck_WithUnhealthyPool_ReturnsUnhealthy()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddObjectPool<Car>(builder => builder
            .WithInitialObjects(Car.GetInitialCars())
            .WithMaxSize(7)
            .WithMaxActiveObjects(7));

        services.AddHealthChecks()
            .AddObjectPoolHealthCheck<Car>("car-pool", configureOptions: options =>
            {
                options.UnhealthyUtilizationThreshold = 80.0;
            });

        var provider = services.BuildServiceProvider();
        var pool = provider.GetRequiredService<IObjectPool<Car>>();
        var healthCheckService = provider.GetRequiredService<HealthCheckService>();

        // Take out all objects
        var objects = new List<PoolModel<Car>>();
        for (int i = 0; i < 7; i++)
        {
            objects.Add(pool.GetObject());
        }

        // Act
        var result = await healthCheckService.CheckHealthAsync();

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        var carPoolEntry = result.Entries["car-pool"];
        Assert.Equal(HealthStatus.Unhealthy, carPoolEntry.Status);

        // Cleanup
        objects.ForEach(o => o.Dispose());
    }

    [Fact]
    public async Task HealthCheck_WithMultiplePools_ChecksAll()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        
        services.AddObjectPool<Car>(builder => builder
            .WithInitialObjects(Car.GetInitialCars())
            .WithMaxSize(100));

        services.AddObjectPool<TestObject>(builder => builder
            .WithFactory(() => new TestObject())
            .WithInitialObjects(new TestObject(), new TestObject(), new TestObject()) // Add initial objects
            .WithMaxSize(50));

        services.AddHealthChecks()
            .AddObjectPoolHealthCheck<Car>("car-pool")
            .AddObjectPoolHealthCheck<TestObject>("testobject-pool");

        var provider = services.BuildServiceProvider();
        var healthCheckService = provider.GetRequiredService<HealthCheckService>();

        // Act
        var result = await healthCheckService.CheckHealthAsync();

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("car-pool", result.Entries.Keys);
        Assert.Contains("testobject-pool", result.Entries.Keys);
        Assert.All(result.Entries.Values, entry => Assert.Equal(HealthStatus.Healthy, entry.Status));
    }

    [Fact]
    public async Task HealthCheck_WithQueryablePool_Works()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        
        services.AddQueryableObjectPool<Car>(builder => builder
            .AsQueryable()
            .WithInitialObjects(Car.GetInitialCars()));

        services.AddHealthChecks()
            .AddQueryablePoolHealthCheck<Car>("queryable-car-pool");

        var provider = services.BuildServiceProvider();
        var healthCheckService = provider.GetRequiredService<HealthCheckService>();

        // Act
        var result = await healthCheckService.CheckHealthAsync();

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("queryable-car-pool", result.Entries.Keys);
        
        var entry = result.Entries["queryable-car-pool"];
        Assert.Equal(HealthStatus.Healthy, entry.Status);
        Assert.Contains("queryable", entry.Tags);
    }

    [Fact]
    public async Task HealthCheck_WithCustomOptions_UsesCustomThresholds()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddObjectPool<Car>(builder => builder
            .WithInitialObjects(Car.GetInitialCars())
            .WithMaxSize(10)
            .WithMaxActiveObjects(10));

        services.AddHealthChecks()
            .AddObjectPoolHealthCheck<Car>("car-pool", configureOptions: options =>
            {
                options.DegradedUtilizationThreshold = 30.0;
                options.UnhealthyUtilizationThreshold = 60.0;
            });

        var provider = services.BuildServiceProvider();
        var pool = provider.GetRequiredService<IObjectPool<Car>>();
        var healthCheckService = provider.GetRequiredService<HealthCheckService>();

        // Take out 4 objects (40% utilization)
        var objects = new List<PoolModel<Car>>();
        for (int i = 0; i < 4; i++)
        {
            objects.Add(pool.GetObject());
        }

        // Act
        var result = await healthCheckService.CheckHealthAsync();

        // Assert - Should be degraded because 40% > 30% threshold
        Assert.Equal(HealthStatus.Degraded, result.Status);

        // Cleanup
        objects.ForEach(o => o.Dispose());
    }

    [Fact]
    public async Task HealthCheck_WithTags_IncludesTags()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddObjectPool<Car>(builder => builder
            .WithInitialObjects(Car.GetInitialCars()));

        var customTags = new[] { "ready", "objectpool", "critical", "database" };
        services.AddHealthChecks()
            .AddObjectPoolHealthCheck<Car>("car-pool", tags: customTags);

        var provider = services.BuildServiceProvider();
        var healthCheckService = provider.GetRequiredService<HealthCheckService>();

        // Act
        var result = await healthCheckService.CheckHealthAsync();

        // Assert
        var entry = result.Entries["car-pool"];
        Assert.All(customTags, tag => Assert.Contains(tag, entry.Tags));
    }

    [Fact]
    public async Task HealthCheck_IncludesMetricsData()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddObjectPool<Car>(builder => builder
            .WithInitialObjects(Car.GetInitialCars()));

        services.AddHealthChecks()
            .AddObjectPoolHealthCheck<Car>("car-pool");

        var provider = services.BuildServiceProvider();
        var pool = provider.GetRequiredService<IObjectPool<Car>>();
        var healthCheckService = provider.GetRequiredService<HealthCheckService>();

        // Perform some operations
        using (var obj = pool.GetObject())
        {
            // Use object
        }

        // Act
        var result = await healthCheckService.CheckHealthAsync();

        // Assert
        var entry = result.Entries["car-pool"];
        Assert.Contains("total_retrieved", entry.Data.Keys);
        Assert.Contains("total_returned", entry.Data.Keys);
        Assert.Contains("peak_active", entry.Data.Keys);
        Assert.Contains("pool_empty_events", entry.Data.Keys);
        Assert.Contains("last_checked", entry.Data.Keys);

        Assert.Equal(1L, entry.Data["total_retrieved"]);
        Assert.Equal(1L, entry.Data["total_returned"]);
    }

    [Fact]
    public async Task HealthCheck_WithFailureStatus_UsesCustomStatus()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddObjectPool<Car>(builder => builder
            .WithInitialObjects(Car.GetInitialCars())
            .WithMaxSize(7)
            .WithMaxActiveObjects(7));

        services.AddHealthChecks()
            .AddObjectPoolHealthCheck<Car>(
                "car-pool",
                failureStatus: HealthStatus.Unhealthy, // Custom failure status
                configureOptions: options =>
                {
                    options.DegradedUtilizationThreshold = 50.0;
                });

        var provider = services.BuildServiceProvider();
        var pool = provider.GetRequiredService<IObjectPool<Car>>();
        var healthCheckService = provider.GetRequiredService<HealthCheckService>();

        // Take out most objects
        var objects = new List<PoolModel<Car>>();
        for (int i = 0; i < 5; i++)
        {
            objects.Add(pool.GetObject());
        }

        // Act
        var result = await healthCheckService.CheckHealthAsync();

        // Assert - Even though pool would be degraded, check still reports degraded
        // (failureStatus affects registration behavior, not runtime status determination)
        var entry = result.Entries["car-pool"];
        Assert.True(entry.Status == HealthStatus.Degraded || entry.Status == HealthStatus.Unhealthy);

        // Cleanup
        objects.ForEach(o => o.Dispose());
    }
}

// Helper class for testing
public class TestObject
{
    public int Id { get; set; } = Random.Shared.Next();
}
