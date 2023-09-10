using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Threading;
using Bomberman.Network;
using Bomberman.Network.Messages;

namespace Bomberman.Shared
{
    public class World
    {
        private const int DEFAULT_GAME_WIDTH = 16;
        private const int DEFAULT_GAME_HEIGHT = 16;
        private const int BEGIN_GAME_COUNTDOWN = 5;

        public const int MIN_GAME_WIDTH = 5;
        public const int MIN_GAME_HEIGHT = 5;

        public const int DEFAULT_BOMB_EXPLODE_SECS = 5;
        public const int BOMB_EXPLOSION_SIZE = 3;

        public const int TIME_UNTIL_MAP_SHRINKS = 15;
        public const int TIME_BETWEEN_WALL_ADDITION = 2;

        public static World State { get; private set; }
        public static bool IsServer { get; set; }
        
        static World()
        {
            State = new World();
        }

        private List<Wall> m_Walls;
        private List<Bomb> m_Bombs;
        private List<Player> m_Players;
        private Thread m_Worker;
        private bool m_AbortWorker;

        public Counter ObjectIdCounter { get; private set; }
        public Wall[] Walls {  get { return m_Walls.ToArray(); } }
        public Bomb[] Bombs {  get { return m_Bombs.ToArray(); } }
        public Player[] Players {  get { return m_Players.ToArray(); } }
        public Random Rng { get; private set; }
        public Size Dimensions { get; set; }
        public Size? ResetDimensions { get; set; } // if set, on reset dimensions will be changed to this
        public bool BombChaining { get; set; } //read from file
        public bool AllWallsPermanent { get; set; } //""

        public bool GameStart { get; private set; }
        public DateTime CountdownTime { get; set; }
        public DateTime GameStarted { get; private set; }
        public int ServerPlayerCount { get { return m_Players.Count; } }

        public event Action BackgroundAssignment;

        private World()
        {
            m_Walls = new List<Wall>();
            m_Bombs = new List<Bomb>();
            m_Players = new List<Player>();
            m_AbortWorker = false;


            Rng = new Random();
            Dimensions = new Size(DEFAULT_GAME_WIDTH, DEFAULT_GAME_HEIGHT);
            GameStart = false;
            CountdownTime = DateTime.MaxValue;
            GameStarted = DateTime.MaxValue;
            ObjectIdCounter = new Counter();

            m_Worker = new Thread(BackgroundWorker);
            m_Worker.Start();
        }

        public void AbortWorker()
        {
            m_AbortWorker = true;
        }

        private void BackgroundWorker()
        {
            while (!m_AbortWorker)
            {
                // both server & client logic
                if ((DateTime.UtcNow - CountdownTime).TotalMilliseconds > 0)
                {
                    GameStart = true;
                    CountdownTime = DateTime.MaxValue;
                    GameStarted = DateTime.UtcNow;
                }

                BackgroundAssignment?.Invoke();

                //if (BackgroundAssignment != null)
                    //BackgroundAssignment();

                Thread.Sleep(5); // 5ms, 1000ms in 1 second
            }
        }

        public void StartGame()
        {
            if (IsServer)
            {
                CountdownTime = DateTime.UtcNow.AddSeconds(BEGIN_GAME_COUNTDOWN);
                foreach(Player p in Players)
                    p.IsDead = false;
                //server tells all players game is starting
                SendToAll(new StartGameMessage(GameStart, CountdownTime));
            }
            else // client side
            {
                GameStart = true;
            }
        }
        public void ResetGame()
        {
            
            GameStart = false;
            if (ResetDimensions != null)
            {
                Dimensions = (Size)ResetDimensions;
                ResetDimensions = null;
            }
            if (IsServer)
            {

                SendToAll(new StartGameMessage(GameStart));
                // Maybe there is still players on the board that are alive,
                // i.e. last man standing (1 player alive)
                foreach (Player p in Players)              //      .Where(player => !player.IsDead))
                    SendToAll(new DespawnMessage(SpawnType.Player, p.Id));

                // Maybe there's remaining bombs on the board that have yet to explode, etc.
                foreach (Bomb b in Bombs)
                    SendToAll(new DespawnMessage(SpawnType.Bomb, b.Id));
                m_Bombs.Clear();

                //clears and generate new walls
                GenerateWalls();

                // "respawn" all the players connected to the game
                // set all connected players to NOT ready
                foreach (var player in Players)
                {
                    player.IsDead = false;
                    player.PlayerReady = false;

                    var dst = GetOpenPoint();
                    player.MoveTo(dst, true);
                }
                foreach (var player in Players)
                    SendWorldInfo(player);

                Console.WriteLine();
                
            }
        }

        public bool IsWall(int x, int y)
        {
            if (m_Walls.Any(w=> w.Location.X == x && w.Location.Y==y))
                return true;
            return false;
        }

        public bool IsWall(Point p) 
        {
            return IsWall(p.X, p.Y);
        }

        public bool IsOpenSpace(int x, int y)
        {
            if (IsWall(new Point(x, y)))
                return false;

            //  looking for ANY player WHICH/WHERE the (x and y match)/(bool is true)
            //                          bool based on specified rules/criteria
            if (Players.Any(player => (player.Location.X == x) && (player.Location.Y == y) && !player.IsDead))
                return false;

            if (Bombs.Any(bomb => bomb.Location.X == x && bomb.Location.Y == y))
                return false;

            return true;
        }

        public bool IsOpenSpace(Point p)
        {
            return IsOpenSpace(p.X, p.Y);
        }

        public Bomb AddBomb(int x, int y, DateTime detonate, int size, int id, int? ownerId)
        {
            Bomb newBomb = new Bomb(id, new Point(x, y));
            newBomb.DetonateTime = detonate;
            newBomb.DetonateSize = size;

            if (ownerId != null)
                newBomb.OwnerId = (int)ownerId;

            m_Bombs.Add(newBomb);
            return newBomb;
        }

        public void SpawnBomb(Player player)
        {
            if (!World.IsServer)
                throw new ServerSideOnlyException();

            //spawn a bomb under/over THAT instance of player
            var newBomb = AddBomb(player.Location.X, player.Location.Y, DateTime.UtcNow.AddSeconds(DEFAULT_BOMB_EXPLODE_SECS), BOMB_EXPLOSION_SIZE, World.State.ObjectIdCounter.Next(), player.Id);
            var msg = new SpawnMessage(SpawnType.Bomb, newBomb.Id, newBomb.Location.X, newBomb.Location.Y)
            {
                DetonateSize = newBomb.DetonateSize,
                DetonateTime = newBomb.DetonateTime,
                OwnerId = newBomb.OwnerId
            };

            SendToAll(msg);
        }


        public void SendToAll(NetworkMessage mes, Func<Player, bool> exclude = null)
        {
            if (!World.IsServer)
                throw new ServerSideOnlyException();

            foreach (Player p in Players)
            {
                if (exclude == null || !exclude(p))
                    p.Send(mes);
            }
        }
       
        public Point GetOpenPoint()
        {
            while (true)
            {
                int y = Rng.Next(1, Dimensions.Height - 1);
                int x = Rng.Next(1, Dimensions.Width - 1);

                if (!IsOpenSpace(x, y))
                    continue;

                Point location = new Point(x, y);
                return location;
            }
        }

        public Player SpawnPlayer(ClientSocket socket)
        {
            if (!World.IsServer)
                throw new ServerSideOnlyException();

            Point location = GetOpenPoint();
            var newPlayer = new Player(socket, ObjectIdCounter.Next(), location);
            AddPlayer(newPlayer);

            // send new player to all the other player connected
            if (GameStart || CountdownTime != DateTime.MaxValue)
            {
                newPlayer.IsDead = true;
            }
            else
            {
                var spawn = new SpawnMessage(SpawnType.Player, newPlayer.Id, location.X, location.Y) { Color = newPlayer.Color };
                SendToAll(spawn, p => p.Id == newPlayer.Id);
            }

            return newPlayer;
        }
        public void AddPlayer(Player p)
        {
            m_Players.Add(p);
        }

        public void AddWall(int id, int x, int y, bool perm)
        {
            Wall newWall = new Wall(id, new Point(x, y), perm);
            m_Walls.Add(newWall);
        }

        public void SendWorldInfo(Player newPlayer) 
        {
            if (!IsServer)
                throw new ServerSideOnlyException();
            //send boardsize
            newPlayer.Send(new BoardSizeMessage(Dimensions.Width, Dimensions.Height));

            // send to the client the present state of the game
            newPlayer.Send(new StartGameMessage(GameStart, CountdownTime));

            // send to client so they know who they are
            newPlayer.Send(new IdentityMessage(newPlayer.Id));

            // send each wall/point for wall
            //      send info based on List cloned to array/[] to prevent crashing due to information/collection change
            foreach (var wallPoint in Walls)
            {
                newPlayer.Send(new SpawnMessage(SpawnType.Wall, wallPoint.Id, wallPoint.Location.X, wallPoint.Location.Y) 
                { IsPermanent = wallPoint.IsPermanent}); ;
            }
            //joining player/client needs knowledge of existing bombs
            //doesnt actually get used anymore, clients cant join mid-game and bombs dont exist before games start
            foreach (Bomb b in Bombs)
            {
                newPlayer.Send(new SpawnMessage(SpawnType.Bomb, b.Id, b.Location.X, b.Location.Y));
            }
            //send each existing play to the new player AND the new player to the existing players
            foreach (Player p in Players.Where(p => !p.IsDead))
            {
                //each player received the newly created player
                // p.Send(new SpawnMessage(SpawnType.Player, newPlayer.Id, newPlayer.Location.X, newPlayer.Location.Y) { Color = newPlayer.Color });
                //if (p.Id != newPlayer.Id)
                    //if p isnt the new player, send the newly created player/client p's info
                    
                newPlayer.Send(new SpawnMessage(SpawnType.Player, p.Id, p.Location.X, p.Location.Y) 
                { Color = p.Color });
            }
        }
        
        public void DespawnPlayer(int playerId)
        {
            if(!IsServer)
                throw new ServerSideOnlyException();
            RemoveObject(SpawnType.Player, playerId);
            SendToAll(new DespawnMessage(SpawnType.Player, playerId));
        }


        public void RemoveObject(SpawnType type, int id)
        {
            if (type == SpawnType.Player)
            {
                int i = m_Players.FindIndex(p => p.Id == id);
                if (i > -1)
                    m_Players.RemoveAt(i);
            }
            else if (type == SpawnType.Bomb)
            {
                int i = m_Bombs.FindIndex(b => b.Id == id);
                if (i > -1)
                    m_Bombs.RemoveAt(i);
            }
            //exists to communicate to clients to wipe m_walls completely
            else if (type == SpawnType.Wall && id == 0)
            {
                m_Walls.Clear();
            }
            //exists to remove the SPECIFIED NONpermanent wall
            else if (type == SpawnType.Wall)
            {
                int i = m_Walls.FindIndex(w => w.Id == id);
                if (i > -1 && !m_Walls[i].IsPermanent)
                    m_Walls.RemoveAt(i);
            }
        }

        public void GenerateWalls()
        {
            if (!World.IsServer)
                throw new ServerSideOnlyException();

            SendToAll(new DespawnMessage(SpawnType.Wall, 0));
            m_Walls.Clear();

            //permenent walls will still have ID ==0?
            // draw the horizontal walls
            //top row//bottom row
            for (int i = 0; i < Dimensions.Width; i++)
            {
                m_Walls.Add(new Wall(0, new Point(i, 0), true));
                m_Walls.Add(new Wall(0, new Point(i, Dimensions.Height - 1), true));
            }

            // draw the vertical walls
            //left and right sidewalls
            for (int i = 0; i < Dimensions.Height; i++)
            {
                m_Walls.Add(new Wall(0, new Point(0, i), true));
                m_Walls.Add(new Wall(0, new Point(Dimensions.Width - 1, i), true));
            }

            for (int y = 1; y < Dimensions.Height - 1; y++)
            {
                for (int x = 1; x < Dimensions.Width - 1; x++)
                {
                    int chance = Rng.Next(100);
                    if (chance > 70) // 30%
                    {
                        bool topLeft = IsWall(new Point(x - 1, y + 1));
                        bool topRight = IsWall(new Point(x + 1, y + 1));
                        bool bottomLeft = IsWall(new Point(x - 1, y - 1));
                        bool bottomRight = IsWall(new Point(x + 1, y + 1));

                        if (x == 1 || y == 1 || x == Dimensions.Width - 1 || y == Dimensions.Height - 1 || (!topLeft && !topRight && !bottomLeft && !bottomRight))
                        {
                            m_Walls.Add(new Wall(World.State.ObjectIdCounter.Next(), new Point(x, y), AllWallsPermanent));
                        }
                    }
                }
            }
        }
    }
}
