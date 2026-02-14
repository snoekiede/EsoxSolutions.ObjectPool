using EsoxSolutions.ObjectPool.CircuitBreaker;
using EsoxSolutions.ObjectPool.Eviction;
using EsoxSolutions.ObjectPool.Policies;

namespace EsoxSolutions.ObjectPool.Models
{
    /// <summary>
    /// Configuration options for object pools
    /// </summary>
    public class PoolConfiguration
    {
        /// <summary>
        /// Maximum number of objects the pool can contain (default: unlimited)
        /// </summary>
        public int MaxPoolSize { get; set; } = int.MaxValue;

        /// <summary>
        /// Maximum number of objects that can be active at once (default: unlimited)
        /// </summary>
        public int MaxActiveObjects { get; set; } = int.MaxValue;

        /// <summary>
        /// Timeout for async operations (default: 30 seconds)
        /// </summary>
        public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Whether to validate objects when returned to pool
        /// </summary>
        public bool ValidateOnReturn { get; set; } = false;

        /// <summary>
        /// Optional validation function for returned objects
        /// </summary>
        public Func<object, bool>? ValidationFunction { get; set; }

        /// <summary>
        /// Whether to enable detailed statistics collection (may impact performance)
        /// </summary>
        public bool EnableDetailedStatistics { get; set; } = true;

        /// <summary>
        /// Eviction configuration for time-to-live and idle timeout support
        /// </summary>
        public EvictionConfiguration? EvictionConfiguration { get; set; }

        /// <summary>
        /// Circuit breaker configuration for protecting against cascading failures
        /// </summary>
        public CircuitBreakerConfiguration? CircuitBreakerConfiguration { get; set; }

        /// <summary>
        /// Lifecycle hooks configuration for custom object lifecycle management
        /// </summary>
        public object? LifecycleHooks { get; set; }

        /// <summary>
        /// Whether to continue pool operations if lifecycle hooks throw exceptions
        /// </summary>
        public bool ContinueOnLifecycleHookError { get; set; } = true;

        /// <summary>
        /// The pooling policy type to use (default: LIFO for best performance).
        /// This is used by PolicyBasedObjectPool and DynamicObjectPool with policies.
        /// </summary>
        public PoolingPolicyType PoolingPolicyType { get; set; } = PoolingPolicyType.Lifo;

        /// <summary>
        /// Priority selector function for Priority pooling policy.
        /// Required when PoolingPolicyType is set to Priority.
        /// Must be of type Func&lt;T, int&gt; where T is the pooled object type.
        /// </summary>
        public object? PrioritySelector { get; set; }

        /// <summary>
        /// Optional async validation function for returned objects.
        /// If both ValidationFunction and AsyncValidationFunction are set, AsyncValidationFunction takes precedence.
        /// </summary>
        public Func<object, ValueTask<bool>>? AsyncValidationFunction { get; set; }

        /// <summary>
        /// Whether to use async disposal when disposing pooled objects (default: true).
        /// When true, objects implementing IAsyncDisposable will be disposed asynchronously.
        /// </summary>
        public bool UseAsyncDisposal { get; set; } = true;
    }
}
