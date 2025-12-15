using EsoxSolutions.ObjectPool.Lifecycle;
using EsoxSolutions.ObjectPool.Models;
using Microsoft.Extensions.DependencyInjection;

namespace EsoxSolutions.ObjectPool.DependencyInjection;

/// <summary>
/// Extension methods for configuring lifecycle hooks
/// </summary>
public static class LifecycleHookExtensions
{
    /// <summary>
    /// Configures lifecycle hooks for the pool
    /// </summary>
    public static ObjectPoolBuilder<T> WithLifecycleHooks<T>(
        this ObjectPoolBuilder<T> builder,
        Action<LifecycleHooks<T>> configure) where T : class
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
    public static ObjectPoolBuilder<T> WithOnCreate<T>(
        this ObjectPoolBuilder<T> builder,
        Action<T> onCreateAction) where T : class
    {
        return builder.WithLifecycleHooks(hooks => hooks.OnCreate = onCreateAction);
    }

    /// <summary>
    /// Configures the OnAcquire lifecycle hook
    /// </summary>
    public static ObjectPoolBuilder<T> WithOnAcquire<T>(
        this ObjectPoolBuilder<T> builder,
        Action<T> onAcquireAction) where T : class
    {
        return builder.WithLifecycleHooks(hooks => hooks.OnAcquire = onAcquireAction);
    }

    /// <summary>
    /// Configures the OnReturn lifecycle hook
    /// </summary>
    public static ObjectPoolBuilder<T> WithOnReturn<T>(
        this ObjectPoolBuilder<T> builder,
        Action<T> onReturnAction) where T : class
    {
        return builder.WithLifecycleHooks(hooks => hooks.OnReturn = onReturnAction);
    }

    /// <summary>
    /// Configures the OnDispose lifecycle hook
    /// </summary>
    public static ObjectPoolBuilder<T> WithOnDispose<T>(
        this ObjectPoolBuilder<T> builder,
        Action<T> onDisposeAction) where T : class
    {
        return builder.WithLifecycleHooks(hooks => hooks.OnDispose = onDisposeAction);
    }

    /// <summary>
    /// Configures the OnEvict lifecycle hook
    /// </summary>
    public static ObjectPoolBuilder<T> WithOnEvict<T>(
        this ObjectPoolBuilder<T> builder,
        Action<T, EvictionReason> onEvictAction) where T : class
    {
        return builder.WithLifecycleHooks(hooks => hooks.OnEvict = onEvictAction);
    }

    /// <summary>
    /// Configures the OnValidationFailed lifecycle hook
    /// </summary>
    public static ObjectPoolBuilder<T> WithOnValidationFailed<T>(
        this ObjectPoolBuilder<T> builder,
        Action<T> onValidationFailedAction) where T : class
    {
        return builder.WithLifecycleHooks(hooks => hooks.OnValidationFailed = onValidationFailedAction);
    }

    /// <summary>
    /// Configures async lifecycle hooks
    /// </summary>
    public static ObjectPoolBuilder<T> WithAsyncLifecycleHooks<T>(
        this ObjectPoolBuilder<T> builder,
        Func<T, Task>? onCreateAsync = null,
        Func<T, Task>? onAcquireAsync = null,
        Func<T, Task>? onReturnAsync = null,
        Func<T, Task>? onDisposeAsync = null) where T : class
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
