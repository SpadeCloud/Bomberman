using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace Bomberman.Network
{
    public class ClientSocket
    {
        public event Action Connected;
        public event Action<byte[]> Message;
        public event Action<Exception> Disconnected;

        private Socket m_Socket;
        private byte[] m_Buffer;
        private ServerSocket m_Server;
        private int m_ExpectedMessageSize;
        private int m_Offset;
        private bool m_HasReceivedSize;

        public int Id { get; private set; }
        public object State { get; set; }

        public ClientSocket(int id, Socket clientSocket, ServerSocket server) //server side of things (for/how server handles client connects in)
        {
            Id = id;
            m_Socket = clientSocket;
            m_Server = server;
            m_Buffer = new byte[0];

            Initialize();
        }

        public ClientSocket() //client side of things (like when client connects in)
        {
            m_Socket = null;
            m_Server = null;
            m_Buffer = new byte[0];

            Initialize();
        }

        public void Connect(string ip, int port)
        {
            Connect(new IPEndPoint(IPAddress.Parse(ip), port));
        }

        public void Connect(IPEndPoint endpoint)
        {
            if (m_Socket == null)
                m_Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            m_Socket.BeginConnect(endpoint, (res) =>
            {
                //ENDCONNECT, ends looking for IP to connect to
                //has connected or throw error
                m_Socket.EndConnect(res);
                if (Connected != null)
                    Connected();

                StartReceive();

            }, null);
        }

        public void Send(byte[] message)
        {
            if (m_Socket == null)
                throw new InvalidOperationException("Socket cannot be null");

            byte[] buffer = new byte[sizeof(int) + message.Length];
            using (var bw = new BinaryWriter(new MemoryStream(buffer)))
            {
                bw.Write((int)message.Length);
                bw.Write(message);
            }

            m_Socket.BeginSend(buffer, 0, buffer.Length, SocketFlags.None, (res) =>
            {
                try
                {
                    m_Socket.EndSend(res);
                }
                catch (SocketException)
                {
                    Disconnect();
                }

            }, null);
        }

        public void Disconnect()
        {
            try
            {
                if (m_Socket == null)
                    return;

                m_Socket.Disconnect(false);
            }
            catch (SocketException)
            {
                //
            }


            if (Disconnected != null)
                Disconnected(null);
        }

        private void Initialize()
        {
            m_Buffer = new byte[1024];
            m_ExpectedMessageSize = 4;
            m_Offset = 0;
            m_HasReceivedSize = false;
        }

        public void StartReceive()
        {
            m_Socket.BeginReceive(m_Buffer, m_Offset, m_ExpectedMessageSize, SocketFlags.None, Receive, null);
        }

        private void Receive(IAsyncResult res)
        {
            int messageLength;
            try
            {
                messageLength = m_Socket.EndReceive(res);
                if (messageLength == 0)
                {
                    if (Disconnected != null)
                        Disconnected(null);

                    return;
                }
            }
            catch (SocketException ex)
            {
                if (Disconnected != null)
                    Disconnected(null);

                return;
            }
            
            m_ExpectedMessageSize -= messageLength;
            m_Offset += messageLength;

            if (m_ExpectedMessageSize == 0)
            {
                //end receive pt 1
                if(!m_HasReceivedSize)//no assigned size for the FOLLOWING packet yet
                {
                    byte[] packetSizeBytes = new byte[m_Offset];
                    Array.Copy(m_Buffer, packetSizeBytes, m_Offset);

                    int packetSize = BitConverter.ToInt32(packetSizeBytes, 0);
                   
                    m_HasReceivedSize=true;
                    m_ExpectedMessageSize = packetSize;
                    m_Offset=0;
                }
                else
                {
                    byte[] message = new byte[m_Offset];
                    Array.Copy(m_Buffer, message, m_Offset);

                    if (Message != null)
                        Message(message);

                    m_HasReceivedSize = false;
                    m_ExpectedMessageSize = 4;
                    m_Offset = 0;
                }
            }

            StartReceive();
        }

        
    }
}
