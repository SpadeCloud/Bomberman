using Bomberman.Network.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bomberman.Network.Attributes
{
    public class NetworkMessageAttribute :Attribute
    {
        public MessageType Type { get; private set; }

        public NetworkMessageAttribute(MessageType type) 
        { 
            Type= type;
        }
    }   
}
