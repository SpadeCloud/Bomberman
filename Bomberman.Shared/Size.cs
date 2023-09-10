using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bomberman.Shared
{
    public struct Size
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public Size(int w, int h)
        {
            Width = w;
            Height = h;
        }
    }
}
