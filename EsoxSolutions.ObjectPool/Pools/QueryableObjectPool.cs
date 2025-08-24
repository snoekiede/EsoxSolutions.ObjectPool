using EsoxSolutions.ObjectPool.Constants;
using EsoxSolutions.ObjectPool.Exceptions;
using EsoxSolutions.ObjectPool.Interfaces;
using EsoxSolutions.ObjectPool.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace EsoxSolutions.ObjectPool.Pools
{
    /// <summary>
    /// Queryable object pool
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class QueryableObjectPool<T> : IQueryableObjectPool<T>
    {
        /// <summary>
        /// A concurrent stack of available objects for efficient O(1) operations
        /// </summary>
        protected ConcurrentStack<T> AvailableObjects;
        
        /// <summary>
        /// A concurrent dictionary of active objects for efficient O(1) lookups
        /// </summary>
        protected ConcurrentDictionary<T, byte> ActiveObjects;
        
        /// <summary>
        /// Pool statistics
        /// </summary>
        protected PoolStatistics statistics = new();
        
        /// <summary>
        /// Pool configuration
        /// </summary>
        protected readonly PoolConfiguration Configuration;
        
        /// <summary>
        /// Logger instance
        /// </summary>
        protected readonly ILogger? Logger;

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
        {
            this.Configuration = configuration ?? new PoolConfiguration();
            this.Logger = logger;
            this.ActiveObjects = new ConcurrentDictionary<T, byte>();
            this.AvailableObjects = new ConcurrentStack<T>(initialObjects);
            
            if (initialObjects.Count > this.Configuration.MaxPoolSize)
            {
                throw new ArgumentException(string.Format(PoolConstants.Messages.InitialObjectsExceedMaxFormat, 
                    initialObjects.Count, this.Configuration.MaxPoolSize));
            }
            
            logger?.LogInformation(PoolConstants.Messages.ObjectpoolCreatedWithInitialcountObjectsMaxpoolsizeMaxactive,
                initialObjects.Count, this.Configuration.MaxPoolSize, this.Configuration.MaxActiveObjects);
        }

        /// <summary>
        /// Gets the number of available objects in the pool
        /// </summary>
        public int AvailableObjectCount => this.AvailableObjects.Count;

        /// <summary>
        /// Gets the current pool statistics
        /// </summary>
        public PoolStatistics Statistics
        {
            get
            {
                statistics.CurrentActiveObjects = this.ActiveObjects.Count;
                statistics.CurrentAvailableObjects = this.AvailableObjects.Count;
                return statistics;
            }
        }

        /// <summary>
        /// Returns an object from the pool. If no objects are available, an exception is thrown.
        /// </summary>
        /// <returns>A PoolModel object</returns>
        /// <exception cref="NoObjectsInPoolException">Raised when no object could be found</exception>
        public virtual PoolModel<T> GetObject()
        {
            Logger?.LogDebug(PoolConstants.Messages.AttemptingToGetObjectFromPoolAvailableCount, AvailableObjects.Count);
            
            if (this.ActiveObjects.Count >= Configuration.MaxActiveObjects)
            {
                Logger?.LogWarning(PoolConstants.Messages.MaxActiveLimitFormat, Configuration.MaxActiveObjects);
                throw new InvalidOperationException(string.Format(PoolConstants.Messages.MaxActiveLimitFormat, 
                    Configuration.MaxActiveObjects));
            }
            
            if (!this.AvailableObjects.TryPop(out var result))
            {
                statistics.PoolEmptyCount++;
                Logger?.LogWarning(PoolConstants.Messages.PoolEmpty);
                throw new NoObjectsInPoolException(PoolConstants.Messages.NoObjectsAvailable);
            }
            
            this.ActiveObjects.TryAdd(result, 0);
            
            statistics.TotalObjectsRetrieved++;
            statistics.CurrentActiveObjects = this.ActiveObjects.Count;
            statistics.CurrentAvailableObjects = this.AvailableObjects.Count;
            if (statistics.CurrentActiveObjects > statistics.PeakActiveObjects)
            {
                statistics.PeakActiveObjects = statistics.CurrentActiveObjects;
            }
            
            Logger?.LogDebug(PoolConstants.Messages.ObjectRetrievedFromPoolActiveActiveAvailableAvailable,
                ActiveObjects.Count, AvailableObjects.Count);
            
            return new PoolModel<T>(result, this);
        }

        /// <summary>
        /// Try to get an object from the pool without throwing an exception
        /// </summary>
        /// <param name="poolModel">The pool model if successful</param>
        /// <returns>True if an object was retrieved, false otherwise</returns>
        public bool TryGetObject(out PoolModel<T>? poolModel)
        {
            if (this.ActiveObjects.Count >= Configuration.MaxActiveObjects)
            {
                Logger?.LogDebug(PoolConstants.Messages.CannotGetObjectActiveObjectsLimitMaxactiveReached, Configuration.MaxActiveObjects);
                poolModel = null;
                return false;
            }
            
            if (!this.AvailableObjects.TryPop(out var result))
            {
                statistics.PoolEmptyCount++;
                Logger?.LogDebug(PoolConstants.Messages.NoAvailableObjects);
                poolModel = null;
                return false;
            }

            this.ActiveObjects.TryAdd(result, 0);
            
            statistics.TotalObjectsRetrieved++;
            statistics.CurrentActiveObjects = this.ActiveObjects.Count;
            statistics.CurrentAvailableObjects = this.AvailableObjects.Count;
            if (statistics.CurrentActiveObjects > statistics.PeakActiveObjects)
            {
                statistics.PeakActiveObjects = statistics.CurrentActiveObjects;
            }
            
            poolModel = new PoolModel<T>(result, this);
            Logger?.LogDebug(PoolConstants.Messages.ObjectRetrievedSuccessfullyActiveAvailable,
                ActiveObjects.Count, AvailableObjects.Count);
            return true;
        }

        /// <summary>
        /// Returns an object to the pool. If the object is not in the pool, an exception is thrown.
        /// </summary>
        /// <param name="obj">The object to be returned</param>
        /// <exception cref="NoObjectsInPoolException">Raised if the object was not in the active objects list</exception>
        public void ReturnObject(PoolModel<T> obj)
        {
            var unwrapped = obj.Unwrap();
            if (!this.ActiveObjects.ContainsKey(unwrapped))
            {
                Logger?.LogWarning(PoolConstants.Messages.ObjectNotInActiveList);
                throw new NoObjectsInPoolException(PoolConstants.Messages.ObjectNotInPool);
            }
            
            // Validate object if configured
            if (Configuration is {ValidateOnReturn: true, ValidationFunction: not null})
            {
                if (!Configuration.ValidationFunction(unwrapped))
                {
                    Logger?.LogWarning(PoolConstants.Messages.ValidationFailed);
                    this.ActiveObjects.TryRemove(unwrapped, out _);
                    statistics.TotalObjectsReturned++;
                    statistics.CurrentActiveObjects = this.ActiveObjects.Count;
                    statistics.CurrentAvailableObjects = this.AvailableObjects.Count;
                    return;
                }
            }
            
            // Check if we're exceeding pool size limit
            if (this.AvailableObjects.Count >= Configuration.MaxPoolSize)
            {
                Logger?.LogDebug(PoolConstants.Messages.PoolAtMaxSize);
                this.ActiveObjects.TryRemove(unwrapped, out _);
                statistics.TotalObjectsReturned++;
                statistics.CurrentActiveObjects = this.ActiveObjects.Count;
                statistics.CurrentAvailableObjects = this.AvailableObjects.Count;
                return;
            }
            
            this.ActiveObjects.TryRemove(unwrapped, out _);
            this.AvailableObjects.Push(unwrapped);
            
            statistics.TotalObjectsReturned++;
            statistics.CurrentActiveObjects = this.ActiveObjects.Count;
            statistics.CurrentAvailableObjects = this.AvailableObjects.Count;
            
            Logger?.LogDebug(PoolConstants.Messages.ObjectReturnedToPoolActiveAvailable,
                ActiveObjects.Count, AvailableObjects.Count);
        }

        /// <summary>
        /// Get objects from the pool which match the query. If no objects are available, an exception is thrown.
        /// </summary>
        /// <param name="query">the query to be performed</param>
        /// <returns>an object from the pool</returns>
        /// <exception cref="NoObjectsInPoolException">Thrown when no objects could be found</exception>
        public PoolModel<T> GetObject(Func<T, bool> query)
        {
            Logger?.LogDebug("Attempting to get object from pool using query. Available: {Count}", AvailableObjects.Count);
            
            if (this.ActiveObjects.Count >= Configuration.MaxActiveObjects)
            {
                Logger?.LogWarning(PoolConstants.Messages.MaxActiveLimitFormat, Configuration.MaxActiveObjects);
                throw new InvalidOperationException(string.Format(PoolConstants.Messages.MaxActiveLimitFormat, 
                    Configuration.MaxActiveObjects));
            }
            
            // Create a snapshot of available objects
            var availableObjects = this.AvailableObjects.ToArray();
            
            // Find a matching object in the snapshot
            var matchingObject = availableObjects.FirstOrDefault(query);
            if (matchingObject == null || EqualityComparer<T>.Default.Equals(matchingObject, default))
            {
                statistics.PoolEmptyCount++;
                Logger?.LogWarning(PoolConstants.Messages.NoObjectsInPoolMatchingYourQuery);
                throw new NoObjectsInPoolException(PoolConstants.Messages.NoObjectsInPoolMatchingYourQuery);
            }

            // Try to find and remove a matching object from the available objects
            bool foundMatch = false;
            T foundObject = default!;
            
            // Create a temporary stack to hold non-matching objects
            var tempStack = new ConcurrentStack<T>();
            
            // Pop items from available stack until we find a match or empty the stack
            while (!foundMatch && this.AvailableObjects.TryPop(out var item))
            {
                if (!foundMatch && query(item))
                {
                    // Found a matching object
                    foundMatch = true;
                    foundObject = item;
                }
                else
                {
                    // Not a match, push to temp stack
                    tempStack.Push(item);
                }
            }
            
            // Push all the non-matching items back to the available stack
            foreach (var item in tempStack)
            {
                this.AvailableObjects.Push(item);
            }
            
            if (!foundMatch)
            {
                // No matching object was available (might have been taken by another thread)
                statistics.PoolEmptyCount++;
                Logger?.LogWarning(PoolConstants.Messages.NoObjectsInPoolMatchingYourQuery);
                throw new NoObjectsInPoolException(PoolConstants.Messages.NoObjectsInPoolMatchingYourQuery);
            }
            
            // Add to active objects and return
            this.ActiveObjects.TryAdd(foundObject, 0);
            
            statistics.TotalObjectsRetrieved++;
            statistics.CurrentActiveObjects = this.ActiveObjects.Count;
            statistics.CurrentAvailableObjects = this.AvailableObjects.Count;
            if (statistics.CurrentActiveObjects > statistics.PeakActiveObjects)
            {
                statistics.PeakActiveObjects = statistics.CurrentActiveObjects;
            }

            Logger?.LogDebug(PoolConstants.Messages.ObjectMatchingQueryRetrievedFromPoolActiveAvailable, 
                ActiveObjects.Count, AvailableObjects.Count);
            
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
            try
            {
                Logger?.LogDebug(PoolConstants.Messages.AttemptingToGetObjectWithQueryFromPoolAvailableCount, AvailableObjects.Count);
                
                if (this.ActiveObjects.Count >= Configuration.MaxActiveObjects)
                {
                    Logger?.LogDebug(PoolConstants.Messages.CannotGetObjectActiveObjectsLimitMaxactiveReached, Configuration.MaxActiveObjects);
                    poolModel = null;
                    return false;
                }
                
                // Create a snapshot of available objects
                var availableObjects = this.AvailableObjects.ToArray();
                
                // First check if any object matches the query
                var matchingObject = availableObjects.FirstOrDefault(query);
                if (matchingObject == null || EqualityComparer<T>.Default.Equals(matchingObject, default))
                {
                    Logger?.LogDebug(PoolConstants.Messages.NoObjectsInPoolMatchTheQuery);
                    poolModel = null;
                    return false;
                }
                
                // Try to find and remove a matching object from the available objects
                bool foundMatch = false;
                T foundObject = default!;
                
                // Create a temporary stack to hold non-matching objects
                var tempStack = new ConcurrentStack<T>();
                
                // Pop items from available stack until we find a match or empty the stack
                while (!foundMatch && this.AvailableObjects.TryPop(out var item))
                {
                    if (!foundMatch && query(item))
                    {
                        // Found a matching object
                        foundMatch = true;
                        foundObject = item;
                    }
                    else
                    {
                        // Not a match, push to temp stack
                        tempStack.Push(item);
                    }
                }
                
                // Push all the non-matching items back to the available stack
                foreach (var item in tempStack)
                {
                    this.AvailableObjects.Push(item);
                }
                
                if (!foundMatch)
                {
                    // No matching object was available (might have been taken by another thread)
                    Logger?.LogDebug(PoolConstants.Messages.NoMatchingObjectAvailableRaceConditionTakenByAnotherThread);
                    poolModel = null;
                    return false;
                }
                
                // Add to active objects and return
                this.ActiveObjects.TryAdd(foundObject, 0);
                
                statistics.TotalObjectsRetrieved++;
                statistics.CurrentActiveObjects = this.ActiveObjects.Count;
                statistics.CurrentAvailableObjects = this.AvailableObjects.Count;
                if (statistics.CurrentActiveObjects > statistics.PeakActiveObjects)
                {
                    statistics.PeakActiveObjects = statistics.CurrentActiveObjects;
                }
                
                poolModel = new PoolModel<T>(foundObject, this);
                
                Logger?.LogDebug(PoolConstants.Messages.ObjectMatchingQueryRetrievedSuccessfullyActiveAvailable, 
                    ActiveObjects.Count, AvailableObjects.Count);
                return true;
            }
            catch (Exception ex)
            {
                // Handle any unexpected errors gracefully
                Logger?.LogError(ex, PoolConstants.Messages.ErrorOccurredWhileTryingToGetObjectWithQuery);
                poolModel = null;
                return false;
            }
        }

        /// <summary>
        /// Asynchronously get an object from the pool
        /// </summary>
        /// <param name="timeout">Maximum time to wait for an object</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A poolmodel</returns>
        public async Task<PoolModel<T>> GetObjectAsync(TimeSpan timeout = default, CancellationToken cancellationToken = default)
        {
            var effectiveTimeout = timeout == TimeSpan.Zero ? Configuration.DefaultTimeout : timeout;
            var deadline = DateTime.UtcNow.Add(effectiveTimeout);

            Logger?.LogDebug(PoolConstants.Messages.StartingAsyncObjectRetrievalWithTimeout, effectiveTimeout);

            while (!cancellationToken.IsCancellationRequested && DateTime.UtcNow < deadline)
            {
                if (TryGetObject(out var poolModel))
                {
                    Logger?.LogDebug(PoolConstants.Messages.AsyncRetrievalSuccess);
                    return poolModel!;
                }

                // Wait a short time before trying again
                await Task.Delay(PoolConstants.Thresholds.DefaultAsyncPollingDelayMs, cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                Logger?.LogDebug(PoolConstants.Messages.AsyncRetrievalCancelled);
                cancellationToken.ThrowIfCancellationRequested();
            }

            Logger?.LogWarning(PoolConstants.Messages.TimeoutWaitingFormat, effectiveTimeout);
            throw new TimeoutException(string.Format(PoolConstants.Messages.TimeoutWaitingFormat, effectiveTimeout));
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
            var effectiveTimeout = timeout == TimeSpan.Zero ? Configuration.DefaultTimeout : timeout;
            var deadline = DateTime.UtcNow.Add(effectiveTimeout);

            Logger?.LogDebug(PoolConstants.Messages.StartingAsyncObjectRetrievalWithQueryAndTimeoutTimeout, effectiveTimeout);

            while (!cancellationToken.IsCancellationRequested && DateTime.UtcNow < deadline)
            {
                if (TryGetObject(query, out var poolModel))
                {
                    Logger?.LogDebug(PoolConstants.Messages.SuccessfullyRetrievedObjectWithQueryAsynchronously);
                    return poolModel!;
                }

                // Wait a short time before trying again
                await Task.Delay(PoolConstants.Thresholds.DefaultAsyncPollingDelayMs, cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                Logger?.LogDebug(PoolConstants.Messages.AsyncRetrievalCancelled);
                cancellationToken.ThrowIfCancellationRequested();
            }

            Logger?.LogWarning(PoolConstants.Messages.TimeoutWaitingForObjectMatchingQueryFromPoolAfter, effectiveTimeout);
            throw new TimeoutException(string.Format(PoolConstants.Messages.TimeoutWaitingFormat, effectiveTimeout));
        }
    }
}

