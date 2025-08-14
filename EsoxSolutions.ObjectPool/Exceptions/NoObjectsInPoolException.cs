using EsoxSolutions.ObjectPool.Constants;

namespace EsoxSolutions.ObjectPool.Exceptions
{
    /// <summary>
    /// Raised when no objects could be retrieved from the pool.
    /// </summary>
    public class NoObjectsInPoolException : Exception
    {
        /// <summary>
        /// Constructor for exception with custom message
        /// </summary>
        /// <param name="message">The exception message</param>
        public NoObjectsInPoolException(string message) : base(message)
        {
        }

        /// <summary>
        /// Constructor for exception with message and inner exception
        /// </summary>
        /// <param name="message">The exception message</param>
        /// <param name="innerException">The inner exception</param>
        public NoObjectsInPoolException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// Default constructor with standard message
        /// </summary>
        public NoObjectsInPoolException() : base(PoolConstants.Messages.NoAvailableObjects)
        {
        }


    }
}
