using EsoxSolutions.ObjectPool.Constants;
using EsoxSolutions.ObjectPool.Exceptions;
using EsoxSolutions.ObjectPool.Interfaces;
using EsoxSolutions.ObjectPool.Models;
using Microsoft.Extensions.Logging;

namespace EsoxSolutions.ObjectPool.Pools
{
    /// <summary>
    /// Queryable object pool that extends <see cref="ObjectPool{T}"/> with predicate-based object retrieval
    /// </summary>
    /// <typeparam name="T">The type of object to be stored in the object pool</typeparam>
    public class QueryableObjectPool<T> : ObjectPool<T>, IQueryableObjectPool<T> where T : notnull
    {
        /// <summary>
        /// The constructor for the queryable object pool
        /// </summary>
        /// <param name="initialObjects">the list of initial objects</param>
        public QueryableObjectPool(List<T> initialObjects) : this(initialObjects, new PoolConfiguration(), null)
        {
        }

        /// <summary>
        /// Constructor for the queryable object pool with configuration and logging
        /// </summary>
        /// <param name="initialObjects">The list of initialized objects</param>
        /// <param name="configuration">Pool configuration options</param>
        /// <param name="logger">Logger instance</param>
        public QueryableObjectPool(List<T> initialObjects, PoolConfiguration? configuration, ILogger<QueryableObjectPool<T>>? logger = null)
            : base(initialObjects, configuration, (ILogger?)logger)
        {
        }

        /// <summary>
        /// Get objects from the pool which match the query. If no objects are available, an exception is thrown.
        /// </summary>
        /// <param name="query">the query to be performed</param>
        /// <returns>an object from the pool</returns>
        /// <exception cref="NoObjectsInPoolException">Thrown when no objects could be found</exception>
        public PoolModel<T> GetObject(Func<T, bool> query)
        {
            ArgumentNullException.ThrowIfNull(query);
            if (Disposed) throw new ObjectDisposedException(nameof(QueryableObjectPool<>));

            Logger?.LogDebug(PoolConstants.Messages.AttemptingToGetObjectFromPoolUsingQueryAvailableCount, PoolingPolicy.Count);
            
            if (!this.ActiveObjectSlots.Wait(0))
            {
                Logger?.LogWarning(PoolConstants.Messages.MaxActiveLimitFormat, Configuration.MaxActiveObjects);
                throw new InvalidOperationException(string.Format(PoolConstants.Messages.MaxActiveLimitFormat, 
                    Configuration.MaxActiveObjects));
            }
            
            if (!this.PoolingPolicy.TryTake(query, out var foundObject) || foundObject is null)
            {
                this.ActiveObjectSlots.Release();
                statistics.IncrementPoolEmpty();
                Logger?.LogWarning(PoolConstants.Messages.NoObjectsInPoolMatchingYourQuery);
                throw new NoObjectsInPoolException(PoolConstants.Messages.NoObjectsInPoolMatchingYourQuery);
            }

            if (!this.ActiveObjects.TryAdd(foundObject, 0))
            {
                this.PoolingPolicy.Add(foundObject);
                this.ActiveObjectSlots.Release();
                throw new InvalidOperationException("The object could not be registered as active.");
            }

            UpdateStatisticsOnRetrieve();

            Logger?.LogDebug(PoolConstants.Messages.ObjectMatchingQueryRetrievedFromPoolActiveAvailable, 
                ActiveObjects.Count, PoolingPolicy.Count);

            return new PoolModel<T>(foundObject, this);
        }

        /// <summary>
        /// Try to get an object from the pool that matches the query without throwing an exception
        /// </summary>
        /// <param name="query">The query to be performed</param>
        /// <param name="poolModel">The pool model if successful</param>
        /// <returns>True if a matching object was retrieved, false otherwise</returns>
        public bool TryGetObject(Func<T, bool> query, out PoolModel<T>? poolModel)
        {
            ArgumentNullException.ThrowIfNull(query);
            poolModel = null;
            
            if (Disposed)
            {
                return false;
            }
            
            if (!this.ActiveObjectSlots.Wait(0))
            {
                Logger?.LogDebug(PoolConstants.Messages.CannotGetObjectActiveObjectsLimitMaxactiveReached, Configuration.MaxActiveObjects);
                return false;
            }
            
            if (!this.PoolingPolicy.TryTake(query, out var foundObject) || foundObject is null)
            {
                this.ActiveObjectSlots.Release();
                Logger?.LogDebug(PoolConstants.Messages.NoMatchingObjectAvailableRaceConditionTakenByAnotherThread);
                return false;
            }
            
            if (!this.ActiveObjects.TryAdd(foundObject, 0))
            {
                this.PoolingPolicy.Add(foundObject);
                this.ActiveObjectSlots.Release();
                return false;
            }

            UpdateStatisticsOnRetrieve();

            poolModel = new PoolModel<T>(foundObject, this);
            Logger?.LogDebug(PoolConstants.Messages.ObjectMatchingQueryRetrievedSuccessfullyActiveAvailable, 
                ActiveObjects.Count, PoolingPolicy.Count);
            return true;
        }
        
        /// <summary>
        /// Asynchronously get an object from the pool that matches the query
        /// </summary>
        /// <param name="query">The query to be performed</param>
        /// <param name="timeout">Maximum time to wait for an object</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A poolmodel</returns>
        public async Task<PoolModel<T>> GetObjectAsync(Func<T, bool> query, TimeSpan timeout = default, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);
            if (Disposed) throw new ObjectDisposedException(nameof(QueryableObjectPool<>));

            var effectiveTimeout = timeout == TimeSpan.Zero ? Configuration.DefaultTimeout : timeout;

            Logger?.LogDebug(PoolConstants.Messages.StartingAsyncObjectRetrievalWithQueryAndTimeout, effectiveTimeout);

            cancellationToken.ThrowIfCancellationRequested();

            if (TryGetObject(query, out var poolModel))
            {
                Logger?.LogDebug(PoolConstants.Messages.SuccessfullyRetrievedObjectWithQueryAsynchronously);
                return poolModel!;
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(effectiveTimeout);

            while (true)
            {
                bool signalled;
                try
                {
                    signalled = await _availabilitySignal
                        .WaitAsync(effectiveTimeout, timeoutCts.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Logger?.LogDebug(PoolConstants.Messages.AsyncRetrievalCancelled);
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    Logger?.LogWarning(PoolConstants.Messages.TimeoutWaitingForObjectMatchingQueryFromPoolAfter, effectiveTimeout);
                    throw new TimeoutException(string.Format(PoolConstants.Messages.TimeoutWaitingFormat, effectiveTimeout));
                }

                if (!signalled)
                {
                    Logger?.LogWarning(PoolConstants.Messages.TimeoutWaitingForObjectMatchingQueryFromPoolAfter, effectiveTimeout);
                    throw new TimeoutException(string.Format(PoolConstants.Messages.TimeoutWaitingFormat, effectiveTimeout));
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (TryGetObject(query, out poolModel))
                {
                    Logger?.LogDebug(PoolConstants.Messages.SuccessfullyRetrievedObjectWithQueryAsynchronously);
                    return poolModel!;
                }

                // A non-matching object was returned; put the signal back for the next waiter and yield briefly.
                _availabilitySignal.Release();
                await Task.Yield();
            }
        }

        /// <inheritdoc/>
        public override Dictionary<string, object> ExportMetrics(Dictionary<string, string>? tags = null)
        {
            var metrics = base.ExportMetrics(tags);
            metrics["pool_type"] = "queryable";
            return metrics;
        }
    }
}

