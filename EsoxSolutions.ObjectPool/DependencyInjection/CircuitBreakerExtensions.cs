using EsoxSolutions.ObjectPool.CircuitBreaker;

namespace EsoxSolutions.ObjectPool.DependencyInjection;

/// <summary>
/// Extension methods for configuring circuit breaker
/// </summary>
public static class CircuitBreakerExtensions
{
    /// <param name="builder">The pool builder</param>
    /// <typeparam name="T">The type of object in the pool</typeparam>
    extension<T>(ObjectPoolBuilder<T> builder) where T : class
    {
        /// <summary>
        /// Configures circuit breaker for the pool
        /// </summary>
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
        public ObjectPoolBuilder<T> WithCircuitBreaker(int failureThreshold = 5,
            TimeSpan? openDuration = null,
            int successThreshold = 3)
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
        public ObjectPoolBuilder<T> WithCircuitBreakerConfiguration(Action<CircuitBreakerConfiguration> configure)
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
        public ObjectPoolBuilder<T> WithCircuitBreakerPercentage(double failurePercentageThreshold = 50.0,
            int minimumThroughput = 10,
            TimeSpan? openDuration = null)
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
        public ObjectPoolBuilder<T> WithCircuitBreakerExceptionFilter(Func<Exception, bool> isFailureException,
            int failureThreshold = 5,
            TimeSpan? openDuration = null)
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
        public ObjectPoolBuilder<T> WithCircuitBreakerCallbacks(Action<CircuitBreakerStatistics>? onOpen = null,
            Action<CircuitBreakerStatistics>? onClose = null,
            Action<CircuitBreakerStatistics>? onHalfOpen = null,
            int failureThreshold = 5)
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
}
