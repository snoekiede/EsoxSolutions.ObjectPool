namespace EsoxSolutions.ObjectPool.Lifecycle;

/// <summary>
/// Lifecycle hooks for pool objects
/// </summary>
/// <typeparam name="T">The type of object in the pool</typeparam>
public class LifecycleHooks<T> where T : notnull
{
    /// <summary>
    /// Called when a new object is created by the factory
    /// </summary>
    public Action<T>? OnCreate { get; set; }

    /// <summary>
    /// Called when an object is retrieved from the pool
    /// </summary>
    public Action<T>? OnAcquire { get; set; }

    /// <summary>
    /// Called when an object is returned to the pool
    /// </summary>
    public Action<T>? OnReturn { get; set; }

    /// <summary>
    /// Called when an object is being disposed/removed from the pool
    /// </summary>
    public Action<T>? OnDispose { get; set; }

    /// <summary>
    /// Called when an object is evicted from the pool
    /// </summary>
    public Action<T, EvictionReason>? OnEvict { get; set; }

    /// <summary>
    /// Called when validation of a returned object fails
    /// </summary>
    public Action<T>? OnValidationFailed { get; set; }

    /// <summary>
    /// Async version of OnCreate
    /// </summary>
    public Func<T, Task>? OnCreateAsync { get; set; }

    /// <summary>
    /// Async version of OnAcquire
    /// </summary>
    public Func<T, Task>? OnAcquireAsync { get; set; }

    /// <summary>
    /// Async version of OnReturn
    /// </summary>
    public Func<T, Task>? OnReturnAsync { get; set; }

    /// <summary>
    /// Async version of OnDispose
    /// </summary>
    public Func<T, Task>? OnDisposeAsync { get; set; }
}

/// <summary>
/// Reason why an object was evicted from the pool
/// </summary>
public enum EvictionReason
{
    /// <summary>
    /// Time-to-live expired
    /// </summary>
    TimeToLive,

    /// <summary>
    /// Idle timeout expired
    /// </summary>
    IdleTimeout,

    /// <summary>
    /// Custom eviction predicate
    /// </summary>
    CustomPredicate,

    /// <summary>
    /// Validation failed
    /// </summary>
    ValidationFailed,

    /// <summary>
    /// Pool is being disposed
    /// </summary>
    PoolDisposal,

    /// <summary>
    /// Manual eviction
    /// </summary>
    Manual
}

/// <summary>
/// Context information for lifecycle events
/// </summary>
/// <typeparam name="T">The type of object</typeparam>
public class LifecycleEventContext<T> where T : notnull
{
    /// <summary>
    /// The object involved in the lifecycle event
    /// </summary>
    public required T Object { get; init; }

    /// <summary>
    /// When the event occurred
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Number of times this object has been acquired
    /// </summary>
    public int AccessCount { get; init; }

    /// <summary>
    /// Age of the object since creation
    /// </summary>
    public TimeSpan Age { get; init; }

    /// <summary>
    /// Time since last access
    /// </summary>
    public TimeSpan? IdleTime { get; init; }

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = [];
}

/// <summary>
/// Statistics about lifecycle hook executions
/// </summary>
public class LifecycleHookStatistics
{
    /// <summary>
    /// Total number of OnCreate calls
    /// </summary>
    public long CreateCalls { get; set; }

    /// <summary>
    /// Total number of OnAcquire calls
    /// </summary>
    public long AcquireCalls { get; set; }

    /// <summary>
    /// Total number of OnReturn calls
    /// </summary>
    public long ReturnCalls { get; set; }

    /// <summary>
    /// Total number of OnDispose calls
    /// </summary>
    public long DisposeCalls { get; set; }

    /// <summary>
    /// Total number of OnEvict calls
    /// </summary>
    public long EvictCalls { get; set; }

    /// <summary>
    /// Total number of OnValidationFailed calls
    /// </summary>
    public long ValidationFailedCalls { get; set; }

    /// <summary>
    /// Total number of hook execution errors
    /// </summary>
    public long ErrorCount { get; set; }

    /// <summary>
    /// Last error that occurred during hook execution
    /// </summary>
    public Exception? LastError { get; set; }

    /// <summary>
    /// Average execution time for hooks
    /// </summary>
    public TimeSpan AverageExecutionTime { get; set; }

    /// <summary>
    /// Total time spent executing hooks
    /// </summary>
    public TimeSpan TotalExecutionTime { get; set; }
}

/// <summary>
/// Manager for executing lifecycle hooks
/// </summary>
/// <typeparam name="T">The type of object</typeparam>
public class LifecycleHookManager<T> where T : notnull
{
    private readonly LifecycleHooks<T> _hooks;
    private readonly LifecycleHookStatistics _statistics = new();
    private readonly bool _continueOnError;
    private readonly Action<Exception, string>? _errorHandler;

    /// <summary>
    /// Creates a new lifecycle hook manager
    /// </summary>
    /// <param name="hooks">The lifecycle hooks configuration</param>
    /// <param name="continueOnError">Whether to continue if a hook throws an exception</param>
    /// <param name="errorHandler">Optional error handler for hook exceptions</param>
    public LifecycleHookManager(
        LifecycleHooks<T> hooks,
        bool continueOnError = true,
        Action<Exception, string>? errorHandler = null)
    {
        _hooks = hooks ?? throw new ArgumentNullException(nameof(hooks));
        _continueOnError = continueOnError;
        _errorHandler = errorHandler;
    }

    /// <summary>
    /// Executes the OnCreate hook
    /// </summary>
    public void ExecuteOnCreate(T obj)
    {
        ExecuteHook(() =>
        {
            _hooks.OnCreate?.Invoke(obj);
            _statistics.CreateCalls++;
        }, "OnCreate");
    }

    /// <summary>
    /// Executes the OnCreate hook asynchronously
    /// </summary>
    public async Task ExecuteOnCreateAsync(T obj)
    {
        await ExecuteHookAsync(async () =>
        {
            if (_hooks.OnCreateAsync != null)
            {
                await _hooks.OnCreateAsync(obj);
            }
            else
            {
                _hooks.OnCreate?.Invoke(obj);
            }
            _statistics.CreateCalls++;
        }, "OnCreateAsync");
    }

    /// <summary>
    /// Executes the OnAcquire hook
    /// </summary>
    public void ExecuteOnAcquire(T obj)
    {
        ExecuteHook(() =>
        {
            _hooks.OnAcquire?.Invoke(obj);
            _statistics.AcquireCalls++;
        }, "OnAcquire");
    }

    /// <summary>
    /// Executes the OnAcquire hook asynchronously
    /// </summary>
    public async Task ExecuteOnAcquireAsync(T obj)
    {
        await ExecuteHookAsync(async () =>
        {
            if (_hooks.OnAcquireAsync != null)
            {
                await _hooks.OnAcquireAsync(obj);
            }
            else
            {
                _hooks.OnAcquire?.Invoke(obj);
            }
            _statistics.AcquireCalls++;
        }, "OnAcquireAsync");
    }

    /// <summary>
    /// Executes the OnReturn hook
    /// </summary>
    public void ExecuteOnReturn(T obj)
    {
        ExecuteHook(() =>
        {
            _hooks.OnReturn?.Invoke(obj);
            _statistics.ReturnCalls++;
        }, "OnReturn");
    }

    /// <summary>
    /// Executes the OnReturn hook asynchronously
    /// </summary>
    public async Task ExecuteOnReturnAsync(T obj)
    {
        await ExecuteHookAsync(async () =>
        {
            if (_hooks.OnReturnAsync != null)
            {
                await _hooks.OnReturnAsync(obj);
            }
            else
            {
                _hooks.OnReturn?.Invoke(obj);
            }
            _statistics.ReturnCalls++;
        }, "OnReturnAsync");
    }

    /// <summary>
    /// Executes the OnDispose hook
    /// </summary>
    public void ExecuteOnDispose(T obj)
    {
        ExecuteHook(() =>
        {
            _hooks.OnDispose?.Invoke(obj);
            _statistics.DisposeCalls++;
        }, "OnDispose");
    }

    /// <summary>
    /// Executes the OnDispose hook asynchronously
    /// </summary>
    public async Task ExecuteOnDisposeAsync(T obj)
    {
        await ExecuteHookAsync(async () =>
        {
            if (_hooks.OnDisposeAsync != null)
            {
                await _hooks.OnDisposeAsync(obj);
            }
            else
            {
                _hooks.OnDispose?.Invoke(obj);
            }
            _statistics.DisposeCalls++;
        }, "OnDisposeAsync");
    }

    /// <summary>
    /// Executes the OnEvict hook
    /// </summary>
    public void ExecuteOnEvict(T obj, EvictionReason reason)
    {
        ExecuteHook(() =>
        {
            _hooks.OnEvict?.Invoke(obj, reason);
            _statistics.EvictCalls++;
        }, "OnEvict");
    }

    /// <summary>
    /// Executes the OnValidationFailed hook
    /// </summary>
    public void ExecuteOnValidationFailed(T obj)
    {
        ExecuteHook(() =>
        {
            _hooks.OnValidationFailed?.Invoke(obj);
            _statistics.ValidationFailedCalls++;
        }, "OnValidationFailed");
    }

    /// <summary>
    /// Gets the current lifecycle hook statistics
    /// </summary>
    public LifecycleHookStatistics GetStatistics() => _statistics;

    private void ExecuteHook(Action action, string hookName)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            action();
            
            var duration = DateTime.UtcNow - startTime;
            UpdateExecutionTime(duration);
        }
        catch (Exception ex)
        {
            _statistics.ErrorCount++;
            _statistics.LastError = ex;
            _errorHandler?.Invoke(ex, hookName);

            if (!_continueOnError)
            {
                throw;
            }
        }
    }

    private async Task ExecuteHookAsync(Func<Task> action, string hookName)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            await action();
            
            var duration = DateTime.UtcNow - startTime;
            UpdateExecutionTime(duration);
        }
        catch (Exception ex)
        {
            _statistics.ErrorCount++;
            _statistics.LastError = ex;
            _errorHandler?.Invoke(ex, hookName);

            if (!_continueOnError)
            {
                throw;
            }
        }
    }

    private void UpdateExecutionTime(TimeSpan duration)
    {
        _statistics.TotalExecutionTime += duration;
        
        var totalCalls = _statistics.CreateCalls + _statistics.AcquireCalls + 
                        _statistics.ReturnCalls + _statistics.DisposeCalls + 
                        _statistics.EvictCalls + _statistics.ValidationFailedCalls;
        
        if (totalCalls > 0)
        {
            _statistics.AverageExecutionTime = TimeSpan.FromTicks(
                _statistics.TotalExecutionTime.Ticks / totalCalls);
        }
    }
}
