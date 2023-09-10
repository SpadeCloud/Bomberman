using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Bomberman.Network.Messages
{
    public enum SpawnType
    {
        Player = 1,
        Wall = 2,
        Bomb = 3
    }


    
    public class SpawnMessage : NetworkMessage
    {
        public override MessageType MessageType { get { return MessageType.Spawn; } }
        public SpawnType Type { get; set; }
        public int Id { get; set; }
        public int X { get; set; }
        public int Y { get; set; }

        // used for players
        public int Color { get; set; }

        // used for bombs
        public DateTime DetonateTime { get; set; }
        public int DetonateSize { get; set; }
        public int? OwnerId { get; set; }

        //used for walls
        public bool IsPermanent { get; set; }

        public SpawnMessage(SpawnType type, int id, int x, int y)
        {
            Type = type;
            Id = id;
            X = x;
            Y = y;
        }

#warning network message would not work for this due to the unused properties depending on spawn type
        public SpawnMessage(byte[] message)

        {
            using (var br = new BinaryReader(new MemoryStream(message)))
            {
                br.ReadInt32(); // see LOCATIONMESSAGE for notes
                Type = (SpawnType)br.ReadInt32();
                Id = br.ReadInt32();
                X = br.ReadInt32();
                Y = br.ReadInt32();

                if (Type == SpawnType.Player)
                {
                    Color = br.ReadInt32();
                }
                else if (Type == SpawnType.Bomb)
                {
                    DetonateTime = new DateTime(br.ReadInt64());
                    DetonateSize = br.ReadInt32();

                    OwnerId = br.ReadInt32();
                    OwnerId = (OwnerId == 0) ? null : OwnerId;
                }
                else if(Type == SpawnType.Wall)
                {
                    IsPermanent = br.ReadBoolean();
                }
            }
        }
#warning writing/reading changes based on spawnTYPE so... not using the parentMethod
        public override byte[] ToBytes()
        {
            byte[] result = new byte[36];
            using (var bw = new BinaryWriter(new MemoryStream(result)))
            {
                bw.Write((int)MessageType.Spawn);
                bw.Write((int)Type);
                bw.Write(Id);
                bw.Write(X);
                bw.Write(Y);

                if (Type == SpawnType.Player)
                {
                    bw.Write(Color);
                }
                else if (Type == SpawnType.Bomb)
                {
                    bw.Write(DetonateTime.Ticks);
                    bw.Write(DetonateSize);
                    bw.Write(OwnerId ?? 0);
                }
                else if (Type == SpawnType.Wall)
                {
                    bw.Write(IsPermanent);
                }
            }
            return result;
        }
    }
}
