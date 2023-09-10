using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bomberman.Network.Messages;

namespace Bomberman.Shared
{
    public class Bomb
    {
        private bool m_IsFirstDetonation;
        private Point[] m_ExplosionPoints;
        public DateTime DetonateTime { get; set; }
        public Point Location { get; private set; }
        public int DetonateSize { get; set; }
        public bool IsIdle { get; private set; }
        public int Id { get; private set; }
        public int OwnerId { get; set; }
        
        

        public Bomb(int id, Point location)
        {
            Id = id;
            Location = location;
            IsIdle = true;
            m_IsFirstDetonation = true;
            m_ExplosionPoints = null;
                //new Point[(((DetonateSize - 1) * 4) + 1)];
        }
        //continously recalled until bomb has exploded for more than .5 seconds
        public void Detonate()
        {

            IsIdle = false;
            //  if(!World.IsServer)
            //       m_CollisionPoint = GetExplosionPoints(World.State.IsWall);

            // create list to store the hitWalls
            // when called client side this will also cache m_ExplosionPoints
            List<Point> wallCollisionPoints = new List<Point>();
            var explosionPoints = GetExplosionPoints(World.State.IsWall, wallCollisionPoints);

            if (World.IsServer)
            {
                World.State.SendToAll(new StatusChangeMessage(SpawnType.Bomb, this.Id));

                //this is the ARRAY of explosion points, msut check inside for players
                //CLONE saved to prevent explosion past a just broken wall (at least for this specific bomb)
                if (m_IsFirstDetonation)
                {
                    //only runs if wallsCANbreak and its the bomb's first detonation
                    if (!World.State.AllWallsPermanent)
                    {
                        //find and damage wall logic, only needed once per bomb
                        foreach (Point p in wallCollisionPoints)
                        {
                            //only hit the 1st wall added at that location which is breakable
                            //add wall method should not skip breakable walls -----IF--- walls are breakable
                            Wall hitByBomb = World.State.Walls.FirstOrDefault(w => !w.IsPermanent && w.Location.X == p.X
                                && w.Location.Y == p.Y);
                            if (hitByBomb == null)
                                continue;
                            hitByBomb.DamageWall();
                        }
                    }
                }

                foreach (var deadPlayer in World.State.Players.Where(player => !player.IsDead && explosionPoints.Contains(player.Location)))
                {
                    deadPlayer.Kill();
                }

                if (World.State.BombChaining)
                {
                    //find a bomb that IS idle AND contained in m_explosionPoints(only stopped by walls)
                    foreach(var bombToChain in World.State.Bombs.Where(b=> b.IsIdle && explosionPoints.Contains(b.Location)))
                    {
                        bombToChain.DetonateTime = this.DetonateTime;
                        bombToChain.IsIdle = false;
                        World.State.SendToAll(new StatusChangeMessage(SpawnType.Bomb,bombToChain.Id));
                    }
                }
            }


            m_IsFirstDetonation = false;
        }


        public Point[] GetExplosionPoints(Func<Point, bool> isCollision, List<Point> collisionPoints = null)
        {
#warning the cache assumes isCollision criteria does not change between subsequent calls but not a problem currently

            if (m_ExplosionPoints == null)
            {
                //rules/definition of collision are defined by the FUNC given at call
                //perhaps what a collision is changes? ???
                //allows for future differnt use ?
                //bombs that ignore walls?

                bool n = true, e = true, s = true, w = true;
                var points = new List<Point>();
                points.Add(Location);
                for (int i = 1; i < DetonateSize; i++)
                {
                    //method currently only uses World...IsWall
                    var north = new Point(Location.X, Location.Y - i);
                    var east = new Point(Location.X + i, Location.Y);
                    var south = new Point(Location.X, Location.Y + i);
                    var west = new Point(Location.X - i, Location.Y);

                    // a?.b()           -- if a is not null then call b
                    if (n && isCollision(north))
                    {
                        n = false;
                        collisionPoints?.Add(north);
                    }
                    if (e && isCollision(east))
                    {
                        e = false;
                        collisionPoints?.Add(east);
                    }
                    if (s && isCollision(south))
                    {
                        s = false;
                        collisionPoints?.Add(south);
                    }
                    if (w && isCollision(west))
                    {
                        w = false;
                        collisionPoints?.Add(west);
                    }
                    /* ABOVE will add two walls north if there are 2 there
                     * how to only save the nearest once... per direction
                     */
                    if (n) points.Add(north);
                    if (e) points.Add(east);
                    if (s) points.Add(south);
                    if (w) points.Add(west);

                    /* should this method call DAMAGEWALL? how to store the collision points 
                     */
                }
                
                m_ExplosionPoints = points.ToArray();
            }

            return m_ExplosionPoints;
        }
    }
}
