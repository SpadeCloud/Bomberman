using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Bomberman.Network.Messages
{
    public class IdentityMessage : NetworkMessage
    {
        public override MessageType MessageType { get { return MessageType.Identity; } }
        public int Id { get; set; }
        public IdentityMessage(int id)
        {
            Id = id;
        }

        public IdentityMessage(byte[] message)
            : base(message)
        {
            /*using (var br = new BinaryReader(new MemoryStream(message)))
            {
                br.ReadInt32(); // type
                Id = br.ReadInt32();
            }*/
        }

      /*  public byte[] ToBytes()
        {
            byte[] result = new byte[8];
            using (var bw = new BinaryWriter(new MemoryStream(result)))
            {
                bw.Write((int)MessageType.Identity);
                bw.Write(Id);
            }
            return result;
        }*/
    }
}
