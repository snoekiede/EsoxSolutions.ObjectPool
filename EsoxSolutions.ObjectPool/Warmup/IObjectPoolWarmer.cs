using EsoxSolutions.ObjectPool.Interfaces;

namespace EsoxSolutions.ObjectPool.Warmup;

/// <summary>
/// Interface for pool warm-up functionality
/// </summary>
/// <typeparam name="T">The type of object in the pool</typeparam>
public interface IObjectPoolWarmer<T> where T : notnull
{
    /// <summary>
    /// Warms up the pool by pre-creating objects to the target size
    /// </summary>
    /// <param name="targetSize">The target number of objects to pre-create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the warm-up operation</returns>
    Task WarmUpAsync(int targetSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Warms up the pool to a percentage of maximum capacity
    /// </summary>
    /// <param name="targetPercentage">Target percentage (0-100) of maximum capacity</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the warm-up operation</returns>
    Task WarmUpToPercentageAsync(double targetPercentage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current warm-up status
    /// </summary>
    WarmupStatus GetWarmupStatus();
}

/// <summary>
/// Status information for pool warm-up operations
/// </summary>
public class WarmupStatus
{
    /// <summary>
    /// Whether the pool has been warmed up
    /// </summary>
    public bool IsWarmedUp { get; set; }

    /// <summary>
    /// Number of objects created during warm-up
    /// </summary>
    public int ObjectsCreated { get; set; }

    /// <summary>
    /// Target number of objects for warm-up
    /// </summary>
    public int TargetSize { get; set; }

    /// <summary>
    /// Time taken for warm-up
    /// </summary>
    public TimeSpan WarmupDuration { get; set; }

    /// <summary>
    /// When the warm-up was completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Any errors that occurred during warm-up
    /// </summary>
    public List<string> Errors { get; set; } = [];

    /// <summary>
    /// Warm-up progress percentage (0-100)
    /// </summary>
    public double ProgressPercentage => TargetSize > 0 ? (double)ObjectsCreated / TargetSize * 100.0 : 0.0;
}
