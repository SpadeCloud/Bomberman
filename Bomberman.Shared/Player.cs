using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using Bomberman.Network;
using Bomberman.Network.Messages;

namespace Bomberman.Shared
{
    public class Player
    {
        public int Id { get; private set; }
        public Point Location { get; set; }
        public bool IsDead { get; set; }
        public int Color { get; private set; }
        public ClientSocket Socket { get; private set; }
        public DateTime ProcessMovement { get; set;}
        public bool PlayerReady { get; set; }

        public Player(ClientSocket socket, int playerId, Point location, int? color = null)
        {
            Socket = socket;
            Id = playerId;
            Location = location;
            IsDead = false;
            if (color != null)
                Color = (int)color;
            else
            {
                var r = World.State.Rng.Next(255);
                var g = World.State.Rng.Next(255);
                var b = World.State.Rng.Next(255);

                Color = (r << 16) | (g << 8) | b;
            }
            ProcessMovement = DateTime.UtcNow;
            PlayerReady = false;
        }

        public bool MoveTo(Point p, bool force = false)
        {
            if (!force)
            {
                if ((DateTime.UtcNow - ProcessMovement).TotalMilliseconds < 150)
                    return false;

                if (IsDead)
                    return false;
            }

            if (!World.State.IsOpenSpace(p.X, p.Y))
                return false;

            if (World.IsServer)
            {
                Location = p;
                return true;
            }
            else //when it's client 
            {
                // notify the server of intent to move
                var msg = new LocationMessage(this.Id, p.X, p.Y);
                Socket.Send(msg.ToBytes());
                return true;
            }
        }

        public void Kill()
        {
            IsDead = true;
            //this runs AFTER game is reest, without the IF() the player that ran into the bomb would be despawned after game reset
            if (World.State.GameStart)
            {
                World.State.SendToAll(new DespawnMessage(SpawnType.Player, this.Id));
            }
        }

        public void DropBomb()
        {
            if(!IsDead)
            {
                if (World.IsServer)
                    World.State.SpawnBomb(this);

                else
                {
                    var msg = new SpawnMessage(SpawnType.Bomb, -1, this.Location.X, this.Location.Y);
                    Socket.Send(msg.ToBytes());
                }

            }
                
            //player still "exists" but cannot play anymore (move/dropbomb)
        }
        public void ReadyUp()
        {
            if(!IsDead)
            {
                if (!World.IsServer)
                {
                    //player tells server its ready
                    var msg = new StartGameMessage(true);
                    Socket.Send(msg.ToBytes());
                }
            }
        }
        public void Send(NetworkMessage message)
        {
            Socket.Send(message.ToBytes());
        }
    }
}
