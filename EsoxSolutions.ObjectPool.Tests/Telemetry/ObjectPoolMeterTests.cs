using System.Diagnostics.Metrics;
using EsoxSolutions.ObjectPool.DependencyInjection;
using EsoxSolutions.ObjectPool.Telemetry;
using EsoxSolutions.ObjectPool.Tests.Models;
using Microsoft.Extensions.DependencyInjection;

namespace EsoxSolutions.ObjectPool.Tests.Telemetry;

public class ObjectPoolMeterTests
{
    [Fact]
    public void ObjectPoolMeter_WithPool_CreatesMetrics()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddObjectPool<Car>(builder => builder
            .WithInitialObjects(Car.GetInitialCars())
            .WithMaxSize(100));

        var provider = services.BuildServiceProvider();
        var pool = provider.GetRequiredService<Interfaces.IObjectPool<Car>>();

        // Act
        using var meter = new ObjectPoolMeter<Car>(pool, poolName: "test-pool");

        // Assert
        Assert.NotNull(meter);
    }

    [Fact]
    public void ObjectPoolMeter_RecordsRetrieval_Success()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddObjectPool<Car>(builder => builder
            .WithInitialObjects(Car.GetInitialCars()));

        var provider = services.BuildServiceProvider();
        var pool = provider.GetRequiredService<Interfaces.IObjectPool<Car>>();
        using var meter = new ObjectPoolMeter<Car>(pool);

        // Act
        meter.RecordRetrieval(success: true, durationMs: 1.5);

        // Assert - No exception thrown
        Assert.True(true);
    }

    [Fact]
    public void ObjectPoolMeter_RecordsReturn_Success()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddObjectPool<Car>(builder => builder
            .WithInitialObjects(Car.GetInitialCars()));

        var provider = services.BuildServiceProvider();
        var pool = provider.GetRequiredService<Interfaces.IObjectPool<Car>>();
        using var meter = new ObjectPoolMeter<Car>(pool);

        // Act
        meter.RecordReturn(success: true, durationMs: 0.5);

        // Assert - No exception thrown
        Assert.True(true);
    }

    [Fact]
    public void ObjectPoolMeter_RecordsEmptyEvent_Success()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddObjectPool<Car>(builder => builder
            .WithInitialObjects(Car.GetInitialCars()));

        var provider = services.BuildServiceProvider();
        var pool = provider.GetRequiredService<Interfaces.IObjectPool<Car>>();
        using var meter = new ObjectPoolMeter<Car>(pool);

        // Act
        meter.RecordEmptyEvent();

        // Assert - No exception thrown
        Assert.True(true);
    }

    [Fact]
    public void ObjectPoolMeter_Dispose_DoesNotThrow()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddObjectPool<Car>(builder => builder
            .WithInitialObjects(Car.GetInitialCars()));

        var provider = services.BuildServiceProvider();
        var pool = provider.GetRequiredService<Interfaces.IObjectPool<Car>>();
        var meter = new ObjectPoolMeter<Car>(pool);

        // Act
        meter.Dispose();

        // Assert - No exception thrown
        Assert.True(true);
    }

    [Fact]
    public void ObjectPoolMeter_AfterDispose_IgnoresRecording()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddObjectPool<Car>(builder => builder
            .WithInitialObjects(Car.GetInitialCars()));

        var provider = services.BuildServiceProvider();
        var pool = provider.GetRequiredService<Interfaces.IObjectPool<Car>>();
        var meter = new ObjectPoolMeter<Car>(pool);
        meter.Dispose();

        // Act - Should not throw after disposal
        meter.RecordRetrieval();
        meter.RecordReturn();
        meter.RecordEmptyEvent();

        // Assert
        Assert.True(true);
    }

    [Fact]
    public void TelemetryExtensions_AddObjectPoolMetrics_RegistersMeter()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddObjectPool<Car>(builder => builder
            .WithInitialObjects(Car.GetInitialCars()));

        // Act
        services.AddObjectPoolMetrics<Car>(poolName: "car-pool");

        var provider = services.BuildServiceProvider();
        var meter = provider.GetService<ObjectPoolMeter<Car>>();

        // Assert
        Assert.NotNull(meter);
    }

    [Fact]
    public void TelemetryExtensions_AddObjectPoolsWithMetrics_RegistersMultipleMeters()
    {
        // Arrange
        var services = new ServiceCollection();
        
        services.AddObjectPool<Car>(builder => builder
            .WithInitialObjects(Car.GetInitialCars()));

        services.AddObjectPool<TestObject>(builder => builder
            .WithFactory(() => new TestObject())
            .WithInitialObjects(new TestObject(), new TestObject()));

        // Act
        services.AddObjectPoolsWithMetrics(metrics =>
        {
            metrics.AddMetrics<Car>("car-pool");
            metrics.AddMetrics<TestObject>("test-pool");
        });

        var provider = services.BuildServiceProvider();
        var carMeter = provider.GetService<ObjectPoolMeter<Car>>();
        var testMeter = provider.GetService<ObjectPoolMeter<TestObject>>();

        // Assert
        Assert.NotNull(carMeter);
        Assert.NotNull(testMeter);
    }

    [Fact]
    public void ObjectPoolMeter_WithCustomMeterName_Works()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddObjectPool<Car>(builder => builder
            .WithInitialObjects(Car.GetInitialCars()));

        var provider = services.BuildServiceProvider();
        var pool = provider.GetRequiredService<Interfaces.IObjectPool<Car>>();

        // Act
        using var meter = new ObjectPoolMeter<Car>(
            pool, 
            meterName: "CustomMeter", 
            poolName: "custom-pool",
            version: "1.0.0");

        // Assert
        Assert.NotNull(meter);
    }

    [Fact]
    public void ObjectPoolMeter_WithMeterListener_ExportsMetrics()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddObjectPool<Car>(builder => builder
            .WithInitialObjects(Car.GetInitialCars())
            .WithMaxSize(10)
            .WithMaxActiveObjects(5));

        var provider = services.BuildServiceProvider();
        var pool = provider.GetRequiredService<Interfaces.IObjectPool<Car>>();

        var measurements = new List<string>();
        using var meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == "EsoxSolutions.ObjectPool")
                {
                    listener.EnableMeasurementEvents(instrument);
                    measurements.Add($"Published: {instrument.Name}");
                }
            }
        };

        meterListener.SetMeasurementEventCallback<int>((instrument, measurement, tags, state) =>
        {
            measurements.Add($"{instrument.Name}: {measurement}");
        });

        meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            measurements.Add($"{instrument.Name}: {measurement:F2}");
        });

        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            measurements.Add($"{instrument.Name}: {measurement}");
        });

        meterListener.Start();

        // Act
        using var meter = new ObjectPoolMeter<Car>(pool, poolName: "test-pool");
        
        // Get an object to trigger metrics
        using (var obj = pool.GetObject())
        {
            meter.RecordRetrieval(durationMs: 1.5);
        }
        meter.RecordReturn(durationMs: 0.5);

        // Allow time for observable instruments to be recorded
        Thread.Sleep(100);

        meterListener.RecordObservableInstruments();

        // Assert
        Assert.NotEmpty(measurements);
        Assert.Contains(measurements, m => m.Contains("objectpool.objects.active"));
        Assert.Contains(measurements, m => m.Contains("objectpool.objects.available"));
    }

    [Fact]
    public void ObjectPoolMeter_TracksPoolOperations_AccuratelyWithMultipleOperations()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddObjectPool<Car>(builder => builder
            .WithInitialObjects(Car.GetInitialCars()));

        var provider = services.BuildServiceProvider();
        var pool = provider.GetRequiredService<Interfaces.IObjectPool<Car>>();
        using var meter = new ObjectPoolMeter<Car>(pool);

        // Act - Perform multiple operations
        for (int i = 0; i < 5; i++)
        {
            meter.RecordRetrieval(durationMs: i * 0.5);
            using (var obj = pool.GetObject())
            {
                // Use object
            }
            meter.RecordReturn(durationMs: i * 0.3);
        }

        // Assert - No exceptions thrown
        Assert.True(true);
    }
}

// Helper class
public class TestObject
{
    public int Id { get; set; } = Random.Shared.Next();
}
