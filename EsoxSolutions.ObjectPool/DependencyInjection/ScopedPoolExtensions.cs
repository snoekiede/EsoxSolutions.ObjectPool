using EsoxSolutions.ObjectPool.Models;
using EsoxSolutions.ObjectPool.Pools;
using EsoxSolutions.ObjectPool.Scoping;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace EsoxSolutions.ObjectPool.DependencyInjection;

/// <summary>
/// Extension methods for configuring scoped pools
/// </summary>
public static class ScopedPoolExtensions
{
    /// <param name="services">The service collection</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers a scoped pool manager for multi-tenancy scenarios
        /// </summary>
        /// <typeparam name="T">The type of object in the pools</typeparam>
        /// <param name="factory">Factory function to create objects</param>
        /// <param name="configurePool">Optional pool configuration</param>
        /// <param name="configureScoping">Optional scoping configuration</param>
        /// <returns>The service collection for chaining</returns>
        public IServiceCollection AddScopedObjectPool<T>(Func<IServiceProvider, PoolScope, T> factory,
            Action<PoolConfiguration>? configurePool = null,
            Action<ScopedPoolConfiguration>? configureScoping = null) where T : class
        {
            var poolConfig = new PoolConfiguration();
            configurePool?.Invoke(poolConfig);

            var scopingConfig = new ScopedPoolConfiguration();
            configureScoping?.Invoke(scopingConfig);

            services.TryAddSingleton(scopingConfig);

            services.TryAddSingleton<ScopedPoolManager<T>>(sp =>
            {
                var logger = sp.GetService<ILogger<ScopedPoolManager<T>>>();

                return new ScopedPoolManager<T>(
                    scope => new DynamicObjectPool<T>(
                        () => factory(sp, scope),
                        poolConfig,
                        sp.GetService<ILogger<ObjectPool<T>>>()),
                    scopingConfig,
                    logger);
            });

            return services;
        }

        /// <summary>
        /// Registers a scoped pool manager with per-tenant configuration
        /// </summary>
        public IServiceCollection AddTenantScopedObjectPool<T>(Func<IServiceProvider, string, T> tenantFactory,
            Action<PoolConfiguration>? configurePool = null) where T : class
        {
            return services.AddScopedObjectPool<T>(
                (sp, scope) => tenantFactory(sp, scope.TenantId ?? "default"),
                configurePool,
                config =>
                {
                    config.ResolutionStrategy = ScopeResolutionStrategy.HttpContext;
                    config.TenantHeaderName = "X-Tenant-Id";
                });
        }

        /// <summary>
        /// Registers a scoped pool manager with ambient scope resolution
        /// </summary>
        public IServiceCollection AddAmbientScopedObjectPool<T>(Func<IServiceProvider, T> factory,
            Action<PoolConfiguration>? configurePool = null) where T : class
        {
            return services.AddScopedObjectPool<T>(
                (sp, scope) => factory(sp),
                configurePool,
                config => config.ResolutionStrategy = ScopeResolutionStrategy.Ambient);
        }

        /// <summary>
        /// Registers a scoped pool manager with custom scope resolution
        /// </summary>
        public IServiceCollection AddCustomScopedObjectPool<T>(Func<IServiceProvider, PoolScope, T> factory,
            Func<PoolScope> scopeResolver,
            Action<PoolConfiguration>? configurePool = null) where T : class
        {
            return services.AddScopedObjectPool(
                factory,
                configurePool,
                config =>
                {
                    config.ResolutionStrategy = ScopeResolutionStrategy.Custom;
                    config.CustomScopeResolver = scopeResolver;
                });
        }
    }
}

/// <summary>
/// Builder extensions for scoped pools
/// </summary>
public static class ScopedPoolBuilderExtensions
{
    /// <param name="builder">The object pool builder</param>
    extension<T>(ObjectPoolBuilder<T> builder) where T : class
    {
        /// <summary>
        /// Configures the pool to be scoped by tenant
        /// </summary>
        public ObjectPoolBuilder<T> WithTenantScoping(string tenantHeaderName = "X-Tenant-Id")
        {
            // Configuration would be applied when building the pool
            // For now, just return the builder as scoping is managed separately
            return builder;
        }

        /// <summary>
        /// Configures the pool to be scoped by user
        /// </summary>
        public ObjectPoolBuilder<T> WithUserScoping(string userClaimType = "sub")
        {
            // Configuration would be applied when building the pool
            return builder;
        }

        /// <summary>
        /// Configures the pool to be scoped by custom logic
        /// </summary>
        public ObjectPoolBuilder<T> WithCustomScoping(Func<PoolScope> scopeResolver)
        {
            // Configuration would be applied when building the pool
            return builder;
        }
    }
}
