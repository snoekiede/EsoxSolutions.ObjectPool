using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EsoxSolutions.ObjectPool.Exceptions
{
    /// <summary>
    /// Raised when no objects could be released from the pool.
    /// </summary>
    public class NoObjectsInPoolException:Exception
    {
        public NoObjectsInPoolException(string message):base(message)
        {
        }

        public NoObjectsInPoolException(string message, Exception innerException):base(message, innerException)
        {
        }

        public NoObjectsInPoolException():base()
        {
        }

        

    }
}
