using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace Bomberman.Network
{
    public class ServerSocket
    {
        public event Action<ClientSocket> ClientConnected;
        public event Action<ClientSocket> ClientDisconnected;
        public event Action<ClientSocket, byte[]> ClientMessage;

        private Socket m_Socket;
        private int m_NextClientId;
        private Dictionary<int, ClientSocket> m_Clients;

        public ClientSocket[] Clients {  get {  return m_Clients.Values.ToArray(); } }  

        public ServerSocket(int port)
        {            
            m_Clients = new Dictionary<int, ClientSocket>();

            // Server side
            // creates the socket
            m_Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // binds the socket to port 9337 (reversing "room" under ip address @ 9337)
            m_Socket.Bind(new IPEndPoint(IPAddress.Any, port));
        }

        public void Start()
        {
            // listen for incoming client sockets (start accepting guests into the room)
            m_Socket.Listen(100);
            m_Socket.BeginAccept(AcceptClient, null);
        }

        private void AcceptClient(IAsyncResult res)
        {
            //allows ONE client into the room
            try
            {
                var client = new ClientSocket(++m_NextClientId, m_Socket.EndAccept(res), this);
                m_Clients[client.Id] = client;

                client.Message += (message) => ClientMessage?.Invoke(client, message);
                client.Disconnected += (e) =>
                {
                    m_Clients.Remove(client.Id);
                    ClientDisconnected?.Invoke(client);
                };

                if (ClientConnected != null)
                    ClientConnected(client);

                client.StartReceive();
            }
            catch (SocketException)
            {
                // do nothing
            }

            m_Socket.BeginAccept(AcceptClient, null);
        }
        public void Stop()
        {
            m_Socket.Close();
        }
    }
}
