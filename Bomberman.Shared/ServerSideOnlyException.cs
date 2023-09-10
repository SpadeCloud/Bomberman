using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bomberman.Shared
{
    public class ServerSideOnlyException : Exception
    {
        public ServerSideOnlyException(string message = null) 
            : base(message ?? "This method should only be called on the server side")
        {

        }
    }
}
