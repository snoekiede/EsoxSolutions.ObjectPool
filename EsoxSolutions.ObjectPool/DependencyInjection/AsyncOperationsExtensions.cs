using EsoxSolutions.ObjectPool.Models;
using Microsoft.Extensions.DependencyInjection;

namespace EsoxSolutions.ObjectPool.DependencyInjection
{
    /// <summary>
    /// Extension methods for configuring async operations in object pools
    /// </summary>
    public static class AsyncOperationsExtensions
    {
        /// <summary>
        /// Configures async validation for returned objects
        /// </summary>
        /// <typeparam name="T">The type of object in the pool</typeparam>
        /// <param name="builder">The object pool builder</param>
        /// <param name="asyncValidationFunction">Async function to validate objects when returned to pool</param>
        /// <returns>The builder for method chaining</returns>
        public static ObjectPoolBuilder<T> WithAsyncValidation<T>(
            this ObjectPoolBuilder<T> builder,
            Func<T, ValueTask<bool>> asyncValidationFunction) where T : class
        {
            if (asyncValidationFunction == null)
                throw new ArgumentNullException(nameof(asyncValidationFunction));

            builder.Configure(config =>
            {
                config.ValidateOnReturn = true;
                config.AsyncValidationFunction = obj => asyncValidationFunction((T)obj);
            });

            return builder;
        }

        /// <summary>
        /// Enables async disposal for pooled objects (enabled by default)
        /// </summary>
        /// <typeparam name="T">The type of object in the pool</typeparam>
        /// <param name="builder">The object pool builder</param>
        /// <param name="enable">Whether to enable async disposal</param>
        /// <returns>The builder for method chaining</returns>
        public static ObjectPoolBuilder<T> WithAsyncDisposal<T>(
            this ObjectPoolBuilder<T> builder,
            bool enable = true) where T : class
        {
            builder.Configure(config => config.UseAsyncDisposal = enable);
            return builder;
        }

        /// <summary>
        /// Configures the pool to use async lifecycle hooks
        /// </summary>
        /// <typeparam name="T">The type of object in the pool</typeparam>
        /// <param name="builder">The object pool builder</param>
        /// <param name="configureHooks">Action to configure async lifecycle hooks</param>
        /// <returns>The builder for method chaining</returns>
        public static ObjectPoolBuilder<T> WithAsyncLifecycleHooks<T>(
            this ObjectPoolBuilder<T> builder,
            Action<Lifecycle.LifecycleHooks<T>> configureHooks) where T : class
        {
            if (configureHooks == null)
                throw new ArgumentNullException(nameof(configureHooks));

            var hooks = new Lifecycle.LifecycleHooks<T>();
            configureHooks(hooks);

            builder.Configure(config => config.LifecycleHooks = hooks);
            return builder;
        }
    }
}
