using Bomberman.Network.Messages;
using Bomberman.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Bomberman.Shared
{
    public class Wall
    {
        private const int PERMANENT_DURABILITY = 100000;
        private const int NORMAL_DURABILITY = 2; 

        public int Durability { get; private set; }
        public int MaxDurability { get; private set; }

        public bool IsPermanent
        {
            get { return Durability == PERMANENT_DURABILITY; }
        }
        public Point Location { get; private set; }
        public int Id { get; private set; }

        public Wall(int id, Point location, bool permanent)
        {
            Id = id;
            Location = location;

            if(permanent)
                MaxDurability = PERMANENT_DURABILITY;
            else
                MaxDurability = NORMAL_DURABILITY;

                Durability = MaxDurability;
        }
        public void DamageWall()
        {
            //check if wall is permanent
            if(IsPermanent)
                return; //bombs will try to damage all walls in their explosion points

            Durability--;
            if (World.IsServer)
            {
                if (Durability > 0)
                {
                    World.State.SendToAll(new StatusChangeMessage(SpawnType.Wall, this.Id));
                }
                Console.WriteLine($"wall at {Location.X},{Location.Y} has {Durability} durability remaining;");
                if (Durability == 0)
                {
                    DestroyWall();
                }
            }
        }
        private void DestroyWall()
        {
            if(!World.IsServer)
                throw new ServerSideOnlyException();
            if (IsPermanent)
                return;
            World.State.RemoveObject(SpawnType.Wall, Id);
            World.State.SendToAll(new DespawnMessage(SpawnType.Wall, Id));      
        }
        public void ShrinkChangeWall()
        {
            Durability = PERMANENT_DURABILITY;
            MaxDurability = PERMANENT_DURABILITY;
            if (World.IsServer)
                World.State.SendToAll(new StatusChangeMessage(SpawnType.Wall, this.Id, true));
        }
    }
}
