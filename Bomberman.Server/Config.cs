using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bomberman.Server
{
    public class Config
    {
        public bool? BombChaining { get; set; }
        public int? BoardWidth { get; set; }
        public int? BoardHeight { get; set; }
        public bool? AllWallsPermanent { get; set; } 

        public Config()
        {

        }
    }
}
