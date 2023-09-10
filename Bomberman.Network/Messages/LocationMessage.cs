using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Bomberman.Network.Messages
{
    public class LocationMessage : NetworkMessage
    {
        public override MessageType MessageType {  get {  return MessageType.Location; } }

        public int X { get; set; }
        public int Y { get; set; }
        public int PlayerId { get; set; }
        public LocationMessage(int playerId, int x, int y)
        {
            PlayerId = playerId;
            X = x;
            Y = y;
        }

        public LocationMessage(byte[] message)
            : base(message)
        {
           /* using (var br = new BinaryReader(new MemoryStream(message)))
            {
                br.ReadInt32(); // type, always reads in groups of 4 bytes
                PlayerId = br.ReadInt32();
                X = br.ReadInt32();
                Y = br.ReadInt32();
            }*/
        }

    /*    public byte[] ToBytes()
        {
            byte[] result = new byte[16];
            using (var bw = new BinaryWriter(new MemoryStream(result)))
            {
                bw.Write((int)MessageType.Location);
                bw.Write(PlayerId);
                bw.Write(X);
                bw.Write(Y);
            }
            return result;
        }*/

    }
}
