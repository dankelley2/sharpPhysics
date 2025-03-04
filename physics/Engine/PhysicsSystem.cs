using System;
using System.Collections.Generic;
using System.Drawing;
using physics.Engine.Classes;
using physics.Engine.Objects;
using physics.Engine.Extensions;
using physics.Engine.Helpers;
using physics.Engine.Shapes;
using physics.Engine.Structs;
using SFML.System;
using physics.Engine.Constraints;
using physics.Engine.Shaders;
using System.Numerics;
using System.Linq;

namespace physics.Engine
{
    public class PhysicsSystem
    {
        #region Public Properties

        public float GravityScale = 30F;
        public Vector2f Gravity { get; set; }
        public float Friction { get; set; }

        // Set this to roughly your average AABB size – adjust as needed.
        public float SpatialHashCellSize { get; set; } = 10F;

        #endregion

        #region Local Declarations
        public const float FPS = 144;
        private const float _dt = 1 / FPS;
        private const int PHYSICS_ITERATIONS = 4;
        private double accumulator = 0;

        public static PhysicsObject ActiveObject;
        private static readonly List<SFMLShader> ListShaders = new List<SFMLShader>();
        private static readonly List<CollisionPair> ListCollisionPairs = new List<CollisionPair>();
        public static readonly List<PhysicsObject> ListGravityObjects = new List<PhysicsObject>();
        public static readonly List<PhysicsObject> ListStaticObjects = new List<PhysicsObject>();
        private readonly ManifoldPool _manifoldPool = new ManifoldPool();
        public List<Constraint> Constraints = new List<Constraint>();

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
            // Create the circle shape using the given radius.
            IShape shape = new CirclePhysShape(radius);
            // For a circle, the center is the provided location.
            var obj = new PhysicsObject(shape, loc, restitution, locked, shader, canRotate: true);
            ListStaticObjects.Add(obj);
            return obj;
        }

        public static PhysicsObject CreateStaticBox(Vector2f start, Vector2f end, bool locked, SFMLShader shader, float mass)
        {
            // Ensure start and end define the correct bounds.
            var min = new Vector2f(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y));
            var max = new Vector2f(Math.Max(start.X, end.X), Math.Max(start.Y, end.Y));

            // Compute width and height.
            float width = max.X - min.X;
            float height = max.Y - min.Y;
            // Calculate the center from the corrected bounds.
            var center = new Vector2f((min.X + max.X) / 2f, (min.Y + max.Y) / 2f);

            // Create a box shape with the computed dimensions.
            IShape shape = new BoxPhysShape(width, height);
            var obj = new PhysicsObject(shape, center, 0.2f, locked, shader, mass);
            ListStaticObjects.Add(obj);
            return obj;
        }

        public static PhysicsObject CreateStaticBox2(Vector2f start, Vector2f end, bool locked, SFMLShader shader, float mass)
        {
            // Compute the corrected bounding box.
            var min = new Vector2f(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y));
            var max = new Vector2f(Math.Max(start.X, end.X), Math.Max(start.Y, end.Y));

            // Compute width and height.
            float width = max.X - min.X;
            float height = max.Y - min.Y;
            // Compute the center.
            var center = new Vector2f((min.X + max.X) / 2f, (min.Y + max.Y) / 2f);

            // Create the box shape.
            IShape shape = new BoxPhysShape(width, height);
            var obj = new PhysicsObject(shape, center, 0.2f, locked, shader, mass, canRotate: true);
            obj.Friction = 0.8f;
            ListStaticObjects.Add(obj);
            return obj;
        }

        public static PhysicsObject CreatePolygon(Vector2f origin, Vector2f[] points, SFMLShader shader, bool locked = false, bool canRotate = true)
        {
            // Create the polygon shape.
            IShape shape = new PolygonPhysShape(points);
            var obj = new PhysicsObject(shape, origin, 0.2f, locked, shader, canRotate: canRotate);
            obj.Friction = 0.8f;
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

            var friction = Friction * dt;

            // Store velocity in a temporary variable.
            var velocity = obj.Velocity;
            velocity.X += velocity.X == 0 ? 0 : velocity.X > 0 ? -friction : friction;
            velocity.Y += velocity.Y == 0 ? 0 : velocity.Y > 0 ? -friction : friction;
            // Assign the modified velocity back.
            obj.Velocity = velocity;

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
            while (RemovalQueue.Count > 0)
            {
                var obj = RemovalQueue.Dequeue();
                ListStaticObjects.Remove(obj);
                ListGravityObjects.Remove(obj);
            }
        }

        public void HandleConstraints(float dt)
        {
            foreach (var constraint in Constraints)
            {
                constraint.ApplyConstraint(dt);
            }
        }

        private void UpdatePhysics(float dt)
        {
            // Loop over physics iterations.
            for (int iter = 0; iter < PHYSICS_ITERATIONS; iter++)
            {
                //TODO: Constraints
                //HandleConstraints(dt);
                
                // Use a for-loop to iterate over collision pairs.
                for (int i = 0; i < ListCollisionPairs.Count; i++)
                {
                    var pair = ListCollisionPairs[i];
                    var objA = pair.A;
                    var objB = pair.B;

                    // Cache shape references.
                    var shapeA = objA.Shape;
                    var shapeB = objB.Shape;

                    // Retrieve a manifold from the pool.
                    Manifold m = _manifoldPool.Get();
                    bool collision = false;

                    // Set ordering: if objA is a circle and objB is a box, swap them.
                    if (shapeA is CirclePhysShape && (shapeB is BoxPhysShape or PolygonPhysShape))
                    {
                        m.A = objB;
                        m.B = objA;
                    }
                    else
                    {
                        m.A = objA;
                        m.B = objB;
                    }

                    // Cache again for m.A and m.B.
                    var shapeA2 = m.A.Shape;
                    var shapeB2 = m.B.Shape;

                    // Determine collision detection method based on shape types.
                    if (shapeA2 is BoxPhysShape or PolygonPhysShape)
                    {
                        if (shapeB2 is BoxPhysShape or PolygonPhysShape)
                        {
                            collision = Collision.PolygonVsPolygon(ref m);
                        }
                        else if (shapeB2 is CirclePhysShape)
                        {
                            collision = Collision.PolygonVsCircle(ref m);
                        }
                    }
                    else if (shapeA2 is CirclePhysShape && shapeB2 is CirclePhysShape)
                    {
                        collision = Collision.CirclevsCircle(ref m);
                    }

                    // Resolve collision if detected.
                    if (collision)
                    {
                        // Add to object contact points once per physics tick
                        if (iter == PHYSICS_ITERATIONS - 1)
                        {
                            m.A.ContactPoints.Add(m.B, (m.ContactPoint, m.Normal));
                            m.B.ContactPoints.Add(m.A, (m.ContactPoint, -m.Normal));
                        }

                        // Resolve Collision
                        Collision.ResolveCollisionRotational(ref m);
                        Collision.PositionalCorrection(ref m);
                        Collision.AngularPositionalCorrection(ref m);
                    }
                    else
                    {
                        // Return manifold to pool if no collision.
                        _manifoldPool.Return(m);
                    }
                }
            }

            // Process static objects.
            for (int i = 0; i < ListStaticObjects.Count; i++)
            {
                var staticObj = ListStaticObjects[i];
                ApplyConstants(staticObj, dt);
                staticObj.Update(dt);
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
                // Get min / max extents, divide by cellSize for grid coordinates.
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
