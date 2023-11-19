namespace EsoxSolutions.ObjectPool.Exceptions
{
    /// <summary>
    /// Raised when no objects could be released from the pool.
    /// </summary>
    public class NoObjectsInPoolException:Exception
    {
        /// <summary>
        /// Constructor for exception
        /// </summary>
        /// <param name="message">The exception exception</param>
        public NoObjectsInPoolException(string message):base(message)
        {
        }

        /// <summary>
        /// Constructor for exception
        /// </summary>
        /// <param name="message">the exception message</param>
        /// <param name="innerException">the inner exception</param>
        public NoObjectsInPoolException(string message, Exception innerException):base(message, innerException)
        {
        }

        /// <summary>
        /// Empty constructor
        /// </summary>
        public NoObjectsInPoolException():base()
        {
        }

        

    }
}
