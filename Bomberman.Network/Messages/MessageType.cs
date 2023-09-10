using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bomberman.Network.Attributes;

namespace Bomberman.Network.Messages
{
    public enum MessageType
    {
        Identity = 1, //playerid

        Spawn = 2, //notify that a player/wall/bomb has spawned
        Location = 3, //player has moved
        Despawn =4, //remove player or bomb
        StatusChange = 5, //sned bombID of exploding bomb
                      //send wallID of damaged wall
                      //send playerID of new-ready-player
        StartGame = 6, //server starts game (or resets?)
        BoardSize = 7, //tells the clients the board size
    }
}
