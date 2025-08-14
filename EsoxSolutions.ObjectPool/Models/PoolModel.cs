using EsoxSolutions.ObjectPool.Interfaces;

namespace EsoxSolutions.ObjectPool.Models
{
    /// <summary>
    /// A wrapper for the object pool
    /// </summary>
    /// <typeparam name="T">The type of the object to be wrapped</typeparam>
    public class PoolModel<T> : IDisposable
    {
        private readonly T _value;
        private readonly IObjectPool<T> _pool;
        private bool _disposed = false;

        /// <summary>
        /// Constructor for the pool model
        /// </summary>
        /// <param name="value">The value to be wrapped</param>
        /// <param name="pool">The object pool to which this PoolModel belongs</param>
        public PoolModel(T value, IObjectPool<T> pool)
        {
            this._value = value ?? throw new ArgumentNullException(nameof(value));
            this._pool = pool ?? throw new ArgumentNullException(nameof(pool));
        }

        /// <summary>
        /// Unwraps the value
        /// </summary>
        /// <returns>The value</returns>
        /// <exception cref="ObjectDisposedException">Thrown when trying to access a disposed object</exception>
        public T Unwrap()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(PoolModel<T>));
            return this._value;
        }

        /// <summary>
        /// Returns the poolmodel to the pool
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                this._pool.ReturnObject(this);
                _disposed = true;
            }
        }
    }
}
