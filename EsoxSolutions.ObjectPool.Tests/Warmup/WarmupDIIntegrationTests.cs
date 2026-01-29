using EsoxSolutions.ObjectPool.DependencyInjection;
using EsoxSolutions.ObjectPool.Tests.Models;
using EsoxSolutions.ObjectPool.Warmup;
using Microsoft.Extensions.DependencyInjection;

namespace EsoxSolutions.ObjectPool.Tests.Warmup;

public class WarmupDIIntegrationTests
{
    [Fact]
    public void AddDynamicObjectPool_RegistersIObjectPoolWarmer()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddDynamicObjectPool<Car>(
            sp => new Car("Test", "Model"),
            config => config.MaxPoolSize = 100);

        var provider = services.BuildServiceProvider();

        // Assert
        var warmer = provider.GetService<IObjectPoolWarmer<Car>>();
        Assert.NotNull(warmer);
    }

    [Fact]
    public async Task WithAutoWarmup_WarmsUpPoolOnStartup()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddDynamicObjectPool<Car>(
            sp => new Car("Test", "Model"),
            config => config.MaxPoolSize = 100)
            .WithAutoWarmup<Car>(10);

        var provider = services.BuildServiceProvider();

        // Trigger hosted service startup
        var hostedServices = provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>();
        foreach (var service in hostedServices)
        {
            await service.StartAsync(CancellationToken.None);
        }

        // Small delay for warm-up to complete
        await Task.Delay(100);

        // Assert
        var warmer = provider.GetRequiredService<IObjectPoolWarmer<Car>>();
        var status = warmer.GetWarmupStatus();

        Assert.True(status.IsWarmedUp);
        Assert.Equal(10, status.ObjectsCreated);
    }

    [Fact]
    public async Task WithAutoWarmupPercentage_WarmsUpPoolOnStartup()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddDynamicObjectPool<Car>(
            sp => new Car("Test", "Model"),
            config => config.MaxPoolSize = 100)
            .WithAutoWarmupPercentage<Car>(50);

        var provider = services.BuildServiceProvider();

        // Trigger hosted service startup
        var hostedServices = provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>();
        foreach (var service in hostedServices)
        {
            await service.StartAsync(CancellationToken.None);
        }

        // Small delay for warm-up to complete
        await Task.Delay(100);

        // Assert
        var warmer = provider.GetRequiredService<IObjectPoolWarmer<Car>>();
        var status = warmer.GetWarmupStatus();

        Assert.True(status.IsWarmedUp);
        Assert.Equal(50, status.ObjectsCreated); // 50% of 100
    }

    [Fact]
    public void CanResolveAllPoolInterfaces()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddDynamicObjectPool<Car>(
            sp => new Car("Test", "Model"),
            config => config.MaxPoolSize = 100);

        var provider = services.BuildServiceProvider();

        // Assert - Core pool interfaces should be resolvable
        var pool = provider.GetService<Interfaces.IObjectPool<Car>>();
        Assert.NotNull(pool);

        var warmer = provider.GetService<IObjectPoolWarmer<Car>>();
        Assert.NotNull(warmer);

        // Pool and warmer should be the same instance
        Assert.Same(pool, warmer);

        // Pool should also implement IPoolMetrics and IPoolHealth
        Assert.IsAssignableFrom<Interfaces.IPoolMetrics>(pool);
        Assert.IsAssignableFrom<Interfaces.IPoolHealth>(pool);
    }
}
