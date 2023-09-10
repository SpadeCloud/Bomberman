using Bomberman.Network.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Bomberman.Network.Attributes;

namespace Bomberman.Network
{
    public class NetworkMessageDispatcher
    {
        private Dictionary<MessageType, Action<byte[]>> m_Actions;

        public NetworkMessageDispatcher()
        {
            m_Actions = new Dictionary<MessageType, Action<byte[]>>();
        }

        public void RegisterMethods(object instance)
        {
            Type type = instance.GetType();
            MethodInfo[] methods = type.GetMethods();
            foreach (MethodInfo methodInfo in methods)
            {
                NetworkMessageAttribute attr = methodInfo.GetCustomAttribute<NetworkMessageAttribute>();
                if (attr != null)
                {
                    // We know that the method that the MethodInfo contains info about should look like
                    // void Method(byte[] message)
                    // because it was marked with NetworkMessageAttribute
                    // we also know that because of this, this method qualifies as an Action<byte[]>

                    // Action<byte[]> action = instance.Method;
                    Action<byte[]> action = (Action<byte[]>)methodInfo.CreateDelegate(typeof(Action<byte[]>), instance);
                    
                    m_Actions[attr.Type] = action;
                }
            }
        }

        //2.
        public void Dispatch(byte[] message)
        {
            //3.  finds the KEY to pull from dictionary 
            MessageType type = (MessageType)BitConverter.ToInt32(message, 0);
            
            //4. stores the ACTION<BYTE[]> associated to the KEY (to call associated method)
            Action<byte[]> action;

            //5. attempt to pull from dictionary
            if (m_Actions.TryGetValue(type, out action))
                //6. call appropriate method
                action.Invoke(message);
                
            else
                throw new ArgumentException($"No method exists for type {type}", nameof(type));
        }
    }
}
