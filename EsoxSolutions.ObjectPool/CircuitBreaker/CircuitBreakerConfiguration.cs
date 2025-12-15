namespace EsoxSolutions.ObjectPool.CircuitBreaker;

/// <summary>
/// Circuit breaker states
/// </summary>
public enum CircuitState
{
    /// <summary>
    /// Circuit is closed, operations proceed normally
    /// </summary>
    Closed = 0,

    /// <summary>
    /// Circuit is open, operations are blocked
    /// </summary>
    Open = 1,

    /// <summary>
    /// Circuit is half-open, allowing limited operations to test recovery
    /// </summary>
    HalfOpen = 2
}

/// <summary>
/// Configuration for circuit breaker
/// </summary>
public class CircuitBreakerConfiguration
{
    /// <summary>
    /// Number of consecutive failures before opening the circuit
    /// </summary>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>
    /// Time window for counting failures
    /// </summary>
    public TimeSpan FailureWindow { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// How long to keep the circuit open before attempting recovery
    /// </summary>
    public TimeSpan OpenDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Number of successful operations required in half-open state to close the circuit
    /// </summary>
    public int SuccessThreshold { get; set; } = 3;

    /// <summary>
    /// Percentage of failures that triggers circuit open
    /// </summary>
    public double FailurePercentageThreshold { get; set; } = 50.0;

    /// <summary>
    /// Minimum number of operations before calculating failure percentage
    /// </summary>
    public int MinimumThroughput { get; set; } = 10;

    /// <summary>
    /// Whether to enable automatic recovery attempts
    /// </summary>
    public bool EnableAutomaticRecovery { get; set; } = true;

    /// <summary>
    /// Custom exception predicate to determine if an exception should count as a failure
    /// </summary>
    public Func<Exception, bool>? IsFailureException { get; set; }

    /// <summary>
    /// Action to execute when circuit opens
    /// </summary>
    public Action<CircuitBreakerStatistics>? OnCircuitOpen { get; set; }

    /// <summary>
    /// Action to execute when circuit closes
    /// </summary>
    public Action<CircuitBreakerStatistics>? OnCircuitClose { get; set; }

    /// <summary>
    /// Action to execute when circuit enters half-open state
    /// </summary>
    public Action<CircuitBreakerStatistics>? OnCircuitHalfOpen { get; set; }
}

/// <summary>
/// Statistics for circuit breaker operations
/// </summary>
public class CircuitBreakerStatistics
{
    /// <summary>
    /// Current state of the circuit
    /// </summary>
    public CircuitState State { get; set; } = CircuitState.Closed;

    /// <summary>
    /// Total number of operations attempted
    /// </summary>
    public long TotalOperations { get; set; }

    /// <summary>
    /// Total number of successful operations
    /// </summary>
    public long SuccessfulOperations { get; set; }

    /// <summary>
    /// Total number of failed operations
    /// </summary>
    public long FailedOperations { get; set; }

    /// <summary>
    /// Number of consecutive failures
    /// </summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>
    /// Number of consecutive successes (in half-open state)
    /// </summary>
    public int ConsecutiveSuccesses { get; set; }

    /// <summary>
    /// When the circuit was opened
    /// </summary>
    public DateTime? CircuitOpenedAt { get; set; }

    /// <summary>
    /// When the circuit last changed state
    /// </summary>
    public DateTime? LastStateChange { get; set; }

    /// <summary>
    /// Total number of times the circuit has opened
    /// </summary>
    public long CircuitOpenCount { get; set; }

    /// <summary>
    /// Total number of rejected operations (when circuit is open)
    /// </summary>
    public long RejectedOperations { get; set; }

    /// <summary>
    /// Last exception that caused a failure
    /// </summary>
    public Exception? LastException { get; set; }

    /// <summary>
    /// Failure percentage in the current window
    /// </summary>
    public double FailurePercentage => TotalOperations > 0 
        ? (double)FailedOperations / TotalOperations * 100.0 
        : 0.0;

    /// <summary>
    /// Success rate percentage
    /// </summary>
    public double SuccessRate => TotalOperations > 0
        ? (double)SuccessfulOperations / TotalOperations * 100.0
        : 0.0;

    /// <summary>
    /// Whether the circuit is currently open
    /// </summary>
    public bool IsOpen => State == CircuitState.Open;

    /// <summary>
    /// Whether the circuit is currently half-open
    /// </summary>
    public bool IsHalfOpen => State == CircuitState.HalfOpen;

    /// <summary>
    /// Whether the circuit is currently closed
    /// </summary>
    public bool IsClosed => State == CircuitState.Closed;

    /// <summary>
    /// Time remaining until circuit can transition to half-open
    /// </summary>
    public TimeSpan? TimeUntilRetry
    {
        get
        {
            if (State != CircuitState.Open || !CircuitOpenedAt.HasValue)
                return null;

            var elapsed = DateTime.UtcNow - CircuitOpenedAt.Value;
            return elapsed;
        }
    }
}

/// <summary>
/// Exception thrown when circuit breaker is open
/// </summary>
public class CircuitBreakerOpenException : Exception
{
    /// <summary>
    /// Circuit breaker statistics at the time of exception
    /// </summary>
    public CircuitBreakerStatistics Statistics { get; }

    public CircuitBreakerOpenException(CircuitBreakerStatistics statistics)
        : base($"Circuit breaker is open. Consecutive failures: {statistics.ConsecutiveFailures}, " +
               $"Failure rate: {statistics.FailurePercentage:F2}%. Circuit opened at: {statistics.CircuitOpenedAt}")
    {
        Statistics = statistics;
    }

    public CircuitBreakerOpenException(string message, CircuitBreakerStatistics statistics)
        : base(message)
    {
        Statistics = statistics;
    }
}
