using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Bomberman.Network.Messages
{
    public class DespawnMessage : NetworkMessage
    {
        public override MessageType MessageType { get { return MessageType.Despawn; } }
        public SpawnType Type { get; set; }
        public int  Id { get; set; }


        public DespawnMessage(SpawnType type, int id)
        {
            Type = type;
            Id = id;
        }

        public DespawnMessage(byte[] message)
            : base(message)
        {
            /*using (var br = new BinaryReader(new MemoryStream(message)))
            {
                br.ReadInt32();
                Type = (SpawnType)br.ReadInt32();
                Id = br.ReadInt32();
            }*/
        }

       /* public byte[] ToBytes()
        {
            byte[] result = new byte[12];
            using (var bw = new BinaryWriter(new MemoryStream(result)))
            {
                //bw(binary writer) writes/inputs into the memory stream, whose size is 
                //determined by result(byte[] given) and the memory stream accounts for offset/placemnt/index
                bw.Write((int)MessageType.Despawn);
                bw.Write((int)Type);
                bw.Write(Id);
            }
            return result;
        }*/
    }
}
