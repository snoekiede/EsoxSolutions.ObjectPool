using EsoxSolutions.ObjectPool.Lifecycle;

namespace EsoxSolutions.ObjectPool.DependencyInjection;

/// <summary>
/// Extension methods for configuring lifecycle hooks
/// </summary>
public static class LifecycleHookExtensions
{
    /// <param name="builder">The object pool builder to configure. Cannot be null.</param>
    extension<T>(ObjectPoolBuilder<T> builder) where T : class
    {
        /// <summary>
        /// Configures lifecycle hooks for the pool
        /// </summary>
        public ObjectPoolBuilder<T> WithLifecycleHooks(Action<LifecycleHooks<T>> configure)
        {
            return builder.Configure(config =>
            {
                var hooks = new LifecycleHooks<T>();
                configure(hooks);
                config.LifecycleHooks = hooks;
            });
        }

        /// <summary>
        /// Configures the OnCreate lifecycle hook
        /// </summary>
        public ObjectPoolBuilder<T> WithOnCreate(Action<T> onCreateAction)
        {
            return builder.WithLifecycleHooks(hooks => hooks.OnCreate = onCreateAction);
        }

        /// <summary>
        /// Configures the OnAcquire lifecycle hook
        /// </summary>
        public ObjectPoolBuilder<T> WithOnAcquire(Action<T> onAcquireAction)
        {
            return builder.WithLifecycleHooks(hooks => hooks.OnAcquire = onAcquireAction);
        }

        /// <summary>
        /// Configures the OnReturn lifecycle hook
        /// </summary>
        public ObjectPoolBuilder<T> WithOnReturn(Action<T> onReturnAction)
        {
            return builder.WithLifecycleHooks(hooks => hooks.OnReturn = onReturnAction);
        }

        /// <summary>
        /// Configures the OnDispose lifecycle hook
        /// </summary>
        public ObjectPoolBuilder<T> WithOnDispose(Action<T> onDisposeAction)
        {
            return builder.WithLifecycleHooks(hooks => hooks.OnDispose = onDisposeAction);
        }

        /// <summary>
        /// Configures the OnEvict lifecycle hook
        /// </summary>
        public ObjectPoolBuilder<T> WithOnEvict(Action<T, EvictionReason> onEvictAction)
        {
            return builder.WithLifecycleHooks(hooks => hooks.OnEvict = onEvictAction);
        }

        /// <summary>
        /// Configures the OnValidationFailed lifecycle hook
        /// </summary>
        public ObjectPoolBuilder<T> WithOnValidationFailed(Action<T> onValidationFailedAction)
        {
            return builder.WithLifecycleHooks(hooks => hooks.OnValidationFailed = onValidationFailedAction);
        }

        /// <summary>
        /// Configures async lifecycle hooks
        /// </summary>
        public ObjectPoolBuilder<T> WithAsyncLifecycleHooks(Func<T, Task>? onCreateAsync = null,
            Func<T, Task>? onAcquireAsync = null,
            Func<T, Task>? onReturnAsync = null,
            Func<T, Task>? onDisposeAsync = null)
        {
            return builder.WithLifecycleHooks(hooks =>
            {
                hooks.OnCreateAsync = onCreateAsync;
                hooks.OnAcquireAsync = onAcquireAsync;
                hooks.OnReturnAsync = onReturnAsync;
                hooks.OnDisposeAsync = onDisposeAsync;
            });
        }
    }
}
