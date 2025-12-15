namespace EsoxSolutions.ObjectPool.Scoping;

/// <summary>
/// Represents a scope identifier for scoped object pools
/// </summary>
public class PoolScope : IEquatable<PoolScope>
{
    /// <summary>
    /// Unique identifier for the scope
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Optional tenant identifier
    /// </summary>
    public string? TenantId { get; }

    /// <summary>
    /// Optional user identifier
    /// </summary>
    public string? UserId { get; }

    /// <summary>
    /// Additional metadata for the scope
    /// </summary>
    public Dictionary<string, string> Metadata { get; }

    /// <summary>
    /// When the scope was created
    /// </summary>
    public DateTime CreatedAt { get; }

    /// <summary>
    /// Creates a new pool scope
    /// </summary>
    /// <param name="id">Unique scope identifier</param>
    /// <param name="tenantId">Optional tenant identifier</param>
    /// <param name="userId">Optional user identifier</param>
    public PoolScope(string id, string? tenantId = null, string? userId = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Scope ID cannot be null or empty", nameof(id));

        Id = id;
        TenantId = tenantId;
        UserId = userId;
        Metadata = new Dictionary<string, string>();
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a scope from tenant ID
    /// </summary>
    public static PoolScope FromTenant(string tenantId)
    {
        return new PoolScope($"tenant:{tenantId}", tenantId);
    }

    /// <summary>
    /// Creates a scope from user ID
    /// </summary>
    public static PoolScope FromUser(string userId, string? tenantId = null)
    {
        return new PoolScope($"user:{userId}", tenantId, userId);
    }

    /// <summary>
    /// Creates a scope from request context
    /// </summary>
    public static PoolScope FromContext(string contextId, string? tenantId = null)
    {
        return new PoolScope($"context:{contextId}", tenantId);
    }

    public bool Equals(PoolScope? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Id == other.Id && TenantId == other.TenantId && UserId == other.UserId;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as PoolScope);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id, TenantId, UserId);
    }

    public override string ToString()
    {
        var parts = new List<string> { Id };
        if (TenantId != null) parts.Add($"Tenant={TenantId}");
        if (UserId != null) parts.Add($"User={UserId}");
        return string.Join(", ", parts);
    }

    public static bool operator ==(PoolScope? left, PoolScope? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(PoolScope? left, PoolScope? right)
    {
        return !Equals(left, right);
    }
}

/// <summary>
/// Scope resolution strategy
/// </summary>
public enum ScopeResolutionStrategy
{
    /// <summary>
    /// Resolve scope from HTTP context (tenant from header/claim)
    /// </summary>
    HttpContext,

    /// <summary>
    /// Resolve scope from ambient context (AsyncLocal)
    /// </summary>
    Ambient,

    /// <summary>
    /// Resolve scope from dependency injection scope
    /// </summary>
    DependencyInjection,

    /// <summary>
    /// Custom resolution logic
    /// </summary>
    Custom
}

/// <summary>
/// Configuration for scoped pools
/// </summary>
public class ScopedPoolConfiguration
{
    /// <summary>
    /// Strategy for resolving the current scope
    /// </summary>
    public ScopeResolutionStrategy ResolutionStrategy { get; set; } = ScopeResolutionStrategy.HttpContext;

    /// <summary>
    /// Custom scope resolver function
    /// </summary>
    public Func<PoolScope>? CustomScopeResolver { get; set; }

    /// <summary>
    /// Maximum number of scopes to maintain simultaneously
    /// </summary>
    public int MaxScopes { get; set; } = 100;

    /// <summary>
    /// How long to keep inactive scopes before cleanup
    /// </summary>
    public TimeSpan ScopeIdleTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Whether to enable automatic scope cleanup
    /// </summary>
    public bool EnableAutomaticCleanup { get; set; } = true;

    /// <summary>
    /// Interval for scope cleanup checks
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// HTTP header name for tenant ID
    /// </summary>
    public string TenantHeaderName { get; set; } = "X-Tenant-Id";

    /// <summary>
    /// Claim type for tenant ID
    /// </summary>
    public string TenantClaimType { get; set; } = "tenant_id";

    /// <summary>
    /// Whether to dispose pools when scopes are cleaned up
    /// </summary>
    public bool DisposePoolsOnCleanup { get; set; } = true;

    /// <summary>
    /// Action to execute when a scope is created
    /// </summary>
    public Action<PoolScope>? OnScopeCreated { get; set; }

    /// <summary>
    /// Action to execute when a scope is disposed
    /// </summary>
    public Action<PoolScope>? OnScopeDisposed { get; set; }
}

/// <summary>
/// Statistics for scoped pools
/// </summary>
public class ScopedPoolStatistics
{
    /// <summary>
    /// Total number of scopes created
    /// </summary>
    public long TotalScopesCreated { get; set; }

    /// <summary>
    /// Current number of active scopes
    /// </summary>
    public int ActiveScopes { get; set; }

    /// <summary>
    /// Total number of scopes cleaned up
    /// </summary>
    public long ScopesCleanedUp { get; set; }

    /// <summary>
    /// Peak number of concurrent scopes
    /// </summary>
    public int PeakScopes { get; set; }

    /// <summary>
    /// Last cleanup time
    /// </summary>
    public DateTime? LastCleanup { get; set; }

    /// <summary>
    /// Average number of objects per scope
    /// </summary>
    public double AverageObjectsPerScope { get; set; }

    /// <summary>
    /// Total objects across all scopes
    /// </summary>
    public long TotalObjects { get; set; }

    /// <summary>
    /// Scope access counts
    /// </summary>
    public Dictionary<string, long> ScopeAccessCounts { get; } = new();
}

/// <summary>
/// Context for ambient scope resolution
/// </summary>
public static class AmbientPoolScope
{
    private static readonly AsyncLocal<PoolScope?> _currentScope = new();

    /// <summary>
    /// Gets or sets the current ambient scope
    /// </summary>
    public static PoolScope? Current
    {
        get => _currentScope.Value;
        set => _currentScope.Value = value;
    }

    /// <summary>
    /// Creates a new scope context
    /// </summary>
    public static IDisposable BeginScope(PoolScope scope)
    {
        var previous = Current;
        Current = scope;
        return new ScopeContext(previous);
    }

    private class ScopeContext : IDisposable
    {
        private readonly PoolScope? _previousScope;
        private bool _disposed;

        public ScopeContext(PoolScope? previousScope)
        {
            _previousScope = previousScope;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Current = _previousScope;
                _disposed = true;
            }
        }
    }
}
