using Bomberman.Network;
using Bomberman.Network.Attributes;
using Bomberman.Network.Messages;
using Bomberman.Shared;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DrawingPoint = System.Drawing.Point;
using GamePoint = Bomberman.Shared.Point;

namespace Bomberman
{
    public partial class GameForm : Form
    {
        private static Random Rng;

        static GameForm()
        {
            Rng = new Random();
        }

        // for now have our board always be 16x16 cells
        private const int CELL_SIZE = 32;

        private ClientSocket m_Client;
        private int m_PlayerId;
        private NetworkMessageDispatcher m_MessageDispatcher;

        // ASSETS
        private Bitmap m_BackgroundImage;
        private Bitmap m_WallImage;
        private Bitmap m_BombImage;
        private Bitmap m_ExplodeCenter;
        private Bitmap m_ExplodeNorth;
        private Bitmap m_ExplodeNorthEdge;
        private Bitmap m_ExplodeSouth;
        private Bitmap m_ExplodeSouthEdge;
        private Bitmap m_ExplodeEast;
        private Bitmap m_ExplodeEastEdge;
        private Bitmap m_ExplodeWest;
        private Bitmap m_ExplodeWestEdge;
        private Bitmap m_PlayerImageBase;
        private Dictionary<int, Bitmap> m_PlayerImages;
        private Dictionary<Color, Bitmap> m_WallImages;

        public GameForm()
        {
            InitializeComponent();
            m_MessageDispatcher = new NetworkMessageDispatcher();
            m_MessageDispatcher.RegisterMethods(this);
        }

        private DrawingPoint ToCellPoint(int x, int y)
        {
            return ToCellPoint(new GamePoint(x, y));
        }
        private DrawingPoint ToCellPoint(GamePoint p)
        {
            return new DrawingPoint(p.X * CELL_SIZE, p.Y * CELL_SIZE);
        }


        //creating "visual representation of the board"
        // not the information/board itself??
        //
        //      CLIENT SIDE
        //
        //cannot call it an interface since that is its own class/type?
        private void RenderBoard()
        {

            var old = pictureBox1.Image;

            var gameImage = new Bitmap(CELL_SIZE * World.State.Dimensions.Width, CELL_SIZE * World.State.Dimensions.Height);
            using (var graphics = Graphics.FromImage(gameImage))
            {
                // draw the background
                for (int j = 0; j < (World.State.Dimensions.Height / 2); j++)
                {
                    for (int i = 0; i < (World.State.Dimensions.Width / 2); i++)
                        graphics.DrawImage(m_BackgroundImage, new DrawingPoint(i * m_BackgroundImage.Width, j * m_BackgroundImage.Height));
                }
                // draw the walls
                foreach (Wall w in World.State.Walls)
                {
                    Bitmap wallImage;

                    if (w.IsPermanent)
                        wallImage = m_WallImages[Color.Maroon];
                    else
                    {
                        double durabilityFrac = (double)w.Durability / w.MaxDurability;
                        if (durabilityFrac > 0.5)
                            wallImage = m_WallImages[Color.Black];
                        else
                            wallImage = m_WallImages[Color.White];
                    }

                    graphics.DrawImage(wallImage, ToCellPoint(w.Location));

                }
                // draw bombs
                foreach (Bomb b in World.State.Bombs)
                {
                    if (b.IsIdle)
                        graphics.DrawImage(m_BombImage, ToCellPoint(b.Location));
                    else
                    {

                        //takes a POINT returns a BOOL based on if the POINT is a wall or not
                        //using lambda(unnamed unassigned FUNC) avoids making a one use method
                        //point[] storing/holding only points not colliding with wall
                        // "=>" actually means nothing
                        // (parameters) => (what it actually does/searches) 
                        var points = b.GetExplosionPoints(World.State.IsWall);

                        foreach (var p in points)
                        {
                            // c = (3,3)
                            // p = (3,5)
                            // size - 1 = 2

                            // xDiff = p.X - c.X = abs(0) = 0
                            // yDiff = p.Y - c.Y = abs(2) = 2
                            //takes largest # from (differences of X value) and (differences of Y value)
                            //... if the difference = max range of bomb
                            //it must be an edge?
                            int magnitude = (Math.Max(Math.Abs(p.X - b.Location.X), Math.Abs(p.Y - b.Location.Y)));
                            bool isEdge = magnitude == (b.DetonateSize - 1);

                            if (p.X == b.Location.X && p.Y == b.Location.Y)
                                graphics.DrawImage(m_ExplodeCenter, ToCellPoint(p));
                            else if (p.Y < b.Location.Y)
                                graphics.DrawImage(isEdge ? m_ExplodeNorthEdge : m_ExplodeNorth, ToCellPoint(p));
                            else if (p.Y > b.Location.Y)
                                graphics.DrawImage(isEdge ? m_ExplodeSouthEdge : m_ExplodeSouth, ToCellPoint(p));
                            else if (p.X > b.Location.X)
                                graphics.DrawImage(isEdge ? m_ExplodeEastEdge : m_ExplodeEast, ToCellPoint(p));
                            else if (p.X < b.Location.X)
                                graphics.DrawImage(isEdge ? m_ExplodeWestEdge : m_ExplodeWest, ToCellPoint(p));
                                        //checks if ISEDGE is TRUE, edgepic if true, nonedgepic if not 
                        }
                    }
                }

                if (!World.State.GameStart)
                {
                    //game has not started AND starttime is maxed
                    if (World.State.CountdownTime == DateTime.MaxValue)
                        DrawCenterText(graphics, "Waiting...");
                    else if (World.State.CountdownTime > DateTime.UtcNow)
                    {
                        // game has not started, but starttime is NOT max
                        int timeLeft = (int)(World.State.CountdownTime - DateTime.UtcNow).TotalSeconds;
                        DrawCenterText(graphics, timeLeft.ToString());
                    }
                    //PRINT # of ready / # total players
                    //should only print at waiting screen

                    //set up 
                    int totalReady = World.State.Players.Count(p => p.PlayerReady == true);
                    int totalPlayers = World.State.ServerPlayerCount;
                    
                    DrawBottomLeftText(graphics, $"Players ready: {totalReady}/{totalPlayers}");
                }
                else if (!World.State.Players.Any(p => p.Id == m_PlayerId)) //players do not delete out of m_players so need matching ID and confirm its dead 
                    DrawCenterText(graphics, "you have died..");
                    

                //logic for the waiting screen
                // draw the player ONLY if they are alive/PLAYERS has a p.id == m_playerId
                foreach (Player p in World.State.Players.Where(p => !p.IsDead))
                {
                    // if the game hasn't started yet
                    // and the player is not us
                    // do not draw this player
                    if (!World.State.GameStart && p.Id != m_PlayerId)
                        continue;
                    Bitmap image;
                    if (!m_PlayerImages.TryGetValue(p.Id, out image))
                    {
                        image = RecolorBitmap(m_PlayerImageBase, Color.Blue, Color.FromArgb(p.Color), 0.6, 0.7);
                        m_PlayerImages.Add(p.Id, image);
                    }
                    graphics.DrawImage(image, ToCellPoint(p.Location));
                }
               
            }
            pictureBox1.Image = gameImage; 

            if (old != null)
                old.Dispose();

        }
        public void DrawText(Graphics graphics,
            string textToDraw, 
            bool translucent, 
            int? textSize = 16, Font font = null, 
            Color? clr = null, 
            StringFormat sf = null,
            DrawingPoint? topLeft = null)
        {
            textSize = textSize ?? 16;
            clr = clr ?? Color.Black;
            if (textSize < 0)
                textSize = textSize * -1;
            font = font ?? new Font("Arial", (int)textSize);
            sf = sf ?? new StringFormat();
            topLeft = topLeft ?? new DrawingPoint(0, 0);

            //seems to be a new rectangle just to communicate where text starts
            //does not neccesarily communicate the translucent area
            Rectangle layoutRectangle = new Rectangle(topLeft.Value.X, topLeft.Value.Y, pictureBox1.Width - topLeft.Value.X, pictureBox1.Height - topLeft.Value.Y);

            if(translucent)
            {
                SizeF measurement = graphics.MeasureString(textToDraw, font, this.Width, sf);
                RecolorTransparentRectangle(graphics, 
                    new Shared.Point(topLeft.Value.X, topLeft.Value.Y), 
                    new Shared.Point(topLeft.Value.X + (int)measurement.Width, topLeft.Value.Y + (int)measurement.Height));
            }

            graphics.DrawString(textToDraw, font, new SolidBrush((Color)clr), layoutRectangle, sf);

        }
        //general drawTEXT does not specify newAlpha, seems to already have too much potentially null
        private void RecolorTransparentRectangle(Graphics graphics,
            Shared.Point topLeft, Shared.Point bottomRight, 
            int newAlpha=127)
        {
            var width = bottomRight.X - topLeft.X;
            var height = bottomRight.Y - topLeft.Y;
            using (var brush = new SolidBrush(Color.FromArgb(newAlpha, Color.White)))
            {
                // https://stackoverflow.com/questions/7580145/draw-a-fill-rectangle-with-low-opacity
                graphics.FillRectangle(brush, topLeft.X, topLeft.Y, width, height);

                //make trnaslucent part
                /*for (int y = topLeft.Y; y < bottomRight.Y; y++)
                {
                    for (int x = topLeft.X; x < bottomRight.X; x++)
                    {
                        var p = gameImage.GetPixel(x, y);
                        var change = Color.FromArgb(newAlpha, p);
                        gameImage.SetPixel(x, y, change);
                    }
                }*/
            }
        }
        public void DrawCenterText(Graphics graphics, string textToDraw, int textSize = 16, Color? color = null)
        {
            var sf = new StringFormat();
            sf.LineAlignment = StringAlignment.Center;
            sf.Alignment = StringAlignment.Center;

            var font = new Font("Artial", textSize);

            graphics.DrawString(textToDraw, font, new SolidBrush((color == null)?Color.Black:(Color)color), pictureBox1.ClientRectangle, sf);
        }
        public void DrawBottomLeftText(Graphics graphics, string textToDraw, int textSize = 16, Color? color = null)
        {
            var sf = new StringFormat();
            sf.LineAlignment = StringAlignment.Far;
            sf.Alignment = StringAlignment.Near;

            var font = new Font("Artial", textSize);
            var measure = graphics.MeasureString(textToDraw, font, this.Width, sf);

            //to use with translucent area 
            var textWidth = (int)measure.Width;
            var textHeight = (int)measure.Height;
            var topleft = new Shared.Point(0, pictureBox1.Height - textHeight - 1);
            var bottomright = new Shared.Point(textWidth + 1, pictureBox1.Height);

            DrawText(graphics, textToDraw, true, null, null, null, null, new DrawingPoint(topleft.X, topleft.Y));
        }

        // https://stackoverflow.com/questions/3968179/compare-rgb-colors-in-c-sharp
        public static double CompareColors(Color a, Color b)
        {
            return
                1.0 - ((double)(
                    Math.Abs(a.R - b.R) +
                    Math.Abs(a.G - b.G) +
                    Math.Abs(a.B - b.B)
                ) / (256.0 * 3));
        }

        // https://stackoverflow.com/questions/4106363/converting-rgb-to-hsb-colors
        public static Color FromAhsb(int alpha, float hue, float saturation, float brightness)
        {
            if (0 > alpha
                || 255 < alpha)
            {
                throw new ArgumentOutOfRangeException(
                    "alpha",
                    alpha,
                    "Value must be within a range of 0 - 255.");
            }

            if (0f > hue
                || 360f < hue)
            {
                throw new ArgumentOutOfRangeException(
                    "hue",
                    hue,
                    "Value must be within a range of 0 - 360.");
            }

            if (0f > saturation
                || 1f < saturation)
            {
                throw new ArgumentOutOfRangeException(
                    "saturation",
                    saturation,
                    "Value must be within a range of 0 - 1.");
            }

            if (0f > brightness
                || 1f < brightness)
            {
                throw new ArgumentOutOfRangeException(
                    "brightness",
                    brightness,
                    "Value must be within a range of 0 - 1.");
            }

            if (0 == saturation)
            {
                return Color.FromArgb(
                                    alpha,
                                    Convert.ToInt32(brightness * 255),
                                    Convert.ToInt32(brightness * 255),
                                    Convert.ToInt32(brightness * 255));
            }

            float fMax, fMid, fMin;
            int iSextant, iMax, iMid, iMin;

            if (0.5 < brightness)
            {
                fMax = brightness - (brightness * saturation) + saturation;
                fMin = brightness + (brightness * saturation) - saturation;
            }
            else
            {
                fMax = brightness + (brightness * saturation);
                fMin = brightness - (brightness * saturation);
            }

            iSextant = (int)Math.Floor(hue / 60f);
            if (300f <= hue)
            {
                hue -= 360f;
            }

            hue /= 60f;
            hue -= 2f * (float)Math.Floor(((iSextant + 1f) % 6f) / 2f);
            if (0 == iSextant % 2)
            {
                fMid = (hue * (fMax - fMin)) + fMin;
            }
            else
            {
                fMid = fMin - (hue * (fMax - fMin));
            }

            iMax = Convert.ToInt32(fMax * 255);
            iMid = Convert.ToInt32(fMid * 255);
            iMin = Convert.ToInt32(fMin * 255);

            switch (iSextant)
            {
                case 1:
                    return Color.FromArgb(alpha, iMid, iMax, iMin);
                case 2:
                    return Color.FromArgb(alpha, iMin, iMax, iMid);
                case 3:
                    return Color.FromArgb(alpha, iMin, iMid, iMax);
                case 4:
                    return Color.FromArgb(alpha, iMid, iMin, iMax);
                case 5:
                    return Color.FromArgb(alpha, iMax, iMin, iMid);
                default:
                    return Color.FromArgb(alpha, iMax, iMid, iMin);
            }
        }

        private Bitmap RecolorBitmap(Bitmap bitmap, Color originalColor, Color newColor, double lowerCompare, double upperCompare)
        {
            var clone = new Bitmap(bitmap);
            var hue = newColor.GetHue();
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    var c = bitmap.GetPixel(x, y);
                    var compare = CompareColors(c, originalColor);
                    //if (compare >= 0.6 && compare <= 0.7)
                    if (compare >= lowerCompare && compare <= upperCompare)
                    {
                        var brightness = Math.Min(0.50f, Math.Max(newColor.GetBrightness(), 0.33f));
                        var change = FromAhsb(255, newColor.GetHue(), newColor.GetSaturation(), brightness);
                        clone.SetPixel(x, y, change);
                    }
                    else
                    {
                        clone.SetPixel(x, y, c);
                    }
                }
            }

            return clone;
        }
        private void GameForm_Load(object sender, EventArgs e)
        {
            //button1.BringToFront();
            m_Client = new ClientSocket();
            m_Client.Connect("127.0.0.1", 9337); // TO-DO: add to config file
            m_Client.Message += M_Client_Message;

            m_BackgroundImage = new Bitmap("./GameAssets/background.png");
            m_WallImage = new Bitmap("./GameAssets/wall.png");
            m_BombImage = new Bitmap("./GameAssets/bomb.png");
            m_ExplodeCenter = new Bitmap("./GameAssets/explodecenter.png");
            m_ExplodeNorth = new Bitmap("./GameAssets/explodenorth.png");
            m_ExplodeNorthEdge = new Bitmap("./GameAssets/explodenorthedge.png");
            m_ExplodeSouth = new Bitmap("./GameAssets/explodesouth.png");
            m_ExplodeSouthEdge = new Bitmap("./GameAssets/explodesouthedge.png");
            m_ExplodeEast = new Bitmap("./GameAssets/explodeeast.png");
            m_ExplodeEastEdge = new Bitmap("./GameAssets/explodeeastedge.png");
            m_ExplodeWest = new Bitmap("./GameAssets/explodewest.png");
            m_ExplodeWestEdge = new Bitmap("./GameAssets/explodewestedge.png");
            m_PlayerImageBase = new Bitmap("./GameAssets/player.png");
            m_PlayerImages = new Dictionary<int, Bitmap>();
            m_WallImages = new Dictionary<Color, Bitmap>();
            m_WallImages[Color.Maroon] = RecolorBitmap(m_WallImage, Color.FromArgb(0x323232), Color.Maroon, 0.7, 1.0);
            m_WallImages[Color.Black] = m_WallImage;
            m_WallImages[Color.White] = RecolorBitmap(m_WallImage, Color.FromArgb(0x323232), Color.White, 0.7, 1.0);

            
        }
        /// 
        /// 
        /// 
        ///
        [NetworkMessageAttribute(MessageType.Despawn)]
        public void HandleDespawnMessage(byte[] msg)
        {
            var despawningObj = new DespawnMessage(msg);
            World.State.RemoveObject(despawningObj.Type, despawningObj.Id);
        }

        [NetworkMessage(MessageType.Identity)]
        public void HandleIdentityMessage(byte[] msg)
        {
            var identity = new IdentityMessage(msg);
            m_PlayerId = identity.Id;

            Action setText = () =>
            {
                this.Text = $"Bomberman - Player Id: {m_PlayerId}";
            };

            this.Invoke(setText);
        }

        [NetworkMessage(MessageType.Spawn)]
        public void HandleSpawnMessage(byte[] msg)
        {
            var spawn = new SpawnMessage(msg);
            if (spawn.Type == SpawnType.Player)
            {
                var p = new Player(m_Client, spawn.Id, new GamePoint(spawn.X, spawn.Y), spawn.Color);
                World.State.AddPlayer(p);
            }
            else if (spawn.Type == SpawnType.Wall)
            {
                World.State.AddWall(spawn.Id, spawn.X, spawn.Y, spawn.IsPermanent);
            }
            else if (spawn.Type == SpawnType.Bomb)
            {
                World.State.AddBomb(spawn.X, spawn.Y, spawn.DetonateTime, spawn.DetonateSize, spawn.Id, spawn.OwnerId);
            }
        }

        [NetworkMessage(MessageType.Location)]
        public void HandleLoctionMessage(byte[] msg)
        {
            LocationMessage updPlayer = new LocationMessage(msg);
            var player = World.State.Players.FirstOrDefault(p => p.Id == updPlayer.PlayerId);

            if (player != null)
                player.Location = new GamePoint(updPlayer.X, updPlayer.Y);
        }

        [NetworkMessage(MessageType.StatusChange)]
        public void HandleStatusChange(byte[] msg)
        {
            StatusChangeMessage updateInfo = new StatusChangeMessage(msg);
            if (updateInfo.Type == SpawnType.Bomb)
            {
                Bomb b = World.State.Bombs.FirstOrDefault(bomb => bomb.Id == updateInfo.Id);
                if (b != null)
                    b.Detonate();
            }
            else if (updateInfo.Type == SpawnType.Wall)
            {

                Wall w = World.State.Walls.FirstOrDefault(wall => wall.Id == updateInfo.Id);
                if (w != null)
                {
                    if (!updateInfo.StatusChange)
                        w.DamageWall();
                    else
                        w.ShrinkChangeWall();
                }
            }
            else if (updateInfo.Type == SpawnType.Player)
            {
                Player p = World.State.Players.FirstOrDefault(player => player.Id == updateInfo.Id);
                if (p != null)
                {
                    p.PlayerReady = updateInfo.StatusChange;
                    //World.State.ServerPlayerCount = updateInfo.Count;
                }
            }
        }

        [NetworkMessage(MessageType.StartGame)]
        public void HandleStartGameMessage(byte[] msg)
        {
            var startMessage = new StartGameMessage(msg);
            if (startMessage.StartGame)
            {
                World.State.StartGame();
            }
            else
            {
                if (startMessage.StartTime != DateTime.MaxValue)
                    //"true", game started  (which only set by background worker)
                    World.State.CountdownTime = startMessage.StartTime;
                else
                {
                    World.State.ResetGame();
                    Action setButtonVisibility = () =>
                    {
                        button1.Visible = true;
                    };
                    this.Invoke(setButtonVisibility);
                }
            }
        }

        [NetworkMessage(MessageType.BoardSize)]
        public void HandleBoardSizeMessage(byte[] msg)
        {
            var boardSizeMsg = new BoardSizeMessage(msg);
            World.State.Dimensions = new Shared.Size(boardSizeMsg.BoardWidth, boardSizeMsg.BoardHeight);

            Action setFormSize = () =>
            {
                int heightPadding = this.Height - this.ClientRectangle.Height;
                int widthPadding = this.Width - this.ClientRectangle.Width;

                this.Width = CELL_SIZE * World.State.Dimensions.Width + widthPadding;
                this.Height = CELL_SIZE * World.State.Dimensions.Height + heightPadding;
            };
            this.Invoke(setFormSize);
        }
 
        
        private void M_Client_Message(byte[] message)//ProcessInputMenu
        {   
            //1. client tries to call the correct method (look NetworkMessageDispactcher class)
            m_MessageDispatcher.Dispatch(message);

            // ...
        }

        private void button1_Click(object sender, EventArgs e)
        {
            //MessageBox.Show(this.Width + " " + this.Height);

            var player = World.State.Players.FirstOrDefault(p=> p.Id == m_PlayerId);
            if (player == null)
                return;
            player.ReadyUp();
            button1.Visible = false;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (World.State.GameStart)
            { 
                
                var player = World.State.Players.FirstOrDefault(p => p.Id == m_PlayerId);
                if (player != null)
                {
                    if (keyData == Keys.Left)
                    {
                        player.MoveTo(new GamePoint(player.Location.X - 1, player.Location.Y));
                        return true;
                    }
                    else if (keyData == Keys.Right)
                    {
                        player.MoveTo(new GamePoint(player.Location.X + 1, player.Location.Y));
                        return true;
                    }
                    else if (keyData == Keys.Up)
                    {
                        player.MoveTo(new GamePoint(player.Location.X, player.Location.Y - 1));
                        return true;
                    }
                    else if (keyData == Keys.Down)
                    {
                        player.MoveTo(new GamePoint(player.Location.X, player.Location.Y + 1));
                        return true;
                    }
                    else if (keyData == Keys.Space)
                    {
                        player.DropBomb();
                        return true;
                    }
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            RenderBoard();
        }

        private void GameForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            World.State.AbortWorker();
        }
    }
}
