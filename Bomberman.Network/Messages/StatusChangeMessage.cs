using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Bomberman.Network.Messages
{
    public class StatusChangeMessage : NetworkMessage
    {
        public override MessageType MessageType { get { return MessageType.StatusChange; } }
        public SpawnType Type { get; private set; }
        public int Id { get; private set; }
        public bool StatusChange { get; private set; }
        public int Count { get; private set; }
        public StatusChangeMessage(SpawnType type, int id, bool change = false, int count = 0)
        {
            Type = type;
            Id = id;
            StatusChange = change;
            Count = count;
        }
        public StatusChangeMessage(byte[] message)
            : base(message)
        {
           /* using (var br = new BinaryReader(new MemoryStream(message)))
            { 
                br.ReadInt32();
                Type = (SpawnType)br.ReadInt32();
                Id = br.ReadInt32();
                StatusChange = br.ReadBoolean();
                Count = br.ReadInt32();
            }*/

        }
     /*   public byte[] ToBytes()
        {
            var result = new byte[17];
            using (var bw = new BinaryWriter(new MemoryStream(result)))
            {
                bw.Write((int)MessageType.StatusChange);
                bw.Write((int)Type);
                bw.Write(Id);
                bw.Write(StatusChange);
                bw.Write(Count);
            }
            return result;
        }*/
    }
}
