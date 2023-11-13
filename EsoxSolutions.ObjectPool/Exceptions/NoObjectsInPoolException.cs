using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EsoxSolutions.ObjectPool.Exceptions
{
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
