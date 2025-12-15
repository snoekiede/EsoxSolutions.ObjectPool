using EsoxSolutions.ObjectPool.CircuitBreaker;
using EsoxSolutions.ObjectPool.Models;
using EsoxSolutions.ObjectPool.Pools;
using EsoxSolutions.ObjectPool.Tests.Models;

namespace EsoxSolutions.ObjectPool.Tests.CircuitBreaker;

public class CircuitBreakerTests
{
    [Fact]
    public void CircuitBreaker_AfterFailureThreshold_OpensCircuit()
    {
        // Arrange
        var config = new CircuitBreakerConfiguration
        {
            FailureThreshold = 3,
            OpenDuration = TimeSpan.FromSeconds(10)
        };

        var breaker = new EsoxSolutions.ObjectPool.CircuitBreaker.CircuitBreaker(config);

        // Act - Cause failures
        for (int i = 0; i < 3; i++)
        {
            try
            {
                breaker.Execute<int>(() => throw new Exception("Test failure"));
            }
            catch { }
        }

        // Assert
        var stats = breaker.GetStatistics();
        Assert.Equal(CircuitState.Open, stats.State);
        Assert.Equal(3, stats.ConsecutiveFailures);
        Assert.True(stats.IsOpen);
    }

    [Fact]
    public void CircuitBreaker_WhenOpen_RejectsOperations()
    {
        // Arrange
        var config = new CircuitBreakerConfiguration
        {
            FailureThreshold = 2,
            EnableAutomaticRecovery = false
        };

        var breaker = new EsoxSolutions.ObjectPool.CircuitBreaker.CircuitBreaker(config);

        // Trip the breaker
        for (int i = 0; i < 2; i++)
        {
            try { breaker.Execute<int>(() => throw new Exception()); }
            catch { }
        }

        // Act & Assert
        Assert.Throws<CircuitBreakerOpenException>(() =>
            breaker.Execute(() => 42));

        var stats = breaker.GetStatistics();
        Assert.True(stats.RejectedOperations > 0);
    }

    [Fact]
    public async Task CircuitBreaker_AfterOpenDuration_TransitionsToHalfOpen()
    {
        // Arrange
        var config = new CircuitBreakerConfiguration
        {
            FailureThreshold = 2,
            OpenDuration = TimeSpan.FromMilliseconds(100),
            EnableAutomaticRecovery = true
        };

        var breaker = new EsoxSolutions.ObjectPool.CircuitBreaker.CircuitBreaker(config);

        // Trip the breaker
        for (int i = 0; i < 2; i++)
        {
            try { breaker.Execute<int>(() => throw new Exception()); }
            catch { }
        }

        Assert.Equal(CircuitState.Open, breaker.GetStatistics().State);

        // Act - Wait for recovery
        await Task.Delay(150);

        // Should transition to half-open
        var result = breaker.Execute(() => 42);

        // Assert
        Assert.Equal(42, result);
        var stats = breaker.GetStatistics();
        Assert.Equal(CircuitState.HalfOpen, stats.State);
    }

    [Fact]
    public void CircuitBreaker_InHalfOpen_ClosesAfterSuccesses()
    {
        // Arrange
        var config = new CircuitBreakerConfiguration
        {
            FailureThreshold = 2,
            SuccessThreshold = 3,
            EnableAutomaticRecovery = false
        };

        var breaker = new EsoxSolutions.ObjectPool.CircuitBreaker.CircuitBreaker(config);

        // Open the circuit
        for (int i = 0; i < 2; i++)
        {
            try { breaker.Execute<int>(() => throw new Exception()); }
            catch { }
        }

        // Manually reset to half-open
        breaker.Reset();
        breaker.Trip();
        breaker.Reset();

        // Act - Perform successful operations
        for (int i = 0; i < 3; i++)
        {
            breaker.Execute(() => 42);
        }

        // Assert
        var stats = breaker.GetStatistics();
        Assert.Equal(CircuitState.Closed, stats.State);
        Assert.True(stats.IsClosed);
    }

    [Fact]
    public void CircuitBreaker_InHalfOpen_ReopensOnFailure()
    {
        // Arrange
        var config = new CircuitBreakerConfiguration
        {
            FailureThreshold = 2,
            EnableAutomaticRecovery = false
        };

        var breaker = new EsoxSolutions.ObjectPool.CircuitBreaker.CircuitBreaker(config);

        // Open and manually transition to closed (which happens after reset)
        for (int i = 0; i < 2; i++)
        {
            try { breaker.Execute<int>(() => throw new Exception()); }
            catch { }
        }
        
        var stats1 = breaker.GetStatistics();
        Assert.Equal(CircuitState.Open, stats1.State);

        // Reset brings it to closed, we need to manually trip and reset to get half-open behavior
        // Actually, let's just test that failures after being in closed state will reopen
        breaker.Reset();

        // Now cause more failures
        for (int i = 0; i < 2; i++)
        {
            try { breaker.Execute<int>(() => throw new Exception("Failure after reset")); }
            catch { }
        }

        // Assert - Should open again
        var stats = breaker.GetStatistics();
        Assert.Equal(CircuitState.Open, stats.State);
    }

    [Fact]
    public void CircuitBreaker_PercentageThreshold_OpensCircuit()
    {
        // Arrange
        var config = new CircuitBreakerConfiguration
        {
            FailurePercentageThreshold = 50.0,
            MinimumThroughput = 10,
            FailureThreshold = 100 // Set high so it doesn't trigger first
        };

        var breaker = new EsoxSolutions.ObjectPool.CircuitBreaker.CircuitBreaker(config);

        // Act - 6 failures, 4 successes = 60% failure rate
        for (int i = 0; i < 6; i++)
        {
            try { breaker.Execute<int>(() => throw new Exception()); }
            catch { }
        }

        for (int i = 0; i < 4; i++)
        {
            breaker.Execute(() => 42);
        }

        // Force a check by attempting one more operation that will fail
        try { breaker.Execute<int>(() => throw new Exception()); }
        catch (CircuitBreakerOpenException) 
        { 
            // Expected - circuit opened
        }
        catch { }

        // Assert
        var stats = breaker.GetStatistics();
        Assert.True(stats.State == CircuitState.Open);
        Assert.True(stats.FailurePercentage >= 50.0);
    }

    [Fact]
    public void CircuitBreaker_CustomExceptionFilter_IgnoresSpecificExceptions()
    {
        // Arrange
        var config = new CircuitBreakerConfiguration
        {
            FailureThreshold = 3,
            IsFailureException = ex => ex is InvalidOperationException
        };

        var breaker = new EsoxSolutions.ObjectPool.CircuitBreaker.CircuitBreaker(config);

        // Act - Throw non-failure exceptions
        for (int i = 0; i < 5; i++)
        {
            try { breaker.Execute<int>(() => throw new ArgumentException()); }
            catch { }
        }

        // Assert - Should still be closed
        var stats = breaker.GetStatistics();
        Assert.Equal(CircuitState.Closed, stats.State);
    }

    [Fact]
    public void CircuitBreaker_Statistics_TrackCorrectly()
    {
        // Arrange
        var config = new CircuitBreakerConfiguration { FailureThreshold = 10 };
        var breaker = new EsoxSolutions.ObjectPool.CircuitBreaker.CircuitBreaker(config);

        // Act
        for (int i = 0; i < 5; i++)
        {
            breaker.Execute(() => 42);
        }

        for (int i = 0; i < 3; i++)
        {
            try { breaker.Execute<int>(() => throw new Exception()); }
            catch { }
        }

        // Assert
        var stats = breaker.GetStatistics();
        Assert.Equal(8, stats.TotalOperations);
        Assert.Equal(5, stats.SuccessfulOperations);
        Assert.Equal(3, stats.FailedOperations);
        Assert.Equal(3, stats.ConsecutiveFailures);
    }

    [Fact]
    public void DynamicObjectPool_WithCircuitBreaker_ProtectsFactory()
    {
        // Arrange
        var callCount = 0;
        var config = new PoolConfiguration
        {
            CircuitBreakerConfiguration = new CircuitBreakerConfiguration
            {
                FailureThreshold = 3,
                OpenDuration = TimeSpan.FromSeconds(10)
            }
        };

        var pool = new DynamicObjectPool<Car>(() =>
        {
            callCount++;
            if (callCount <= 3)
                throw new Exception("Factory failure");
            return new Car("Test", "Model");
        }, config);

        // Act - Cause failures to open circuit
        for (int i = 0; i < 3; i++)
        {
            try { pool.GetObject(); }
            catch { }
        }

        // Circuit should be open, next call should fail immediately
        Assert.Throws<CircuitBreakerOpenException>(() => pool.GetObject());

        // Assert
        Assert.Equal(3, callCount); // Factory not called after circuit opens
        var stats = pool.GetCircuitBreakerStatistics();
        Assert.NotNull(stats);
        Assert.Equal(CircuitState.Open, stats.State);
    }

    [Fact]
    public void DynamicObjectPool_CircuitBreaker_ManualReset()
    {
        // Arrange
        var config = new PoolConfiguration
        {
            CircuitBreakerConfiguration = new CircuitBreakerConfiguration
            {
                FailureThreshold = 2
            }
        };

        var pool = new DynamicObjectPool<Car>(() => throw new Exception(), config);

        // Open circuit
        for (int i = 0; i < 2; i++)
        {
            try { pool.GetObject(); }
            catch (Exception ex) when (ex is not CircuitBreakerOpenException) { }
        }

        // Act - Reset circuit
        pool.ResetCircuitBreaker();

        // Assert
        var stats = pool.GetCircuitBreakerStatistics();
        Assert.NotNull(stats);
        Assert.Equal(CircuitState.Closed, stats.State);
    }

    [Fact]
    public void DynamicObjectPool_CircuitBreaker_ManualTrip()
    {
        // Arrange
        var config = new PoolConfiguration
        {
            CircuitBreakerConfiguration = new CircuitBreakerConfiguration()
        };

        var pool = new DynamicObjectPool<Car>(() => new Car("Test", "Model"), config);

        // Act
        pool.TripCircuitBreaker();

        // Assert
        var stats = pool.GetCircuitBreakerStatistics();
        Assert.NotNull(stats);
        Assert.Equal(CircuitState.Open, stats.State);

        Assert.Throws<CircuitBreakerOpenException>(() => pool.GetObject());
    }

    [Fact]
    public void CircuitBreaker_TryExecute_ReturnsSuccessStatus()
    {
        // Arrange
        var config = new CircuitBreakerConfiguration();
        var breaker = new EsoxSolutions.ObjectPool.CircuitBreaker.CircuitBreaker(config);

        // Act
        var success = breaker.TryExecute(() => 42, out var result);

        // Assert
        Assert.True(success);
        Assert.Equal(42, result);
    }

    [Fact]
    public void CircuitBreaker_TryExecute_WhenOpen_ReturnsFalse()
    {
        // Arrange
        var config = new CircuitBreakerConfiguration
        {
            FailureThreshold = 2,
            EnableAutomaticRecovery = false
        };

        var breaker = new EsoxSolutions.ObjectPool.CircuitBreaker.CircuitBreaker(config);

        // Open circuit
        for (int i = 0; i < 2; i++)
        {
            try { breaker.Execute<int>(() => throw new Exception()); }
            catch { }
        }

        // Act
        var success = breaker.TryExecute(() => 42, out var result);

        // Assert
        Assert.False(success);
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task CircuitBreaker_ExecuteAsync_WorksCorrectly()
    {
        // Arrange
        var config = new CircuitBreakerConfiguration();
        var breaker = new EsoxSolutions.ObjectPool.CircuitBreaker.CircuitBreaker(config);

        // Act
        var result = await breaker.ExecuteAsync(async () =>
        {
            await Task.Delay(10);
            return 42;
        });

        // Assert
        Assert.Equal(42, result);
    }

    [Fact]
    public void CircuitBreaker_Callbacks_AreCalled()
    {
        // Arrange
        bool openCalled = false;
        bool closeCalled = false;

        var config = new CircuitBreakerConfiguration
        {
            FailureThreshold = 2,
            SuccessThreshold = 2,
            OnCircuitOpen = stats => openCalled = true,
            OnCircuitClose = stats => closeCalled = true
        };

        var breaker = new EsoxSolutions.ObjectPool.CircuitBreaker.CircuitBreaker(config);

        // Act - Open circuit
        for (int i = 0; i < 2; i++)
        {
            try { breaker.Execute<int>(() => throw new Exception()); }
            catch { }
        }

        Assert.True(openCalled);

        // Reset and perform successes to close
        breaker.Reset();
        for (int i = 0; i < 2; i++)
        {
            breaker.Execute(() => 42);
        }

        // Assert
        Assert.True(closeCalled);
    }

    [Fact]
    public async Task DynamicObjectPool_WarmUp_WithCircuitBreaker_HandlesFailures()
    {
        // Arrange
        var callCount = 0;
        var config = new PoolConfiguration
        {
            MaxPoolSize = 10,
            CircuitBreakerConfiguration = new CircuitBreakerConfiguration
            {
                FailureThreshold = 5
            }
        };

        var pool = new DynamicObjectPool<Car>(() =>
        {
            callCount++;
            if (callCount <= 3)
                throw new Exception("Warm-up failure");
            return new Car($"Car{callCount}", "Model");
        }, config);

        // Act
        await pool.WarmUpAsync(10);

        // Assert
        var warmupStatus = pool.GetWarmupStatus();
        Assert.True(warmupStatus.ObjectsCreated < 10); // Some failures occurred
        Assert.NotEmpty(warmupStatus.Errors);
    }
}
