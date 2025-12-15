using EsoxSolutions.ObjectPool.DependencyInjection;
using EsoxSolutions.ObjectPool.Interfaces;
using EsoxSolutions.ObjectPool.Models;
using EsoxSolutions.ObjectPool.Tests.Models;
using Microsoft.Extensions.DependencyInjection;

namespace EsoxSolutions.ObjectPool.Tests.DependencyInjection;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddObjectPool_WithFactory_RegistersPool()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddObjectPool<Car>(builder => builder
            .WithFactory(() => new Car("Ford", "Focus"))
            .WithMaxSize(10));

        var provider = services.BuildServiceProvider();
        var pool = provider.GetService<IObjectPool<Car>>();

        // Assert
        Assert.NotNull(pool);
        
        // Should be able to get an object
        using var obj = pool.GetObject();
        Assert.Equal("Ford", obj.Unwrap().Make);
    }

    [Fact]
    public void AddObjectPool_WithInitialObjects_RegistersPool()
    {
        // Arrange
        var services = new ServiceCollection();
        var initialCars = Car.GetInitialCars();

        // Act
        services.AddObjectPool<Car>(builder => builder
            .WithInitialObjects(initialCars)
            .WithMaxSize(10));

        var provider = services.BuildServiceProvider();
        var pool = provider.GetService<IObjectPool<Car>>();

        // Assert
        Assert.NotNull(pool);
        Assert.Equal(7, pool.AvailableObjectCount);
    }

    [Fact]
    public void AddObjectPool_WithValidation_ValidatesObjects()
    {
        // Arrange
        var services = new ServiceCollection();
        var initialCars = new List<Car>
        {
            new("Ford", "Focus"),
            new("Toyota", "Corolla")
        };

        // Act
        services.AddQueryableObjectPool<Car>(builder => builder
            .AsQueryable()
            .WithInitialObjects(initialCars)
            .WithValidation(car => car.Make == "Ford"));

        var provider = services.BuildServiceProvider();
        var pool = provider.GetService<IQueryableObjectPool<Car>>();

        // Assert
        Assert.NotNull(pool);
        
        // Get Toyota and return it - should be rejected
        var toyota = pool.GetObject(x => x.Make == "Toyota");
        toyota.Dispose();
        
        // Pool should only have Ford now
        Assert.Equal(1, pool.AvailableObjectCount);
    }

    [Fact]
    public void AddDynamicObjectPool_WithServiceProvider_CreatesObjects()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ICarFactory, CarFactory>();

        // Act
        services.AddDynamicObjectPool<Car>(
            sp => sp.GetRequiredService<ICarFactory>().CreateCar(),
            config => config.MaxPoolSize = 20);

        var provider = services.BuildServiceProvider();
        var pool = provider.GetService<IObjectPool<Car>>();

        // Assert
        Assert.NotNull(pool);
        
        using var obj = pool.GetObject();
        Assert.Equal("Dynamic", obj.Unwrap().Make);
    }

    [Fact]
    public void AddQueryableObjectPool_RegistersQueryablePool()
    {
        // Arrange
        var services = new ServiceCollection();
        var initialCars = Car.GetInitialCars();

        // Act
        services.AddQueryableObjectPool<Car>(builder => builder
            .AsQueryable()
            .WithInitialObjects(initialCars));

        var provider = services.BuildServiceProvider();
        var pool = provider.GetService<IQueryableObjectPool<Car>>();

        // Assert
        Assert.NotNull(pool);
        
        // Should support queries
        using var ford = pool.GetObject(c => c.Make == "Ford");
        Assert.Equal("Ford", ford.Unwrap().Make);
    }

    [Fact]
    public void AddObjectPoolWithObjects_RegistersPoolWithInitialObjects()
    {
        // Arrange
        var services = new ServiceCollection();
        var initialCars = Car.GetInitialCars();

        // Act
        services.AddObjectPoolWithObjects(initialCars, config =>
        {
            config.MaxActiveObjects = 5;
        });

        var provider = services.BuildServiceProvider();
        var pool = provider.GetService<IObjectPool<Car>>();

        // Assert
        Assert.NotNull(pool);
        Assert.Equal(7, pool.AvailableObjectCount);
    }

    [Fact]
    public void AddObjectPools_RegistersMultiplePools()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddObjectPools(pools =>
        {
            pools.AddPool<Car>(builder => builder
                .WithFactory(() => new Car("Ford", "Focus"))
                .WithMaxSize(10));

            pools.AddPool<TestObject>(builder => builder
                .WithFactory(() => new TestObject())
                .WithMaxSize(5));
        });

        var provider = services.BuildServiceProvider();
        var carPool = provider.GetService<IObjectPool<Car>>();
        var testPool = provider.GetService<IObjectPool<TestObject>>();

        // Assert
        Assert.NotNull(carPool);
        Assert.NotNull(testPool);
    }

    [Fact]
    public void ObjectPoolBuilder_WithMaxSize_SetsConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddObjectPool<TestObject>(builder => builder
            .WithFactory(() => new TestObject())
            .WithMaxSize(50)
            .WithMaxActiveObjects(25)
            .WithDefaultTimeout(TimeSpan.FromSeconds(10)));

        var provider = services.BuildServiceProvider();
        var pool = provider.GetService<IObjectPool<TestObject>>();

        // Assert
        Assert.NotNull(pool);
        
        // Test max active limit
        var objects = new List<PoolModel<TestObject>>();
        for (int i = 0; i < 25; i++)
        {
            objects.Add(pool.GetObject());
        }
        
        // Next one should throw
        Assert.Throws<InvalidOperationException>(() => pool.GetObject());
        
        // Cleanup
        objects.ForEach(o => o.Dispose());
    }

    [Fact]
    public void ObjectPoolBuilder_Configure_AppliesConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddObjectPool<TestObject>(builder => builder
            .WithFactory(() => new TestObject())
            .Configure(config =>
            {
                config.MaxPoolSize = 100;
                config.MaxActiveObjects = 50;
                config.DefaultTimeout = TimeSpan.FromMinutes(1);
            }));

        var provider = services.BuildServiceProvider();
        var pool = provider.GetService<IObjectPool<TestObject>>();

        // Assert
        Assert.NotNull(pool);
    }

    [Fact]
    public async Task AddObjectPool_WithAsyncRetrieval_Works()
    {
        // Arrange
        var services = new ServiceCollection();
        
        services.AddObjectPool<Car>(builder => builder
            .WithInitialObjects(new Car("Ford", "Focus"))
            .WithDefaultTimeout(TimeSpan.FromSeconds(5)));

        var provider = services.BuildServiceProvider();
        var pool = provider.GetService<IObjectPool<Car>>();

        // Act
        using var obj = await pool!.GetObjectAsync();

        // Assert
        Assert.NotNull(obj);
        Assert.Equal("Ford", obj.Unwrap().Make);
    }

    [Fact]
    public void ObjectPoolBuilder_WithInitialObjectsParams_Works()
    {
        // Arrange
        var services = new ServiceCollection();
        var car1 = new Car("Ford", "Focus");
        var car2 = new Car("Ford", "Fiesta");

        // Act
        services.AddObjectPool<Car>(builder => builder
            .WithInitialObjects(car1, car2));

        var provider = services.BuildServiceProvider();
        var pool = provider.GetService<IObjectPool<Car>>();

        // Assert
        Assert.NotNull(pool);
        Assert.Equal(2, pool.AvailableObjectCount);
    }

    [Fact]
    public void AddObjectPool_MultipleCalls_RegistersSamePool()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act - TryAddSingleton should prevent duplicate registration
        services.AddObjectPool<Car>(builder => builder
            .WithFactory(() => new Car("Ford", "Focus")));
        
        services.AddObjectPool<Car>(builder => builder
            .WithFactory(() => new Car("Toyota", "Corolla")));

        var provider = services.BuildServiceProvider();
        var pools = provider.GetServices<IObjectPool<Car>>().ToList();

        // Assert - Should only have one pool (first registration wins)
        Assert.Single(pools);
    }
}

// Helper classes for testing
public interface ICarFactory
{
    Car CreateCar();
}

public class CarFactory : ICarFactory
{
    public Car CreateCar() => new("Dynamic", "TestModel");
}

public class TestObject
{
    public int Id { get; set; } = Random.Shared.Next();
}
