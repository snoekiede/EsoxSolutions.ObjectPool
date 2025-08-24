using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EsoxSolutions.ObjectPool.Constants;

namespace EsoxSolutions.ObjectPool.Exceptions
{
    public class UnableToCreateObjectException: Exception
    {
        /// <summary>
        /// Constructor for exception with custom message
        /// </summary>
        /// <param name="message">The exception message</param>
        public UnableToCreateObjectException(string message) : base(message)
        {
        }

        /// <summary>
        /// Constructor for exception with message and inner exception
        /// </summary>
        /// <param name="message">The exception message</param>
        /// <param name="innerException">The inner exception</param>
        public UnableToCreateObjectException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// Default constructor with standard message
        /// </summary>
        public UnableToCreateObjectException() : base(PoolConstants.Messages.CannotCreateObject)
        {
        }
    }
}
