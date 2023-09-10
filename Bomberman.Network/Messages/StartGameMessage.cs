using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Bomberman.Network.Messages
{
    public class StartGameMessage: NetworkMessage
    {
        public override MessageType MessageType { get { return MessageType.StartGame; } }
        public bool StartGame { get; private set; }
        public DateTime StartTime { get; private set; }

        public StartGameMessage(bool start, DateTime? time = null)
        {
            StartGame = start;
            if (time != null)
                StartTime = (DateTime)time;
            else
                StartTime = DateTime.MaxValue;
        }

        public StartGameMessage(byte[] message)
            : base(message)
        {
            /*using (var br = new BinaryReader(new MemoryStream(data))) 
            {
                br.ReadInt32();
                StartGame = br.ReadBoolean();
                StartTime = new DateTime(br.ReadInt64());
            }*/
        }

    /*    public byte[] ToBytes()
        {
            var result = new byte[13];
            using( var bw = new BinaryWriter(new MemoryStream(result)))
            {
                bw.Write((int)MessageType.StartGame);
                bw.Write(StartGame);
                bw.Write(StartTime.Ticks);
            }
            return result;
        }*/
    }
}
