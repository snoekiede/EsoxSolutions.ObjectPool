using EsoxSolutions.ObjectPool.DependencyInjection;
using EsoxSolutions.ObjectPool.Models;
using EsoxSolutions.ObjectPool.Pools;
using EsoxSolutions.ObjectPool.Tests.Models;
using EsoxSolutions.ObjectPool.Warmup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EsoxSolutions.ObjectPool.Tests.Warmup;

public class PoolWarmupTests
{
    [Fact]
    public async Task WarmUpAsync_WithTargetSize_CreatesObjects()
    {
        // Arrange
        var pool = new DynamicObjectPool<Car>(() => new Car("Test", "Model"));

        // Act
        await pool.WarmUpAsync(10);

        // Assert
        Assert.Equal(10, pool.AvailableObjectCount);
        
        var status = pool.GetWarmupStatus();
        Assert.True(status.IsWarmedUp);
        Assert.Equal(10, status.ObjectsCreated);
        Assert.Equal(10, status.TargetSize);
        Assert.NotNull(status.CompletedAt);
    }

    [Fact]
    public async Task WarmUpAsync_WithExistingObjects_OnlyCreatesNeeded()
    {
        // Arrange
        var initialCars = new List<Car>
        {
            new("Ford", "Focus"),
            new("Toyota", "Corolla")
        };
        var pool = new DynamicObjectPool<Car>(() => new Car("New", "Model"), initialCars);

        // Act
        await pool.WarmUpAsync(5);

        // Assert
        Assert.Equal(5, pool.AvailableObjectCount);
        
        var status = pool.GetWarmupStatus();
        Assert.Equal(3, status.ObjectsCreated); // Only created 3 more
    }

    [Fact]
    public async Task WarmUpAsync_ExceedingMaxSize_RespectsLimit()
    {
        // Arrange
        var config = new PoolConfiguration { MaxPoolSize = 10 };
        var pool = new DynamicObjectPool<Car>(() => new Car("Test", "Model"), config);

        // Act
        await pool.WarmUpAsync(20); // Try to create more than max

        // Assert
        Assert.Equal(10, pool.AvailableObjectCount); // Limited to max size
    }

    [Fact]
    public async Task WarmUpToPercentageAsync_CreatesCorrectAmount()
    {
        // Arrange
        var config = new PoolConfiguration { MaxPoolSize = 100 };
        var pool = new DynamicObjectPool<Car>(() => new Car("Test", "Model"), config);

        // Act
        await pool.WarmUpToPercentageAsync(50); // 50% of 100 = 50

        // Assert
        Assert.Equal(50, pool.AvailableObjectCount);
        
        var status = pool.GetWarmupStatus();
        Assert.Equal(50, status.ObjectsCreated);
    }

    [Fact]
    public async Task WarmUpToPercentageAsync_InvalidPercentage_ThrowsException()
    {
        // Arrange
        var pool = new DynamicObjectPool<Car>(() => new Car("Test", "Model"));

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => 
            pool.WarmUpToPercentageAsync(-10));
        
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => 
            pool.WarmUpToPercentageAsync(150));
    }

    [Fact]
    public async Task WarmUpAsync_WithCancellation_StopsEarly()
    {
        // Arrange
        var pool = new DynamicObjectPool<Car>(() =>
        {
            Thread.Sleep(50); // Simulate slow creation
            return new Car("Test", "Model");
        });

        var cts = new CancellationTokenSource();
        cts.CancelAfter(100); // Cancel after 100ms

        // Act
        await pool.WarmUpAsync(100, cts.Token);

        // Assert
        Assert.True(pool.AvailableObjectCount < 100); // Should be less than target
    }

    [Fact]
    public async Task WarmUpAsync_WithFactoryErrors_TracksErrors()
    {
        // Arrange
        var callCount = 0;
        var pool = new DynamicObjectPool<Car>(() =>
        {
            callCount++;
            if (callCount % 3 == 0)
                throw new InvalidOperationException("Factory error");
            return new Car("Test", "Model");
        });

        // Act
        await pool.WarmUpAsync(10);

        // Assert
        var status = pool.GetWarmupStatus();
        Assert.NotEmpty(status.Errors);
        Assert.True(status.ObjectsCreated < 10); // Some failed
    }

    [Fact]
    public async Task WarmUpAsync_NoFactory_LogsWarning()
    {
        // Arrange
        var pool = new DynamicObjectPool<Car>([new Car("Test", "Model")]);

        // Act
        await pool.WarmUpAsync(10);

        // Assert
        var status = pool.GetWarmupStatus();
        Assert.NotEmpty(status.Errors);
        Assert.Contains("No factory method available", status.Errors[0]);
    }

    [Fact]
    public async Task WarmUpAsync_AlreadyAtTarget_DoesNothing()
    {
        // Arrange
        var initialCars = Enumerable.Range(0, 10)
            .Select(i => new Car($"Car{i}", "Model"))
            .ToList();
        var pool = new DynamicObjectPool<Car>(() => new Car("New", "Model"), initialCars);

        // Act
        await pool.WarmUpAsync(10);

        // Assert
        Assert.Equal(10, pool.AvailableObjectCount);
        
        var status = pool.GetWarmupStatus();
        Assert.True(status.IsWarmedUp);
        Assert.Equal(0, status.ObjectsCreated); // Didn't need to create any
    }

    [Fact]
    public async Task WarmUpAsync_ParallelCreation_WorksCorrectly()
    {
        // Arrange
        var pool = new DynamicObjectPool<Car>(() => new Car("Test", "Model"));

        // Act
        var tasks = new[]
        {
            pool.WarmUpAsync(25),
            pool.WarmUpAsync(25),
            pool.WarmUpAsync(25)
        };
        await Task.WhenAll(tasks);

        // Assert
        Assert.True(pool.AvailableObjectCount >= 25);
    }

    [Fact]
    public void GetWarmupStatus_BeforeWarmup_ReturnsDefault()
    {
        // Arrange
        var pool = new DynamicObjectPool<Car>(() => new Car("Test", "Model"));

        // Act
        var status = pool.GetWarmupStatus();

        // Assert
        Assert.False(status.IsWarmedUp);
        Assert.Equal(0, status.ObjectsCreated);
        Assert.Equal(0, status.TargetSize);
        Assert.Null(status.CompletedAt);
    }

    [Fact]
    public async Task WarmUpAsync_TracksProgress()
    {
        // Arrange
        var pool = new DynamicObjectPool<Car>(() => new Car("Test", "Model"));

        // Act
        await pool.WarmUpAsync(10);

        // Assert
        var status = pool.GetWarmupStatus();
        Assert.Equal(100.0, status.ProgressPercentage);
        Assert.True(status.WarmupDuration > TimeSpan.Zero);
    }

    [Fact]
    public async Task WithAutoWarmup_WarmsUpOnStartup()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        
        services.AddSingleton<IObjectPoolWarmer<Car>>(sp =>
            new DynamicObjectPool<Car>(() => new Car("Test", "Model")));

        services.WithAutoWarmup<Car>(10);

        var provider = services.BuildServiceProvider();
        var hostedServices = provider.GetServices<IHostedService>();

        // Act
        foreach (var service in hostedServices)
        {
            await service.StartAsync(CancellationToken.None);
        }

        // Assert
        var pool = provider.GetRequiredService<IObjectPoolWarmer<Car>>();
        var status = pool.GetWarmupStatus();
        Assert.True(status.IsWarmedUp);
        Assert.Equal(10, status.ObjectsCreated);
    }

    [Fact]
    public async Task WithAutoWarmupPercentage_WarmsUpCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        
        var config = new PoolConfiguration { MaxPoolSize = 100 };
        services.AddSingleton<IObjectPoolWarmer<Car>>(sp =>
            new DynamicObjectPool<Car>(() => new Car("Test", "Model"), config));

        services.WithAutoWarmupPercentage<Car>(50);

        var provider = services.BuildServiceProvider();
        var hostedServices = provider.GetServices<IHostedService>();

        // Act
        foreach (var service in hostedServices)
        {
            await service.StartAsync(CancellationToken.None);
        }

        // Assert
        var pool = provider.GetRequiredService<IObjectPoolWarmer<Car>>();
        var status = pool.GetWarmupStatus();
        Assert.True(status.IsWarmedUp);
        Assert.Equal(50, status.ObjectsCreated);
    }

    [Fact]
    public void WithAutoWarmupPercentage_InvalidPercentage_ThrowsException()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            services.WithAutoWarmupPercentage<Car>(-10));
        
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            services.WithAutoWarmupPercentage<Car>(150));
    }

    [Fact]
    public async Task ConfigurePoolWarmup_MultiplePools_WarmsUpAll()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddSingleton<IObjectPoolWarmer<Car>>(sp =>
            new DynamicObjectPool<Car>(() => new Car("Test", "Model")));

        services.AddSingleton<IObjectPoolWarmer<TestObject>>(sp =>
            new DynamicObjectPool<TestObject>(() => new TestObject()));

        services.ConfigurePoolWarmup(warmup =>
        {
            warmup.WarmupPool<Car>(targetSize: 5);
            warmup.WarmupPool<TestObject>(targetSize: 10);
        });

        var provider = services.BuildServiceProvider();
        var hostedServices = provider.GetServices<IHostedService>();

        // Act
        foreach (var service in hostedServices)
        {
            await service.StartAsync(CancellationToken.None);
        }

        // Assert
        var carPool = provider.GetRequiredService<IObjectPoolWarmer<Car>>();
        var testPool = provider.GetRequiredService<IObjectPoolWarmer<TestObject>>();

        Assert.True(carPool.GetWarmupStatus().IsWarmedUp);
        Assert.Equal(5, carPool.GetWarmupStatus().ObjectsCreated);
        
        Assert.True(testPool.GetWarmupStatus().IsWarmedUp);
        Assert.Equal(10, testPool.GetWarmupStatus().ObjectsCreated);
    }
}

// Helper class for testing
public class TestObject
{
    public int Id { get; set; } = Random.Shared.Next();
}
