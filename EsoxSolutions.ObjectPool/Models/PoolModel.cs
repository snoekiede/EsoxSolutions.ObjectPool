using EsoxSolutions.ObjectPool.Interfaces;

namespace EsoxSolutions.ObjectPool.Models
{
    /// <summary>
    /// A wrapper for the object pool
    /// </summary>
    /// <typeparam name="T">The type of the object to be wrapped</typeparam>
    public sealed class PoolModel<T> : IDisposable
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
            ArgumentNullException.ThrowIfNull(value);
            ArgumentNullException.ThrowIfNull(pool);
            
            this._value = value;
            this._pool = pool;
        }

        /// <summary>
        /// Unwraps the value
        /// </summary>
        /// <returns>The value</returns>
        /// <exception cref="ObjectDisposedException">Thrown when trying to access a disposed object</exception>
        public T Unwrap()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
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
