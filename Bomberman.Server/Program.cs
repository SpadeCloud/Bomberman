using System;
using System.Text;
using System.IO;
using Bomberman.Network;
using Bomberman.Network.Messages;
using Bomberman.Shared;
using Newtonsoft.Json;
using System.Runtime.CompilerServices;
using Bomberman.Network.Attributes;

namespace Bomberman.Server
{
    public class Program
    {
        private DateTime m_LastWallAddition;
        private Point m_NewWallLocation;
        private Point m_TopLeft;
        private NetworkMessageDispatcher m_ServerMessageDispatcher;
        private Player? m_CurrentPlayer;
        public static void Main(string[] args)
        {
            var p = new Program();
            p.Main();
        }
        
        public Program()
        {
            m_LastWallAddition = DateTime.MinValue;
            m_NewWallLocation = new Point(0, 0);
            m_ServerMessageDispatcher = new NetworkMessageDispatcher();
            m_ServerMessageDispatcher.RegisterMethods(this);
        }

        public void Main()
        {
            Config? config = null;

            // check for an existing config.json file and deserialize it
            if (File.Exists("./config.json"))
            {
                string fileContent = File.ReadAllText("./config.json");

                //doesnt break if file contents are wrong, miss info left null
                try
                {
                    config = JsonConvert.DeserializeObject<Config>(fileContent);
                }
                catch // file content could not be parsed correctly
                {
                    Console.WriteLine("config.json is not correctly formatted and cannot be parsed");
                    
                    while (true)
                    {
                        Console.Write("Restore config.json back to default (y/n): ");
                        string? input = Console.ReadLine();
                        if (input == null)
                            continue;
                        else if (input.StartsWith("y"))
                            break;
                        else if (input.StartsWith("n"))
                            return;
                        else
                            Console.WriteLine("Invalid Input");
                    }
                }
            }

            // if the config.json file doesn't exist, write one so it can be easily edited
            if (config == null)
            {
                config = new Config();
                File.WriteAllText("./config.json", JsonConvert.SerializeObject(config, Formatting.Indented));
            }

            World.IsServer = true;
            World.State.BackgroundAssignment += BackgroundWork;

            if (config.BombChaining != null)
                World.State.BombChaining = (bool)config.BombChaining;
            if (config.AllWallsPermanent != null)
                World.State.AllWallsPermanent = (bool)config.AllWallsPermanent;
                //World.State.ResetDimensions = new Size
                //    ((config.BoardWidth == null)? World.State.Dimensions.Width : (int)config.BoardWidth, 
                //    (config.BoardHeight ==null)? World.State.Dimensions.Height: (int)config.BoardHeight);

            Size dimensions = new Size((int)(config.BoardWidth ?? World.State.Dimensions.Width), (int)(config.BoardHeight ?? World.State.Dimensions.Height));
            if (dimensions.Width < World.MIN_GAME_WIDTH || dimensions.Height < World.MIN_GAME_HEIGHT)
            {
                Console.WriteLine("Board size {0}x{1} is too small, minimum size is {2}x{3}", dimensions.Width, dimensions.Height, World.MIN_GAME_WIDTH, World.MIN_GAME_HEIGHT);
                World.State.AbortWorker();
                return;
            }
            World.State.ResetDimensions = dimensions;

            World.State.ResetGame();


            var ss = new ServerSocket(9337);
            ss.ClientConnected += Ss_ClientConnected;
            ss.ClientMessage += Ss_ClientMessage;
            ss.ClientDisconnected += Ss_ClientDisconnected;
            ss.Start();

            Console.WriteLine($"Server has started and is on port {9337}");

            while(true)
            {
                string? input = Console.ReadLine();
                if (input == null) continue;
                
                string[] inputSplit;


                if (input.StartsWith("b"))
                {
                    inputSplit = input.Split(' ');
                    if (inputSplit.Length < 3)
                    {
                        Console.WriteLine("Expected 2 parameters only got {0}", inputSplit.Length-1);
                        continue;
                    }

                    int newWidth;
                    int newHeight;
                    if (!int.TryParse(inputSplit[1], out newWidth) || !int.TryParse(inputSplit[2], out newHeight))
                    {
                        Console.WriteLine("Invalid width or height parameter");
                        continue;
                    }

                    if (newWidth <= World.MIN_GAME_WIDTH || newHeight <= World.MIN_GAME_HEIGHT)
                    {
                        Console.WriteLine("Width or height is below the minimum requirements of {0}x{1}", World.MIN_GAME_WIDTH, World.MIN_GAME_HEIGHT);
                        continue;
                    }
                    
                    World.State.ResetDimensions = new Size(newWidth, newHeight);
                    
                }

            }
        }

        private void CheckBombs()
        {
            foreach (var b in World.State.Bombs)
            {
                if (b.IsIdle)
                {
                    //if explode time is now or past
                    if (DateTime.UtcNow.Subtract(b.DetonateTime).TotalSeconds >= 0)
                    {
                        b.Detonate(); 
                    }
                }
                else //not idle
                {
                    b.Detonate(); // continue to re-detonate


                    //if explode time is at least 1 second past
                    //explosion stays on screen for this amount of time
                    if (DateTime.UtcNow.Subtract(b.DetonateTime).TotalMilliseconds >= 500)
                    {
                        World.State.RemoveObject(SpawnType.Bomb, b.Id);
                        World.State.SendToAll(new DespawnMessage(SpawnType.Bomb, b.Id));
                    }
                }
            }
        }
        private void CheckGameOver()
        {
            if (World.State.GameStart && World.State.Players.Count(player => !player.IsDead) < 2)
            {
                World.State.ResetGame();
                Console.WriteLine("game has ended");
            }
        }

        private void ShrinkMap()
        {
            if (World.State.GameStart == false || World.State.GameStarted == DateTime.MaxValue)
            {
                m_NewWallLocation = new Point (1,1);
                m_TopLeft = new Point (1,1);
                return;
            }

            //
            //first time this goes through, m_lastwalladdition will be minimum
            //every following time, the time past must be more then a full 2 seconds
            if((DateTime.UtcNow > World.State.GameStarted.AddSeconds(World.TIME_UNTIL_MAP_SHRINKS))
                && (DateTime.UtcNow-m_LastWallAddition).TotalSeconds > World.TIME_BETWEEN_WALL_ADDITION)
            {
                //add-or-change-wall
                Wall? aocWall = World.State.Walls.FirstOrDefault(w => w.Location.X == m_NewWallLocation.X && w.Location.Y == m_NewWallLocation.Y);
                Bomb? crushedBomb = World.State.Bombs.FirstOrDefault(b=> b.Location.X== m_NewWallLocation.X && b.Location.Y == m_NewWallLocation.Y);
                
                //if bomb is at new-wall-location, despawn it
                if(crushedBomb!= null) { World.State.SendToAll(new DespawnMessage(SpawnType.Bomb, crushedBomb.Id)); }
                
                //if theres NO wall at coordinate...
                if (aocWall ==null)
                {
                    World.State.AddWall(0, m_NewWallLocation.X, m_NewWallLocation.Y, true);
                    World.State.SendToAll(new SpawnMessage(SpawnType.Wall, 0, m_NewWallLocation.X, m_NewWallLocation.Y)
                    { IsPermanent = true }); ;
                    m_LastWallAddition = DateTime.UtcNow;
                }
                //THERE IS A WALL AND walls CAN break
                else if(!World.State.AllWallsPermanent)
                {
                    aocWall.ShrinkChangeWall();
                    m_LastWallAddition = DateTime.UtcNow;
                }
                if(!GetNextShrinkPoint(ref m_TopLeft, ref m_NewWallLocation))
                    m_LastWallAddition=DateTime.MaxValue;
            }
        }
        private bool GetNextShrinkPoint(ref Point topLeft, ref Point old)
        {
            Point next;

            if (old.X == topLeft.X && old.Y > topLeft.Y) // moving bottom to top
            {
                next = new Point(old.X, old.Y - 1);
                if (next.X == topLeft.X && next.Y == topLeft.Y) // have completed round trip
                {
                    next = new Point(next.X + 1, next.Y + 1); // move bottom right diagonal
                    topLeft = next;
                }
            }
            else if (old.X == World.State.Dimensions.Width - 1 - topLeft.X)
            {
                if (old.Y == World.State.Dimensions.Height - 1 - topLeft.Y) // moving right to left
                    next = new Point(old.X - 1, old.Y);
                else // // moving top to bottom
                    next = new Point(old.X, old.Y + 1);
            }
            else if (old.Y == topLeft.Y) // moving left to right
            {
                next = new Point(old.X + 1, old.Y);
            }
            else if (old.Y == World.State.Dimensions.Height - 1 - topLeft.Y) // moving right to left
            {
                next = new Point(old.X - 1, old.Y);
            }
            else
                throw new InvalidOperationException();

            if (next.X < 0 || next.Y < 0 || next.X >= World.State.Dimensions.Width || next.Y >= World.State.Dimensions.Height)
                return false;
            else
                old = next;
            return true;
        }

        public void CheckCrushedPlayer()
        {
            foreach (Player p in World.State.Players)
            {
                if (p.IsDead)
                    continue;
                if (World.State.Walls.Any(w => w.Location.X == p.Location.X && w.Location.Y == p.Location.Y))
                {
                    p.Kill();
                }
            }
        }
        private void ServerStartsGame()
        {
            if (World.State.CountdownTime != DateTime.MaxValue || World.State.GameStart)
                return;
            // ANY will return TRUE if a single player is NOT READY
            // will return FALSE if ALL PLAYERs ARE ready 
            if (World.State.Players.Length > 1 && (!World.State.Players.Any(p => p.PlayerReady == false)))
            {
                //server starts game automatically when all connected players are ready
                World.State.StartGame();
            }
            
        }
        private void BackgroundWork()
        {
            ServerStartsGame();
            CheckBombs();
            ShrinkMap();

            CheckCrushedPlayer();
            CheckGameOver();
        }
        [NetworkMessage(MessageType.Location)]
        public void HandleLocationMessage(byte[] msg)
        {
            if(m_CurrentPlayer== null)
                return;
#warning
            //all messages require the player, should be stored
            //but where is the problem
            // maybe a private int currentPlayer here/this?
            LocationMessage message = new LocationMessage(msg);
            if (message.PlayerId != m_CurrentPlayer.Id) // cannot move another player
                return;
            if ((DateTime.UtcNow - m_CurrentPlayer.ProcessMovement).TotalMilliseconds < 100)
                return;
            if (m_CurrentPlayer.MoveTo(new Point(message.X, message.Y)))
            {
                m_CurrentPlayer.ProcessMovement = DateTime.UtcNow;
                World.State.SendToAll(message);
            }
        }
        [NetworkMessage(MessageType.Spawn)]
        public void HandleSpawnMessage(byte[] msg)
        {
            if (m_CurrentPlayer == null)
                return;
            //client/player requests a bomb spawn
            //bomb spawns server side
            //server sends info back
            // ALL client accepts and adds to client's world.state.bombs
            SpawnMessage message = new SpawnMessage(msg);
            if (message.Type == SpawnType.Bomb && !m_CurrentPlayer.IsDead)
            {
                World.State.SpawnBomb(m_CurrentPlayer);
            }
        }
        [NetworkMessage(MessageType.StartGame)]
        public void HandleStartGameMessage(byte[] msg)
        {
            if (m_CurrentPlayer == null)
                return;
            StartGameMessage message = new StartGameMessage(msg);
            m_CurrentPlayer.PlayerReady = true;
            Console.WriteLine($"player {m_CurrentPlayer.Id} is ready");

            /*perhaps, when the SERVER is told CLIENT is ready
             * server should inform all connected clients that this-specific-player is ready?
             */
            foreach (Player p in World.State.Players)
            {
                p.Send(new StatusChangeMessage(SpawnType.Player, m_CurrentPlayer.Id, true, World.State.Players.Count()));
            }
        }
       

        private void Ss_ClientMessage(ClientSocket cs, byte[] data)
        {
            m_CurrentPlayer = (Player)cs.State;
            m_ServerMessageDispatcher.Dispatch(data);
            m_CurrentPlayer= null;
        }
        private void Ss_ClientDisconnected(ClientSocket obj)
        {
            Player player = (Player)obj.State;
            World.State.DespawnPlayer(player.Id);
            World.State.SendToAll(new DespawnMessage(SpawnType.Player, player.Id));
        }

        private void Ss_ClientConnected(ClientSocket cs)
        {
            var player = World.State.SpawnPlayer(cs);
            cs.State = player;
            World.State.SendWorldInfo(player);
            Console.WriteLine($"New player has connected, assigned player id {player.Id}");
        }
    }
}