using EsoxSolutions.ObjectPool.Constants;
using EsoxSolutions.ObjectPool.Exceptions;
using EsoxSolutions.ObjectPool.Models;
using Microsoft.Extensions.Logging;

namespace EsoxSolutions.ObjectPool.Pools
{
    /// <summary>
    /// Dynamic object pool that can create new objects using a factory method when the pool is empty
    /// </summary>
    /// <typeparam name="T">the type to be stored in the object pool</typeparam>
    public class DynamicObjectPool<T> : ObjectPool<T> where T:class
    {
        /// <summary>
        /// The factory method to be used to create new objects
        /// </summary>
        private readonly Func<T>? _factory;        

        /// <summary>
        /// The constructor for the queryable object pool
        /// </summary>
        /// <param name="initialObjects">the initial objects</param>
        public DynamicObjectPool(List<T> initialObjects) : base(initialObjects)
        {
        }

        /// <summary>
        /// The constructor for the dynamic object pool
        /// </summary>
        /// <param name="factory">creation function for new objects</param>
        public DynamicObjectPool(Func<T> factory) : base([])
        {
            this._factory = factory;
        }

        /// <summary>
        /// The constructor for the dynamic object pool
        /// </summary>
        /// <param name="factory">creation function for new objects</param>
        /// <param name="initialObjects">list of initial objects</param>
        public DynamicObjectPool(Func<T> factory, List<T> initialObjects) : base(initialObjects)
        {
            this._factory = factory;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicObjectPool{T}"/> class with a specified factory method,
        /// an initial collection of objects, optional pool configuration, and an optional logger.
        /// </summary>
        /// <remarks>The <paramref name="factory"/> parameter is required and must not be null. The
        /// <paramref name="initialObjects"/> list can be empty, but it must not be null. If <paramref
        /// name="configuration"/> is provided, it will override the default pool behavior. The logger can be used to
        /// monitor pool activity, such as object creation and disposal.</remarks>
        /// <param name="factory">A function that creates new instances of the pooled object. This function is invoked when the pool needs to
        /// create additional objects.</param>
        /// <param name="initialObjects">A list of pre-created objects to populate the pool initially. These objects will be available for reuse
        /// immediately.</param>
        /// <param name="configuration">Optional configuration settings for the object pool, such as maximum pool size and eviction policies. If
        /// null, default settings are used.</param>
        /// <param name="logger">An optional logger instance for logging pool-related events. If null, no logging will be performed.</param>
        public DynamicObjectPool(Func<T> factory,List<T> initialObjects, PoolConfiguration? configuration, ILogger<ObjectPool<T>>? logger = null): base(initialObjects, configuration, logger)
        {
            this._factory = factory;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicObjectPool{T}"/> class with the specified factory
        /// method, configuration, and optional logger.
        /// </summary>
        /// <param name="factory">A function that creates new instances of the pooled object. This function is called when the pool needs to
        /// allocate a new object.</param>
        /// <param name="configuration">An optional <see cref="PoolConfiguration"/> object that specifies the settings for the object pool, such as
        /// maximum size and eviction policies. If null, default settings are used.</param>
        /// <param name="logger">An optional <see cref="ILogger{TCategoryName}"/> instance used to log diagnostic information about the
        /// pool's behavior. If null, no logging is performed.</param>
        public DynamicObjectPool(Func<T> factory, PoolConfiguration? configuration, ILogger<ObjectPool<T>>? logger = null) : base([], configuration, logger)
        {
            this._factory = factory;
        }
        /// <summary>
        /// Returns an object from the pool. If no objects are available, an exception is thrown.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NoObjectsInPoolException">Thrown if no object could be found</exception>
        public override PoolModel<T> GetObject()
        {
            T? result;

            // No lock needed, use concurrent collections
            if (this.AvailableObjects.IsEmpty)
            {
                result = this._factory?.Invoke();
                if (result == null)
                {
                    throw new UnableToCreateObjectException(PoolConstants.Messages.CannotCreateObject);
                }
                this.AvailableObjects.Push(result);
            }
            if (!this.AvailableObjects.TryPop(out result!))
            {
                throw new NoObjectsInPoolException(PoolConstants.Messages.NoAvailableObjects);
            }
            this.ActiveObjects.TryAdd(result, 0);
            return new PoolModel<T>(result, this);
        }
    }
}
