using EsoxSolutions.ObjectPool.Models;
using EsoxSolutions.ObjectPool.Policies;
using Microsoft.Extensions.DependencyInjection;

namespace EsoxSolutions.ObjectPool.DependencyInjection
{
    /// <summary>
    /// Extension methods for configuring pooling policies
    /// </summary>
    public static class PoolingPolicyExtensions
    {
        /// <summary>
        /// Configures the pool to use LIFO (Last-In-First-Out) policy.
        /// Most recently returned objects are retrieved first.
        /// Best for: Cache locality and temporal locality patterns (default).
        /// </summary>
        public static ObjectPoolBuilder<T> WithLifoPolicy<T>(this ObjectPoolBuilder<T> builder) where T : class
        {
            builder.Configure(config => config.PoolingPolicyType = PoolingPolicyType.Lifo);
            return builder;
        }

        /// <summary>
        /// Configures the pool to use FIFO (First-In-First-Out) policy.
        /// Objects are retrieved in the order they were returned.
        /// Best for: Fair scheduling and preventing object aging.
        /// </summary>
        public static ObjectPoolBuilder<T> WithFifoPolicy<T>(this ObjectPoolBuilder<T> builder) where T : class
        {
            builder.Configure(config => config.PoolingPolicyType = PoolingPolicyType.Fifo);
            return builder;
        }

        /// <summary>
        /// Configures the pool to use Priority-based policy.
        /// Higher priority objects are retrieved first.
        /// Best for: Quality-of-service requirements and tenant-based prioritization.
        /// </summary>
        /// <param name="builder">The pool builder</param>
        /// <param name="prioritySelector">Function to determine object priority (higher values = higher priority)</param>
        public static ObjectPoolBuilder<T> WithPriorityPolicy<T>(
            this ObjectPoolBuilder<T> builder, 
            Func<T, int> prioritySelector) where T : class
        {
            if (prioritySelector == null)
                throw new ArgumentNullException(nameof(prioritySelector));

            builder.Configure(config =>
            {
                config.PoolingPolicyType = PoolingPolicyType.Priority;
                config.PrioritySelector = prioritySelector;
            });
            return builder;
        }

        /// <summary>
        /// Configures the pool to use Least Recently Used (LRU) policy.
        /// Objects not used for the longest time are retrieved first.
        /// Best for: Preventing staleness and ensuring all objects get exercised.
        /// </summary>
        public static ObjectPoolBuilder<T> WithLeastRecentlyUsedPolicy<T>(this ObjectPoolBuilder<T> builder) where T : class
        {
            builder.Configure(config => config.PoolingPolicyType = PoolingPolicyType.LeastRecentlyUsed);
            return builder;
        }

        /// <summary>
        /// Configures the pool to use Round-robin policy.
        /// Objects are retrieved in a circular fashion.
        /// Best for: Load balancing and even wear distribution.
        /// </summary>
        public static ObjectPoolBuilder<T> WithRoundRobinPolicy<T>(this ObjectPoolBuilder<T> builder) where T : class
        {
            builder.Configure(config => config.PoolingPolicyType = PoolingPolicyType.RoundRobin);
            return builder;
        }

        /// <summary>
        /// Configures the pool with a custom pooling policy type
        /// </summary>
        /// <param name="builder">The pool builder</param>
        /// <param name="policyType">The type of pooling policy to use</param>
        public static ObjectPoolBuilder<T> WithPoolingPolicy<T>(
            this ObjectPoolBuilder<T> builder,
            PoolingPolicyType policyType) where T : class
        {
            builder.Configure(config => config.PoolingPolicyType = policyType);
            return builder;
        }
    }
}
