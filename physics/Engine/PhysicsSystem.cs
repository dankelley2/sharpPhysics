using System;
using System.Collections.Generic;
using physics.Engine.Classes;
using physics.Engine.Objects;
using physics.Engine.Helpers;
using physics.Engine.Shapes;
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
        public Vector2 Gravity { get; set; } = new Vector2(0, 9.8f);
        public float Friction { get; set; }
        public float TimeScale { get; set; } = 1.0f;
        public bool IsPaused { get; set; } = false;

        // Set this to roughly your average AABB size – adjust as needed.
        public float SpatialHashCellSize { get; set; } = 10F;

        // Sleep / Wake thresholds
        public static float WakeImpulseThreshold { get; private set; } = 4.0f;
        public static float LinearSleepThreshold { get; set; } = 0.06f;
        public static float AngularSleepThreshold { get; set; } = 0.11f;
        public static float SleepTimeThreshold { get; set; } = 0.9f;

        #endregion

        #region Local Declarations
        public const float FPS = 144;
        private const float _dt = 1 / FPS;
        private const int PHYSICS_ITERATIONS = 8;
        private double accumulator = 0;

        public PhysicsObject? ActiveObject;
        private readonly List<CollisionPair> ListCollisionPairs = new List<CollisionPair>();
        public readonly List<PhysicsObject> ListGravityObjects = new List<PhysicsObject>();
        public readonly List<PhysicsObject> ListStaticObjects = new List<PhysicsObject>();
        private readonly ManifoldPool _manifoldPool = new ManifoldPool();
        private readonly CollisionPairPool _collisionPairPool = new CollisionPairPool();
        public List<Constraint> Constraints = new List<Constraint>();
        private Queue<Constraint> _constraintRemovalQueue = new Queue<Constraint>();

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

        internal void SetVelocity(PhysicsObject physicsObject, Vector2 velocity)
        {
            physicsObject.Velocity = velocity;
        }

        public readonly Queue<PhysicsObject> RemovalQueue = new Queue<PhysicsObject>();

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
            Friction = 1F;
        }

        #endregion

        #region Public Methods

        public PhysicsObject CreateStaticCircle(Vector2 loc, int radius, float restitution, bool locked, SFMLShader shader)
        {
            // Create the circle shape using the given radius.
            IShape shape = new CirclePhysShape(radius);
            // For a circle, the center is the provided location.
            var obj = new PhysicsObject(shape, loc, restitution, locked, shader, canRotate: true);
            ListStaticObjects.Add(obj);
            return obj;
        }

        public PhysicsObject CreateStaticCircle(Vector2 loc, int radius, float restitution, bool locked, SFMLShader shader, float mass)
        {
            // Create the circle shape using the given radius.
            IShape shape = new CirclePhysShape(radius);
            // For a circle, the center is the provided location.
            var obj = new PhysicsObject(shape, loc, restitution, locked, shader, mass, canRotate: true);
            ListStaticObjects.Add(obj);
            return obj;
        }

        public PhysicsObject CreateStaticBox(Vector2 start, Vector2 end, bool locked, SFMLShader shader, float mass)
        {
            // Ensure start and end define the correct bounds.
            var min = new Vector2(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y));
            var max = new Vector2(Math.Max(start.X, end.X), Math.Max(start.Y, end.Y));

            // Compute width and height.
            float width = max.X - min.X;
            float height = max.Y - min.Y;
            // Calculate the center from the corrected bounds.
            var center = new Vector2((min.X + max.X) / 2f, (min.Y + max.Y) / 2f);

            // Create a box shape with the computed dimensions.
            IShape shape = new BoxPhysShape(width, height);
            var obj = new PhysicsObject(shape, center, 0.2f, locked, shader, mass);
            ListStaticObjects.Add(obj);
            return obj;
        }

        public PhysicsObject CreateStaticBox2(Vector2 start, Vector2 end, bool locked, SFMLShader shader, float mass)
        {
            // Compute the corrected bounding box.
            var min = new Vector2(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y));
            var max = new Vector2(Math.Max(start.X, end.X), Math.Max(start.Y, end.Y));

            // Compute width and height.
            float width = max.X - min.X;
            float height = max.Y - min.Y;
            // Compute the center.
            var center = new Vector2((min.X + max.X) / 2f, (min.Y + max.Y) / 2f);

            // Create the box shape.
            IShape shape = new BoxPhysShape(width, height);
            var obj = new PhysicsObject(shape, center, 0.2f, locked, shader, mass, canRotate: true);
            ListStaticObjects.Add(obj);
            return obj;
        }

        public PhysicsObject CreatePolygon(Vector2 origin, Vector2[] points, SFMLShader shader, bool locked = false, bool canRotate = true)
        {
            // Create the polygon shape.
            IShape shape = new PolygonPhysShape(points);
            var obj = new PhysicsObject(shape, origin, 0.2f, locked, shader, canRotate: canRotate);
            ListStaticObjects.Add(obj);
            return obj;
        }

        /// <summary>
        /// Creates a compound body from a potentially concave polygon by decomposing it
        /// into convex pieces and welding them together.
        /// </summary>
        /// <param name="origin">World position for the compound body.</param>
        /// <param name="vertices">Local-space vertices of the (possibly concave) polygon.</param>
        /// <param name="shader">Shader to use for rendering all pieces.</param>
        /// <param name="canRotate">Whether the pieces can rotate.</param>
        /// <param name="canBreak">Whether the weld constraints can break under stress.</param>
        /// <returns>A CompoundBody containing all pieces and their constraints.</returns>
        public CompoundBody CreateConcavePolygon(Vector2 origin, Vector2[] vertices, SFMLShader shader, bool canRotate = true, bool canBreak = false)
        {
            var compound = new CompoundBody();

            // Decompose the concave polygon into convex pieces
            var convexPieces = PolygonDecomposition.DecomposeToConvex(vertices);

            if (convexPieces.Count == 0)
            {
                // Fallback: treat as convex
                var obj = CreatePolygon(origin, vertices, shader, false, canRotate);
                compound.Parts.Add(obj);
                return compound;
            }

            // Calculate the centroid of the original polygon to use as reference
            Vector2 originalCentroid = PolygonDecomposition.ComputeCentroid(vertices);

            // Create physics objects for each convex piece
            foreach (var pieceVertices in convexPieces)
            {
                // Calculate the centroid of this piece (in the original local space)
                Vector2 pieceCentroid = PolygonDecomposition.ComputeCentroid(pieceVertices);

                // Calculate world position for this piece's centroid
                // The piece will be centered around its own centroid by PolygonPhysShape
                Vector2 pieceWorldPos = origin + (pieceCentroid - originalCentroid);

                // Pass the raw piece vertices - PolygonPhysShape will center them automatically
                var piece = CreatePolygon(pieceWorldPos, pieceVertices, shader, false, canRotate);
                compound.Parts.Add(piece);
            }

            // Weld adjacent pieces together
            if (compound.Parts.Count > 1)
            {
                WeldAdjacentPieces(compound, convexPieces, origin, originalCentroid, canBreak);
            }

            return compound;
        }

        /// <summary>
        /// Finds and welds adjacent convex pieces that share edges or vertices.
        /// Uses a spanning tree approach to minimize constraints: only (n-1) welds for n pieces.
        /// </summary>
        private void WeldAdjacentPieces(CompoundBody compound, List<Vector2[]> convexPieces, Vector2 origin, Vector2 originalCentroid, bool canBreak)
        {
            const float edgeTolerance = 0.1f;
            int n = convexPieces.Count;

            // Union-Find to track connected components
            int[] parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;

            int Find(int x) => parent[x] == x ? x : parent[x] = Find(parent[x]);

            void Union(int a, int b)
            {
                int pa = Find(a), pb = Find(b);
                if (pa != pb) parent[pa] = pb;
            }

            // First pass: find all adjacencies and build spanning tree
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    // Skip if already in same component
                    //if (Find(i) == Find(j))
                    //    continue;

                    // Check for shared edge first (stronger connection)
                    bool adjacent = FindSharedEdge(convexPieces[i], convexPieces[j], edgeTolerance).HasValue;

                    // If no shared edge, check for shared vertex
                    if (!adjacent)
                        adjacent = FindSharedVertex(convexPieces[i], convexPieces[j], edgeTolerance).HasValue;

                    if (adjacent)
                    {
                        // Connect these pieces
                        Union(i, j);

                        var partA = compound.Parts[i];
                        var partB = compound.Parts[j];
                        var halfdiff = (partB.Center - partA.Center) / 2f;
                        var weld = new WeldConstraint(partA, partB, halfdiff, -halfdiff, canBreak);
                        Constraints.Add(weld);
                        compound.Constraints.Add(weld);
                    }
                }
            }
        }

        /// <summary>
        /// Finds a shared vertex between two polygons (for star-shaped decompositions where pieces meet at a point).
        /// </summary>
        private Vector2? FindSharedVertex(Vector2[] polyA, Vector2[] polyB, float tolerance)
        {
            float toleranceSq = tolerance * tolerance;

            foreach (var vertA in polyA)
            {
                foreach (var vertB in polyB)
                {
                    if (Vector2.DistanceSquared(vertA, vertB) < toleranceSq)
                    {
                        return vertA;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Finds a shared edge between two polygons, if one exists.
        /// Returns the two endpoints of the shared edge, or null if no shared edge.
        /// </summary>
        private (Vector2, Vector2)? FindSharedEdge(Vector2[] polyA, Vector2[] polyB, float tolerance)
        {
            float toleranceSq = tolerance * tolerance;

            for (int i = 0; i < polyA.Length; i++)
            {
                int nextI = (i + 1) % polyA.Length;
                Vector2 a1 = polyA[i];
                Vector2 a2 = polyA[nextI];

                for (int j = 0; j < polyB.Length; j++)
                {
                    int nextJ = (j + 1) % polyB.Length;
                    Vector2 b1 = polyB[j];
                    Vector2 b2 = polyB[nextJ];

                    // Check if edges match (in opposite direction for adjacent polygons)
                    if (Vector2.DistanceSquared(a1, b2) < toleranceSq &&
                        Vector2.DistanceSquared(a2, b1) < toleranceSq)
                    {
                        return (a1, a2);
                    }

                    // Also check same direction (depending on winding)
                    if (Vector2.DistanceSquared(a1, b1) < toleranceSq &&
                        Vector2.DistanceSquared(a2, b2) < toleranceSq)
                    {
                        return (a1, a2);
                    }
                }
            }

            return null;
        }


        public bool ActivateAtPoint(Vector2 p)
        {
            ActiveObject = CheckObjectAtPoint(p);

            if (ActiveObject == null)
            {
                ActiveObject = null;
                return false;
            }

            if (ActiveObject.Sleeping)
            {
                ActiveObject.Wake();
            }

            return true;
        }

        public void AddVelocityToActive(Vector2 velocityDelta)
        {
            if (ActiveObject == null || ActiveObject.Mass >= 1000000)
            {
                return;
            }

            ActiveObject.Velocity += velocityDelta;
        }

        public void SetVelocityOfActive(Vector2 velocityDelta)
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
                physicsObject.Velocity = new Vector2 { X = 0, Y = 0 };
            }
        }

        public Vector2 GetActiveObjectCenter()
        {
            if (ActiveObject == null)
            {
                return new Vector2();
            }

            return ActiveObject.Center;
        }

        public void MoveActiveTowardsPoint(Vector2 point)
        {
            if (ActiveObject == null)
            {
                return;
            }

            var delta = ActiveObject.Center - point;
            AddVelocityToActive(-delta / 10000);
        }

        public void HoldActiveAtPoint(Vector2 point)
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

            // Add to removal queue for proper removal
            RemovalQueue.Enqueue(ActiveObject);
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
            // If the system is paused, don't advance the physics simulation
            if (IsPaused)
                return;

            accumulator += elapsedTime;

            // Avoid accumulator spiral of death by clamping
            if (accumulator > 0.1f)
                accumulator = 0.1f;

            while (accumulator > _dt)
            {
                BroadPhase_GeneratePairs();
                UpdatePhysics(_dt * TimeScale);
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
            if (obj.Locked || obj.Sleeping)
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


        private Vector2 CalculatePointGravity(PhysicsObject obj)
        {
            var forces = new Vector2(0, 0);

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
                    forces += diff;

            }

            return forces;
        }

        private PhysicsObject CheckObjectAtPoint(Vector2 p)
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

        private Vector2 GetGravityVector(PhysicsObject obj)
        {
            return CalculatePointGravity(obj) + Gravity * GravityScale;
        }

        private void ProcessRemovalQueue()
        {
            while (RemovalQueue.Count > 0)
            {
                var obj = RemovalQueue.Dequeue();
                if (obj.ConnectedObjects.Any())
                {
                    foreach (var connectedObj in obj.ConnectedObjects)
                    {
                        var constraintToRemove = Constraints.FirstOrDefault(c => (c.A == obj && c.B == connectedObj) || (c.B == obj && c.A == connectedObj));
                        if (constraintToRemove != null)
                        {
                            _constraintRemovalQueue.Enqueue(constraintToRemove);
                        }
                    }
                }
                ListStaticObjects.Remove(obj);
                ListGravityObjects.Remove(obj);
            }

            while (_constraintRemovalQueue.Count > 0)
            {
                var constraint = _constraintRemovalQueue.Dequeue();

                constraint.A.ConnectedObjects.Remove(constraint.B);
                constraint.B.ConnectedObjects.Remove(constraint.A);

                if (!constraint.A.ConnectedObjects.Any())
                {
                    constraint.A.CanSleep = true;
                }
                if (!constraint.B.ConnectedObjects.Any())
                {
                    constraint.B.CanSleep = true;
                }
                Constraints.Remove(constraint);
            }
        }

        public void HandleConstraints(float dt)
        {
            foreach (var constraint in Constraints)
            {
                constraint.ApplyConstraint(dt);

                // Handle broken constraints, queue for removal
                if (constraint.CanBreak && constraint.IsBroken)
                {
                    _constraintRemovalQueue.Enqueue(constraint);
                }
            }
        }

        private void UpdatePhysics(float dt)
        {

            // divide dt into substeps
            float dt_substep = dt / PHYSICS_ITERATIONS;

            // Loop over physics iterations.
            for (int iter = 0; iter < PHYSICS_ITERATIONS; iter++)
            {
                // Use a for-loop to iterate over collision pairs.
                for (int i = 0; i < ListCollisionPairs.Count; i++)
                {
                    var pair = ListCollisionPairs[i];
                    var objA = pair.A;
                    var objB = pair.B;

                    // Skip narrow phase if both objects are sleeping.
                    if (objA.Sleeping && objB.Sleeping)
                        continue;

                    // Cache shape references.
                    var shapeA = objA.Shape;
                    var shapeB = objB.Shape;

                    // Retrieve a manifold from the pool.
                    Manifold m = _manifoldPool.Get();
                    bool collision = false;

                    // Set ordering: if objA is a circle and objB is a box, swap them.
                    if (shapeA.ShapeType == ShapeTypeEnum.Circle && (shapeB.ShapeType == ShapeTypeEnum.Box || shapeB.ShapeType == ShapeTypeEnum.Polygon))
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
                    if (shapeA2.ShapeType == ShapeTypeEnum.Box || shapeA2.ShapeType == ShapeTypeEnum.Polygon)
                    {
                        if (shapeB2.ShapeType == ShapeTypeEnum.Box || shapeB2.ShapeType == ShapeTypeEnum.Polygon)
                        {
                            collision = Collision.PolygonVsPolygon(ref m);
                        }
                        else if (shapeB2.ShapeType == ShapeTypeEnum.Circle)
                        {
                            collision = Collision.PolygonVsCircle(ref m);
                        }
                    }
                    else if (shapeA2.ShapeType == ShapeTypeEnum.Circle && shapeB2.ShapeType == ShapeTypeEnum.Circle)
                    {
                        collision = Collision.CirclevsCircle(ref m);
                    }

                    // Resolve collision if detected.
                    if (collision)
                    {

                        // Here, instead of immediately waking sleeping objects, we compute a rough impulse magnitude.
                        // (For example, you might approximate impulse as the penetration depth times relative velocity along the normal.)
                        float relativeVel = Math.Abs(Vector2.Dot(m.B.Velocity - m.A.Velocity, m.Normal));
                        float impulseApprox = m.Penetration * relativeVel;

                        // Only wake if a significant impulse is delivered.
                        if (impulseApprox > WakeImpulseThreshold)
                        {
                            if (objA.Sleeping && !objA.Locked)
                                objA.Wake();
                            if (objB.Sleeping && !objB.Locked)
                                objB.Wake();
                        }

                        // Add to object contact points once per physics tick
                        if (iter == PHYSICS_ITERATIONS - 1)
                        {
                            m.A.AddContact(m.B, m.ContactPoint, m.Normal);
                            m.B.AddContact(m.A, m.ContactPoint, -m.Normal);
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

                // Apply constraints after collision resolution (uses substep dt)
                HandleConstraints(dt_substep);

                // Process static objects.
                // Apply a portion of DT to static objects.
                for (int i = 0; i < ListStaticObjects.Count; i++)
                {
                    var staticObj = ListStaticObjects[i];
                    ApplyConstants(staticObj, dt_substep);
                    staticObj.Update(dt_substep);
                }
            }
        }



        #endregion

        #region Broad Phase Collision Detection

        private void BroadPhase_GeneratePairs()
        {
            // Return all pairs to the pool before clearing
            _collisionPairPool.ReturnAll(ListCollisionPairs);
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

                            // Skip pairs where both objects are sleeping.
                            if (objA.Sleeping && objB.Sleeping)
                                continue;

                            // Skip connected objects (constraints link them, collisions should be ignored)
                            if (objA.ConnectedObjects.Contains(objB))
                                continue;

                            // Add the pair if it has not already been processed.
                            if (_pairSet.Add((objA, objB)))
                            {
                                ListCollisionPairs.Add(_collisionPairPool.Get(objA, objB));
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
