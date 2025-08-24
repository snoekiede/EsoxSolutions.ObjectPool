using EsoxSolutions.ObjectPool.Constants;
using EsoxSolutions.ObjectPool.Exceptions;
using EsoxSolutions.ObjectPool.Interfaces;
using EsoxSolutions.ObjectPool.Models;
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
        /// The constructor for the queryable object pool
        /// </summary>
        /// <param name="initialObjects">the list of initial objects</param>
        public QueryableObjectPool(List<T> initialObjects)
        {
            this.ActiveObjects = new ConcurrentDictionary<T, byte>();
            this.AvailableObjects = new ConcurrentStack<T>(initialObjects);
        }

        /// <summary>
        /// Gets the number of available objects in the pool
        /// </summary>
        public int AvailableObjectCount => this.AvailableObjects.Count;

        /// <summary>
        /// Returns an object from the pool. If no objects are available, an exception is thrown.
        /// </summary>
        /// <returns>A PoolModel object</returns>
        /// <exception cref="NoObjectsInPoolException">Raised when no object could be found</exception>
        public virtual PoolModel<T> GetObject()
        {
            if (!this.AvailableObjects.TryPop(out var result))
            {
                throw new NoObjectsInPoolException("No objects available");
            }
            
            this.ActiveObjects.TryAdd(result, 0);
            
            return new PoolModel<T>(result, this);
        }

        /// <summary>
        /// Try to get an object from the pool without throwing an exception
        /// </summary>
        /// <param name="poolModel">The pool model if successful</param>
        /// <returns>True if an object was retrieved, false otherwise</returns>
        public bool TryGetObject(out PoolModel<T>? poolModel)
        {
            if (!this.AvailableObjects.TryPop(out var result))
            {
                poolModel = null;
                return false;
            }

            this.ActiveObjects.TryAdd(result, 0);
            poolModel = new PoolModel<T>(result, this);
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
                throw new NoObjectsInPoolException(PoolConstants.Messages.ObjectNotInPool);
            }
            
            this.ActiveObjects.TryRemove(unwrapped, out _);
            this.AvailableObjects.Push(unwrapped);
        }

        /// <summary>
        /// Get objects from the pool which match the query. If no objects are available, an exception is thrown.
        /// </summary>
        /// <param name="query">the query to be performed</param>
        /// <returns>an object from the pool</returns>
        /// <exception cref="NoObjectsInPoolException">Thrown when no objects could be found</exception>
        public PoolModel<T> GetObject(Func<T, bool> query)
        {
            // Create a snapshot of available objects
            var availableObjects = this.AvailableObjects.ToArray();
            
            // Find a matching object in the snapshot
            var matchingObject = availableObjects.FirstOrDefault(query);
            if (matchingObject == null || EqualityComparer<T>.Default.Equals(matchingObject, default))
            {
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
                throw new NoObjectsInPoolException(PoolConstants.Messages.NoObjectsInPoolMatchingYourQuery);
            }
            
            // Add to active objects and return
            this.ActiveObjects.TryAdd(foundObject, 0);
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
                // Create a snapshot of available objects
                var availableObjects = this.AvailableObjects.ToArray();
                
                // First check if any object matches the query
                var matchingObject = availableObjects.FirstOrDefault(query);
                if (matchingObject == null || EqualityComparer<T>.Default.Equals(matchingObject, default))
                {
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
                    poolModel = null;
                    return false;
                }
                
                // Add to active objects and return
                this.ActiveObjects.TryAdd(foundObject, 0);
                poolModel = new PoolModel<T>(foundObject, this);
                return true;
            }
            catch
            {
                // Handle any unexpected errors gracefully
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
            var deadline = timeout == TimeSpan.Zero ? DateTime.MaxValue : DateTime.UtcNow.Add(timeout);

            while (!cancellationToken.IsCancellationRequested && DateTime.UtcNow < deadline)
            {
                if (TryGetObject(out var poolModel))
                {
                    return poolModel!;
                }

                // Wait a short time before trying again
                await Task.Delay(PoolConstants.Thresholds.DefaultAsyncPollingDelayMs, cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            throw new TimeoutException(string.Format(PoolConstants.Messages.TimeoutWaitingFormat, timeout));
        }
    }
}

