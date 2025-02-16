using System;
using System.Collections.Generic;
using System.Drawing;
using physics.Engine.Classes;
using physics.Engine.Extensions;
using physics.Engine.Helpers;
using physics.Engine.Structs;
using SFML.System;

namespace physics.Engine
{
    public class PhysicsSystem
    {
        #region Public Properties

        public float GravityScale = 10F;
        public Vector2f Gravity { get; set; }
        public float Friction { get; set; }

        // Set this to roughly your average AABB size – adjust as needed.
        public float SpatialHashCellSize { get; set; } = 20F;

        #endregion

        #region Local Declarations

        public const float FPS = 60;
        private const float _dt = 1 / FPS;
        private const int PHYSICS_ITERATIONS = 4;
        private double accumulator = 0;

        public static PhysicsObject ActiveObject;
        public static readonly List<SFMLShader> ListShaders = new List<SFMLShader>();
        public static readonly List<CollisionPair> ListCollisionPairs = new List<CollisionPair>();
        public static readonly List<PhysicsObject> ListGravityObjects = new List<PhysicsObject>();
        public static readonly List<PhysicsObject> ListStaticObjects = new List<PhysicsObject>();

        internal IEnumerable<PhysicsObject> GetMoveableObjects()
        {
            for (int i = ListStaticObjects.Count - 1; i >= 0; i--)
            {
                var obj = ListStaticObjects[i];
                if (!obj.Locked && obj.Mass < 1000000)
                {
                    yield return obj;
                }
            }
        }

        internal void SetVelocity(PhysicsObject physicsObject, Vector2f velocity)
        {
            physicsObject.Velocity = velocity;
        }

        public static readonly Queue<PhysicsObject> RemovalQueue = new Queue<PhysicsObject>();

        #endregion

        #region Reusable Broadphase Fields

        // These fields are now allocated once and reused every tick.
        private readonly Dictionary<(int, int), List<PhysicsObject>> _spatialHash = new Dictionary<(int, int), List<PhysicsObject>>();
        private readonly HashSet<(PhysicsObject, PhysicsObject)> _pairSet =
            new HashSet<(PhysicsObject, PhysicsObject)>(new PhysicsObjectPairComparer());

        #endregion

        #region Constructors

        public PhysicsSystem()
        {
            Gravity = new Vector2f { X = 0, Y = 10F * GravityScale };
            Friction = 1F;
        }

        #endregion

        #region Public Methods

        public static PhysicsObject CreateStaticCircle(Vector2f loc, int radius, float restitution, bool locked, SFMLShader shader)
        {
            var oAabb = new AABB
            {
                Min = new Vector2f { X = loc.X - radius, Y = loc.Y - radius },
                Max = new Vector2f { X = loc.X + radius, Y = loc.Y + radius }
            };
            PhysMath.CorrectBoundingBox(ref oAabb);
            var obj = new PhysicsObject(oAabb, PhysicsObject.Type.Circle, restitution, locked, shader);
            ListStaticObjects.Add(obj);
            return obj;
        }

        public static PhysicsObject CreateStaticBox(Vector2f start, Vector2f end, bool locked, SFMLShader shader, float mass)
        {
            var oAabb = new AABB
            {
                Min = new Vector2f { X = start.X, Y = start.Y },
                Max = new Vector2f { X = end.X, Y = end.Y }
            };
            PhysMath.CorrectBoundingBox(ref oAabb);
            var obj = new PhysicsObject(oAabb, PhysicsObject.Type.Box, .95F, locked, shader, mass);
            ListStaticObjects.Add(obj);
            return obj;
        }

        public bool ActivateAtPoint(Vector2f p)
        {
            ActiveObject = CheckObjectAtPoint(p);

            if (ActiveObject == null)
            {
                ActiveObject = null;
                return false;
            }

            return true;
        }

        public void AddVelocityToActive(Vector2f velocityDelta)
        {
            if (ActiveObject == null || ActiveObject.Mass >= 1000000)
            {
                return;
            }

            ActiveObject.Velocity += velocityDelta;
        }

        public void SetVelocityOfActive(Vector2f velocityDelta)
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
                physicsObject.Velocity = new Vector2f { X = 0, Y = 0 };
            }
        }

        public Vector2f GetActiveObjectCenter()
        {
            if (ActiveObject == null)
            {
                return new Vector2f();
            }

            return new Vector2f(ActiveObject.Center.X, ActiveObject.Center.Y);
        }

        public void MoveActiveTowardsPoint(Vector2f point)
        {
            if (ActiveObject == null)
            {
                return;
            }

            var delta = ActiveObject.Center - point;
            AddVelocityToActive(-delta / 10000);
        }

        public void HoldActiveAtPoint(Vector2f point)
        {
            if (ActiveObject == null)
            {
                return;
            }

            var delta = ActiveObject.Center - point;
            SetVelocityOfActive(-delta * 10);
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

            // Avoid accumulator spiral of death by clamping
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
            obj.Velocity = obj.Velocity.Minus(Friction * dt);

            if (obj.Center.Y > 2000 || obj.Center.Y < -2000 || obj.Center.X > 2000 || obj.Center.X < -2000)
            {
                RemovalQueue.Enqueue(obj);
            }
        }

        private Vector2f CalculatePointGravity(PhysicsObject obj)
        {
            var forces = new Vector2f(0, 0);

            if (obj.Locked)
            {
                return forces;
            }

            foreach (var gpt in ListGravityObjects)
            {
                var diff = gpt.Center - obj.Center;
                PhysMath.RoundToZero(ref diff, 5F);

                // Apply inverse square law
                var falloffMultiplier = gpt.Mass / diff.LengthSquared();

                diff.X = (int)diff.X == 0 ? 0 : diff.X * falloffMultiplier;
                diff.Y = (int)diff.Y == 0 ? 0 : diff.Y * falloffMultiplier;

                if (diff.Length() > .005F)
                {
                    forces += diff;
                }
            }

            return forces;
        }

        private PhysicsObject CheckObjectAtPoint(Vector2f p)
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

        private Vector2f GetGravityVector(PhysicsObject obj)
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

                    // Box vs anything
                    if (m.A.ShapeType == PhysicsObject.Type.Box)
                    {
                        if (m.B.ShapeType == PhysicsObject.Type.Box)
                        {
                            if (Collision.AABBvsAABB(ref m))
                            {
                                collision = true;
                            }
                        }
                        else if (m.B.ShapeType == PhysicsObject.Type.Circle)
                        {
                            if (Collision.AABBvsCircle(ref m))
                            {
                                collision = true;
                            }
                        }
                    }
                    // Circle vs Circle
                    else if (m.B.ShapeType == PhysicsObject.Type.Circle)
                    {
                        if (Collision.CirclevsCircle(ref m))
                        {
                            collision = true;
                        }
                    }

                    // Resolve collision if one was detected
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

        #region Broad Phase Collision Detection

        private void BroadPhase_GeneratePairs()
        {
            // Reuse the ListCollisionPairs (clear it first)
            ListCollisionPairs.Clear();

            // Clear reusable structures to avoid allocations
            _spatialHash.Clear();
            _pairSet.Clear();

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
                        if (!_spatialHash.TryGetValue(key, out List<PhysicsObject> cellList))
                        {
                            cellList = new List<PhysicsObject>();
                            _spatialHash[key] = cellList;
                        }
                        cellList.Add(obj);
                    }
                }
            }

            // Use the reusable hash set to avoid duplicate pairs.
            foreach (var cell in _spatialHash.Values)
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
                            if (_pairSet.Add((objA, objB)))
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
