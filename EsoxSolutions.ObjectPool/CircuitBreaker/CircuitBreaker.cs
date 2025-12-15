using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace EsoxSolutions.ObjectPool.CircuitBreaker;

/// <summary>
/// Circuit breaker implementation for protecting against cascading failures
/// </summary>
public class CircuitBreaker : IDisposable
{
    private readonly CircuitBreakerConfiguration _configuration;
    private readonly ILogger? _logger;
    private readonly CircuitBreakerStatistics _statistics = new();
    private readonly ConcurrentQueue<(DateTime Timestamp, bool Success)> _recentOperations = new();
    private readonly object _stateLock = new();
    private Timer? _recoveryTimer;
    private bool _disposed;

    /// <summary>
    /// Creates a new circuit breaker
    /// </summary>
    /// <param name="configuration">Circuit breaker configuration</param>
    /// <param name="logger">Optional logger</param>
    public CircuitBreaker(CircuitBreakerConfiguration configuration, ILogger? logger = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger;

        _logger?.LogInformation(
            "Circuit breaker initialized: FailureThreshold={Threshold}, OpenDuration={Duration}, SuccessThreshold={Success}",
            _configuration.FailureThreshold,
            _configuration.OpenDuration,
            _configuration.SuccessThreshold);
    }

    /// <summary>
    /// Gets the current circuit breaker statistics
    /// </summary>
    public CircuitBreakerStatistics GetStatistics()
    {
        lock (_stateLock)
        {
            CleanupOldOperations();
            return _statistics;
        }
    }

    /// <summary>
    /// Executes an operation with circuit breaker protection
    /// </summary>
    /// <typeparam name="T">Return type</typeparam>
    /// <param name="operation">The operation to execute</param>
    /// <returns>The result of the operation</returns>
    /// <exception cref="CircuitBreakerOpenException">Thrown when circuit is open</exception>
    public T Execute<T>(Func<T> operation)
    {
        // Check if circuit allows operation
        if (!TryAcquirePermission())
        {
            lock (_stateLock)
            {
                _statistics.RejectedOperations++;
            }

            throw new CircuitBreakerOpenException(_statistics);
        }

        try
        {
            var result = operation();
            RecordSuccess();
            return result;
        }
        catch (Exception ex)
        {
            RecordFailure(ex);
            throw;
        }
    }

    /// <summary>
    /// Executes an async operation with circuit breaker protection
    /// </summary>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        if (!TryAcquirePermission())
        {
            _statistics.RejectedOperations++;
            throw new CircuitBreakerOpenException(_statistics);
        }

        try
        {
            var result = await operation();
            RecordSuccess();
            return result;
        }
        catch (Exception ex)
        {
            RecordFailure(ex);
            throw;
        }
    }

    /// <summary>
    /// Tries to execute an operation, returning success status
    /// </summary>
    public bool TryExecute<T>(Func<T> operation, out T? result)
    {
        result = default;

        if (!TryAcquirePermission())
        {
            _statistics.RejectedOperations++;
            return false;
        }

        try
        {
            result = operation();
            RecordSuccess();
            return true;
        }
        catch (Exception ex)
        {
            RecordFailure(ex);
            return false;
        }
    }

    /// <summary>
    /// Checks if the circuit allows operations
    /// </summary>
    private bool TryAcquirePermission()
    {
        lock (_stateLock)
        {
            CleanupOldOperations();

            switch (_statistics.State)
            {
                case CircuitState.Closed:
                    return true;

                case CircuitState.Open:
                    // Check if we should transition to half-open
                    if (_configuration.EnableAutomaticRecovery &&
                        _statistics.CircuitOpenedAt.HasValue &&
                        DateTime.UtcNow - _statistics.CircuitOpenedAt.Value >= _configuration.OpenDuration)
                    {
                        TransitionToHalfOpen();
                        return true;
                    }
                    return false;

                case CircuitState.HalfOpen:
                    // Allow limited operations in half-open state
                    return true;

                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// Records a successful operation
    /// </summary>
    private void RecordSuccess()
    {
        lock (_stateLock)
        {
            _statistics.TotalOperations++;
            _statistics.SuccessfulOperations++;
            _statistics.ConsecutiveFailures = 0;

            _recentOperations.Enqueue((DateTime.UtcNow, true));

            if (_statistics.State == CircuitState.HalfOpen)
            {
                _statistics.ConsecutiveSuccesses++;

                if (_statistics.ConsecutiveSuccesses >= _configuration.SuccessThreshold)
                {
                    TransitionToClosed();
                }
            }

            _logger?.LogDebug(
                "Circuit breaker success recorded. State: {State}, Success rate: {Rate:F2}%",
                _statistics.State,
                _statistics.SuccessRate);
        }
    }

    /// <summary>
    /// Records a failed operation
    /// </summary>
    private void RecordFailure(Exception exception)
    {
        lock (_stateLock)
        {
            // Check if this exception should count as a failure
            if (_configuration.IsFailureException != null && 
                !_configuration.IsFailureException(exception))
            {
                _logger?.LogDebug("Exception ignored by circuit breaker: {Exception}", exception.GetType().Name);
                return;
            }

            _statistics.TotalOperations++;
            _statistics.FailedOperations++;
            _statistics.ConsecutiveFailures++;
            _statistics.ConsecutiveSuccesses = 0;
            _statistics.LastException = exception;

            _recentOperations.Enqueue((DateTime.UtcNow, false));

            _logger?.LogWarning(
                exception,
                "Circuit breaker failure recorded. Consecutive failures: {Count}, State: {State}",
                _statistics.ConsecutiveFailures,
                _statistics.State);

            // Check if we should open the circuit
            if (_statistics.State == CircuitState.Closed)
            {
                if (ShouldOpenCircuit())
                {
                    TransitionToOpen();
                }
            }
            else if (_statistics.State == CircuitState.HalfOpen)
            {
                // Any failure in half-open state reopens the circuit
                TransitionToOpen();
            }
        }
    }

    /// <summary>
    /// Determines if the circuit should be opened
    /// </summary>
    private bool ShouldOpenCircuit()
    {
        // Check consecutive failures threshold
        if (_statistics.ConsecutiveFailures >= _configuration.FailureThreshold)
        {
            _logger?.LogWarning(
                "Circuit breaker threshold reached: {Count} consecutive failures",
                _statistics.ConsecutiveFailures);
            return true;
        }

        // Check failure percentage threshold
        if (_statistics.TotalOperations >= _configuration.MinimumThroughput)
        {
            if (_statistics.FailurePercentage >= _configuration.FailurePercentageThreshold)
            {
                _logger?.LogWarning(
                    "Circuit breaker failure percentage threshold reached: {Percentage:F2}%",
                    _statistics.FailurePercentage);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Transitions circuit to open state
    /// </summary>
    private void TransitionToOpen()
    {
        _statistics.State = CircuitState.Open;
        _statistics.CircuitOpenedAt = DateTime.UtcNow;
        _statistics.LastStateChange = DateTime.UtcNow;
        _statistics.CircuitOpenCount++;

        _logger?.LogError(
            "Circuit breaker opened. Consecutive failures: {Failures}, Failure rate: {Rate:F2}%",
            _statistics.ConsecutiveFailures,
            _statistics.FailurePercentage);

        _configuration.OnCircuitOpen?.Invoke(_statistics);

        // Set up recovery timer if automatic recovery is enabled
        if (_configuration.EnableAutomaticRecovery)
        {
            _recoveryTimer?.Dispose();
            _recoveryTimer = new Timer(
                _ => TryTransitionToHalfOpen(),
                null,
                _configuration.OpenDuration,
                Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>
    /// Transitions circuit to half-open state
    /// </summary>
    private void TransitionToHalfOpen()
    {
        _statistics.State = CircuitState.HalfOpen;
        _statistics.LastStateChange = DateTime.UtcNow;
        _statistics.ConsecutiveSuccesses = 0;

        _logger?.LogInformation("Circuit breaker entering half-open state for recovery testing");

        _configuration.OnCircuitHalfOpen?.Invoke(_statistics);
    }

    /// <summary>
    /// Transitions circuit to closed state
    /// </summary>
    private void TransitionToClosed()
    {
        _statistics.State = CircuitState.Closed;
        _statistics.LastStateChange = DateTime.UtcNow;
        _statistics.ConsecutiveFailures = 0;
        _statistics.ConsecutiveSuccesses = 0;
        _statistics.CircuitOpenedAt = null;

        _logger?.LogInformation("Circuit breaker closed. Service recovered successfully");

        _configuration.OnCircuitClose?.Invoke(_statistics);
    }

    /// <summary>
    /// Attempts to transition to half-open state (called by timer)
    /// </summary>
    private void TryTransitionToHalfOpen()
    {
        lock (_stateLock)
        {
            if (_statistics.State == CircuitState.Open)
            {
                TransitionToHalfOpen();
            }
        }
    }

    /// <summary>
    /// Cleans up operations outside the failure window
    /// </summary>
    private void CleanupOldOperations()
    {
        var cutoff = DateTime.UtcNow - _configuration.FailureWindow;

        while (_recentOperations.TryPeek(out var operation))
        {
            if (operation.Timestamp < cutoff)
            {
                _recentOperations.TryDequeue(out _);
            }
            else
            {
                break;
            }
        }
    }

    /// <summary>
    /// Manually resets the circuit breaker to closed state
    /// </summary>
    public void Reset()
    {
        lock (_stateLock)
        {
            _logger?.LogInformation("Circuit breaker manually reset");
            TransitionToClosed();
            _recentOperations.Clear();
        }
    }

    /// <summary>
    /// Manually opens the circuit breaker
    /// </summary>
    public void Trip()
    {
        lock (_stateLock)
        {
            _logger?.LogWarning("Circuit breaker manually tripped");
            TransitionToOpen();
        }
    }

    /// <summary>
    /// Disposes the circuit breaker
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _recoveryTimer?.Dispose();
        _disposed = true;

        _logger?.LogInformation("Circuit breaker disposed");
    }
}
