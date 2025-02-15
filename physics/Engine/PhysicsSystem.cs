using System;
using System.Collections.Generic;
using System.Drawing;
using physics.Engine.Classes;
using physics.Engine.Helpers;
using physics.Engine.Structs;

namespace physics.Engine
{
    public class PhysicsSystem
    {
        #region Public Properties

        public float GravityScale = 20F;

        public Vec2 Gravity { get; set; }

        public float Friction { get; set; }

        // Set this to roughly your average AABB size – adjust as needed.
        public float SpatialHashCellSize { get; set; } = 20F;

        #endregion

        #region Local Declarations


        public const float FPS = 60;
        private const float _dt = 1 / FPS;
        private const int PHYSICS_ITERATIONS = 8;
        private double accumulator = 0;


        public static PhysicsObject ActiveObject;

        public static readonly List<aShader> ListShaders = new List<aShader>();

        public static readonly List<CollisionPair> ListCollisionPairs = new List<CollisionPair>();

        internal IEnumerable<PhysicsObject> GetMoveableObjects()
        {
            for(int i = ListStaticObjects.Count-1; i >= 0; i--)
            {
                var obj = ListStaticObjects[i];
                if (! obj.Locked && obj.Mass < 1000000)
                {
                    yield return obj;
                }
            }
        }

        public static readonly List<PhysicsObject> ListGravityObjects = new List<PhysicsObject>();

        public static readonly List<PhysicsObject> ListStaticObjects = new List<PhysicsObject>();

        internal void SetVelocity(PhysicsObject physicsObject, Vec2 velocity)
        {
            physicsObject.Velocity = velocity;
        }

        public static readonly Queue<PhysicsObject> RemovalQueue = new Queue<PhysicsObject>();

        #endregion

        #region Constructors

        public PhysicsSystem()
        {
            Gravity = new Vec2 {X = 0, Y = 10F * GravityScale};
            Friction = 1F;
        }

        #endregion

        #region Public Methods


        public static PhysicsObject CreateStaticCircle(Vec2 loc, int radius, float restitution, bool locked, aShader shader)
        {
            var oAabb = new AABB
            {
                Min = new Vec2 { X = loc.X - radius, Y = loc.Y - radius },
                Max = new Vec2 { X = loc.X + radius, Y = loc.Y + radius }
            };
            PhysMath.CorrectBoundingBox(ref oAabb);
            var obj = new PhysicsObject(oAabb, PhysicsObject.Type.Circle, restitution, locked, shader);
            ListStaticObjects.Add(obj);
            return obj;
        }

        public static PhysicsObject CreateStaticBox(Vec2 start, Vec2 end, bool locked, aShader shader, float mass)
        {
            var oAabb = new AABB
            {
                Min = new Vec2 { X = start.X, Y = start.Y },
                Max = new Vec2 { X = end.X, Y = end.Y }
            };
            PhysMath.CorrectBoundingBox(ref oAabb);
            var obj = new PhysicsObject(oAabb, PhysicsObject.Type.Box, .95F, locked, shader, mass);
            ListStaticObjects.Add(obj);
            return obj;
        }

        public bool ActivateAtPoint(PointF p)
        {
            ActiveObject = CheckObjectAtPoint(p);

            if (ActiveObject == null)
            {
                ActiveObject = null;
                return false;
            }

            return true;
        }

        public void AddVelocityToActive(Vec2 velocityDelta)
        {
            if (ActiveObject == null || ActiveObject.Mass >= 1000000)
            {
                return;
            }

            ActiveObject.Velocity += velocityDelta;
        }
        public void SetVelocityOfActive(Vec2 velocityDelta)
        {
            if (ActiveObject == null || ActiveObject.Mass >= 1000000)
            {
                return;
            }

            ActiveObject.Velocity = velocityDelta;
        }

        public void FreezeStaticObjects()
        {
            foreach (var physicsObject in ListStaticObjects)
            {
                physicsObject.Velocity = new Vec2 { X = 0, Y = 0 };
            }
        }

        public PointF GetActiveObjectCenter()
        {
            if (ActiveObject == null)
            {
                return new PointF();
            }

            return new PointF(ActiveObject.Center.X, ActiveObject.Center.Y);
        }

        public void MoveActiveTowardsPoint(Vec2 point)
        {
            if (ActiveObject == null)
            {
                return;
            }

            var delta = ActiveObject.Center - point;
            AddVelocityToActive(-delta / 10000);
        }

        public void HoldActiveAtPoint(Vec2 point)
        {
            if (ActiveObject == null)
            {
                return;
            }

            var delta = ActiveObject.Center - point;
            SetVelocityOfActive(-delta*10);
        }

        public void ReleaseActiveObject()
        {
            ActiveObject = null;
        }

        public void RemoveActiveObject()
        {
            if (ListGravityObjects.Contains(ActiveObject))
            {
                ListGravityObjects.Remove(ActiveObject);
            }
            ListStaticObjects.Remove(ActiveObject);
            ActiveObject = null;
        }

        public void RemoveAllMoveableObjects()
        {
            foreach (PhysicsObject obj in GetMoveableObjects())
            {
                RemovalQueue.Enqueue(obj);
            }
        }

        public void Tick(double elapsedTime)
        {

            accumulator += elapsedTime;

            //Avoid accumulator spiral of death by clamping
            if (accumulator > 0.1f)
                accumulator = 0.1f;

            while (accumulator > _dt)
            {
                BroadPhase_GeneratePairs();
                UpdatePhysics(_dt);
                ProcessRemovalQueue();
                accumulator -= _dt;
            }
        }

        #endregion

        #region Private Methods

        private void AddGravity(PhysicsObject obj, float dt)
        {
            obj.Velocity += GetGravityVector(obj) * dt;
        }

        private void ApplyConstants(PhysicsObject obj, float dt)
        {
            if (obj.Locked)
            {
                return;
            }

            AddGravity(obj, dt);
            obj.Velocity -= Friction * dt;

            if (obj.Center.Y > 2000 || obj.Center.Y < -2000 || obj.Center.X > 2000 || obj.Center.X < -2000)
            {
                RemovalQueue.Enqueue(obj);
            }
        }

        private Vec2 CalculatePointGravity(PhysicsObject obj)
        {
            var forces = new Vec2(0, 0);

            if (obj.Locked)
            {
                return forces;
            }

            foreach (var gpt in ListGravityObjects)
            {
                var diff = gpt.Center - obj.Center;
                PhysMath.RoundToZero(ref diff, 5F);

                //apply inverse square law
                var falloffMultiplier = gpt.Mass / diff.LengthSquared;

                diff.X = (int) diff.X == 0 ? 0 : diff.X * falloffMultiplier;
                diff.Y = (int) diff.Y == 0 ? 0 : diff.Y * falloffMultiplier;

                if (diff.Length > .005F)
                {
                    forces += diff;
                }
            }

            return forces;
        }

        private PhysicsObject CheckObjectAtPoint(PointF p)
        {
            foreach (var physicsObject in ListStaticObjects)
            {
                if (physicsObject.Contains(p))
                {
                    return physicsObject;
                }
            }

            return null;
        }

        private Vec2 GetGravityVector(PhysicsObject obj)
        {
            return CalculatePointGravity(obj) + Gravity;
        }

        private void ProcessRemovalQueue()
        {
            if (RemovalQueue.Count > 0)
            {
                var obj = RemovalQueue.Dequeue();
                ListStaticObjects.Remove(obj);
                ListGravityObjects.Remove(obj);
            }
        }

        private void UpdatePhysics(float dt)
        {
            for (int i = 0; i < PHYSICS_ITERATIONS; i++)
            {

                foreach (var pair in ListCollisionPairs)
                {
                    var objA = pair.A;
                    var objB = pair.B;

                    var m = new Manifold();
                    var collision = false;

                    if (objA.ShapeType == PhysicsObject.Type.Circle && objB.ShapeType == PhysicsObject.Type.Box)
                    {
                        m.A = objB;
                        m.B = objA;
                    }
                    else
                    {
                        m.A = objA;
                        m.B = objB;
                    }

                    //Box vs anything
                    if (m.A.ShapeType == PhysicsObject.Type.Box)
                    {
                        if (m.B.ShapeType == PhysicsObject.Type.Box)
                        {
                            //continue;
                            if (Collision.AABBvsAABB(ref m))
                            {
                                collision = true;
                            }
                        }

                        if (m.B.ShapeType == PhysicsObject.Type.Circle)
                        {
                            if (Collision.AABBvsCircle(ref m))
                            {
                                collision = true;
                            }
                        }
                    }

                    //Circle Circle
                    else
                    {
                        if (m.B.ShapeType == PhysicsObject.Type.Circle)
                        {
                            if (Collision.CirclevsCircle(ref m))
                            {
                                collision = true;
                            }
                        }
                    }

                    //Resolve Collision
                    if (collision)
                    {
                        Collision.ResolveCollision(ref m);
                        Collision.PositionalCorrection(ref m);
                        objA.LastCollision = m;
                        objB.LastCollision = m;
                    }
                }
            }

            for (var i = 0; i < ListStaticObjects.Count; i++)
            {
                ApplyConstants(ListStaticObjects[i], dt);
                ListStaticObjects[i].Move(dt);
            }
        }

        #endregion

        #region Private Events

        private void BroadPhase_GeneratePairs()
        {
            ListCollisionPairs.Clear();

            // Create the spatial hash dictionary keyed by grid cell coordinates.
            Dictionary<(int, int), List<PhysicsObject>> spatialHash = new Dictionary<(int, int), List<PhysicsObject>>();
            float cellSize = SpatialHashCellSize;

            // Populate the spatial hash.
            foreach (var obj in ListStaticObjects)
            {
                int minX = (int)Math.Floor(obj.Aabb.Min.X / cellSize);
                int minY = (int)Math.Floor(obj.Aabb.Min.Y / cellSize);
                int maxX = (int)Math.Floor(obj.Aabb.Max.X / cellSize);
                int maxY = (int)Math.Floor(obj.Aabb.Max.Y / cellSize);

                for (int x = minX; x <= maxX; x++)
                {
                    for (int y = minY; y <= maxY; y++)
                    {
                        var key = (x, y);
                        if (!spatialHash.TryGetValue(key, out List<PhysicsObject> cellList))
                        {
                            cellList = new List<PhysicsObject>();
                            spatialHash[key] = cellList;
                        }
                        cellList.Add(obj);
                    }
                }
            }

            // Use a hash set to avoid adding duplicate pairs (since objects may share multiple cells).
            HashSet<(PhysicsObject, PhysicsObject)> pairSet = new HashSet<(PhysicsObject, PhysicsObject)>(new PhysicsObjectPairComparer());

            // For each cell, add collision pairs from objects that share the cell.
            foreach (var cell in spatialHash.Values)
            {
                int count = cell.Count;
                if (count > 1)
                {
                    for (int i = 0; i < count - 1; i++)
                    {
                        for (int j = i + 1; j < count; j++)
                        {
                            PhysicsObject objA = cell[i];
                            PhysicsObject objB = cell[j];

                            // Add the pair if it has not already been processed.
                            if (pairSet.Add((objA, objB)))
                            {
                                ListCollisionPairs.Add(new CollisionPair(objA, objB));
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region Private Helper Classes

        /// <summary>
        /// Custom comparer for collision pair tuples so that the order of objects does not matter.
        /// </summary>
        private class PhysicsObjectPairComparer : IEqualityComparer<(PhysicsObject, PhysicsObject)>
        {
            public bool Equals((PhysicsObject, PhysicsObject) x, (PhysicsObject, PhysicsObject) y)
            {
                return (ReferenceEquals(x.Item1, y.Item1) && ReferenceEquals(x.Item2, y.Item2)) ||
                       (ReferenceEquals(x.Item1, y.Item2) && ReferenceEquals(x.Item2, y.Item1));
            }

            public int GetHashCode((PhysicsObject, PhysicsObject) pair)
            {
                // XOR is order-independent.
                return pair.Item1.GetHashCode() ^ pair.Item2.GetHashCode();
            }
        }

        #endregion
    }
}
