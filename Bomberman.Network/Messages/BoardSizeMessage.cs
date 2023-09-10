using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.ComponentModel;

namespace Bomberman.Network.Messages
{
    public class BoardSizeMessage : NetworkMessage
    {
        public override MessageType MessageType { get { return MessageType.BoardSize; } }
        public int BoardWidth { get; private set; }
        public int BoardHeight { get; private set; }

        public BoardSizeMessage(int width, int height)
        {
            BoardWidth = width;
            BoardHeight = height;
        }

        public BoardSizeMessage(byte[] message)
            : base(message)
        {
           /* using (var br = new BinaryReader(new MemoryStream(data)))
            {
                br.ReadInt32();
                BoardWidth = br.ReadInt32();
                BoardHeight = br.ReadInt32();
            }*/
        }

     /*  public byte[] ToBytes()
        {
            byte[] result = new byte[12];
            using (var bw = new BinaryWriter(new MemoryStream(result)))
            {
                bw.Write((int)MessageType.BoardSize);
                bw.Write(BoardWidth);
                bw.Write(BoardHeight);
            }
            return result;
        }*/
    }
}
