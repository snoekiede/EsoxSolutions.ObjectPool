using EsoxSolutions.ObjectPool.CircuitBreaker;
using EsoxSolutions.ObjectPool.Models;
using Microsoft.Extensions.DependencyInjection;

namespace EsoxSolutions.ObjectPool.DependencyInjection;

/// <summary>
/// Extension methods for configuring circuit breaker
/// </summary>
public static class CircuitBreakerExtensions
{
    /// <summary>
    /// Configures circuit breaker for the pool
    /// </summary>
    /// <typeparam name="T">The type of object in the pool</typeparam>
    /// <param name="builder">The pool builder</param>
    /// <param name="failureThreshold">Number of failures before opening circuit</param>
    /// <param name="openDuration">How long to keep circuit open</param>
    /// <param name="successThreshold">Successes needed to close circuit</param>
    /// <returns>The builder for chaining</returns>
    /// <example>
    /// <code>
    /// services.AddDynamicObjectPool&lt;HttpClient&gt;(
    ///     sp => new HttpClient(),
    ///     config => config.MaxPoolSize = 100)
    ///     .WithCircuitBreaker(
    ///         failureThreshold: 5,
    ///         openDuration: TimeSpan.FromSeconds(30),
    ///         successThreshold: 3);
    /// </code>
    /// </example>
    public static ObjectPoolBuilder<T> WithCircuitBreaker<T>(
        this ObjectPoolBuilder<T> builder,
        int failureThreshold = 5,
        TimeSpan? openDuration = null,
        int successThreshold = 3) where T : class
    {
        return builder.Configure(config =>
        {
            config.CircuitBreakerConfiguration = new CircuitBreakerConfiguration
            {
                FailureThreshold = failureThreshold,
                OpenDuration = openDuration ?? TimeSpan.FromSeconds(30),
                SuccessThreshold = successThreshold,
                EnableAutomaticRecovery = true
            };
        });
    }

    /// <summary>
    /// Configures circuit breaker with full configuration
    /// </summary>
    public static ObjectPoolBuilder<T> WithCircuitBreakerConfiguration<T>(
        this ObjectPoolBuilder<T> builder,
        Action<CircuitBreakerConfiguration> configure) where T : class
    {
        return builder.Configure(config =>
        {
            var cbConfig = new CircuitBreakerConfiguration();
            configure(cbConfig);
            config.CircuitBreakerConfiguration = cbConfig;
        });
    }

    /// <summary>
    /// Configures circuit breaker with percentage-based thresholds
    /// </summary>
    public static ObjectPoolBuilder<T> WithCircuitBreakerPercentage<T>(
        this ObjectPoolBuilder<T> builder,
        double failurePercentageThreshold = 50.0,
        int minimumThroughput = 10,
        TimeSpan? openDuration = null) where T : class
    {
        return builder.Configure(config =>
        {
            config.CircuitBreakerConfiguration = new CircuitBreakerConfiguration
            {
                FailurePercentageThreshold = failurePercentageThreshold,
                MinimumThroughput = minimumThroughput,
                OpenDuration = openDuration ?? TimeSpan.FromSeconds(30),
                EnableAutomaticRecovery = true
            };
        });
    }

    /// <summary>
    /// Configures circuit breaker with custom exception filtering
    /// </summary>
    public static ObjectPoolBuilder<T> WithCircuitBreakerExceptionFilter<T>(
        this ObjectPoolBuilder<T> builder,
        Func<Exception, bool> isFailureException,
        int failureThreshold = 5,
        TimeSpan? openDuration = null) where T : class
    {
        return builder.Configure(config =>
        {
            config.CircuitBreakerConfiguration = new CircuitBreakerConfiguration
            {
                FailureThreshold = failureThreshold,
                OpenDuration = openDuration ?? TimeSpan.FromSeconds(30),
                IsFailureException = isFailureException,
                EnableAutomaticRecovery = true
            };
        });
    }

    /// <summary>
    /// Configures circuit breaker with callbacks
    /// </summary>
    public static ObjectPoolBuilder<T> WithCircuitBreakerCallbacks<T>(
        this ObjectPoolBuilder<T> builder,
        Action<CircuitBreakerStatistics>? onOpen = null,
        Action<CircuitBreakerStatistics>? onClose = null,
        Action<CircuitBreakerStatistics>? onHalfOpen = null,
        int failureThreshold = 5) where T : class
    {
        return builder.Configure(config =>
        {
            config.CircuitBreakerConfiguration = new CircuitBreakerConfiguration
            {
                FailureThreshold = failureThreshold,
                OnCircuitOpen = onOpen,
                OnCircuitClose = onClose,
                OnCircuitHalfOpen = onHalfOpen,
                EnableAutomaticRecovery = true
            };
        });
    }
}
