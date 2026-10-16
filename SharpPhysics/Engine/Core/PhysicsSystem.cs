#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SharpPhysics.Engine.Classes;
using SharpPhysics.Engine.Constraints;
using SharpPhysics.Engine.Helpers;
using SharpPhysics.Engine.Objects;

namespace SharpPhysics.Engine.Core;

public class PhysicsSystem
{
    #region Public Properties

    public float GravityScale = 30F;
    public Vector3 Gravity { get; set; } = new Vector3(0, 9.8f, 0);
    public float TimeScale { get; set; } = 1.0f;
    public bool IsPaused { get; set; } = false;

    /// <summary>Fixed physics steps run by the most recent <see cref="Tick"/>.</summary>
    public int LastTickSteps { get; private set; }

    // Set this to roughly your average AABB size – adjust as needed.
    public float SpatialHashCellSize { get; set; } = 50F;

    // TODO(3D): sleep thresholds, used by the disabled sleep code in PhysicsObject.
    /*
    public static float WakeImpulseThreshold { get; private set; } = 4.0f;
    public static float LinearSleepThreshold { get; set; } = 0.06f;
    public static float AngularSleepThreshold { get; set; } = 0.11f;
    public static float SleepTimeThreshold { get; set; } = 0.9f;
    */

    #endregion

    #region Local Declarations

    public const float FPS = 144;
    private const float _dt = 1 / FPS;
    private const int PHYSICS_ITERATIONS = 8;
    private double _accumulator = 0;

    public PhysicsObject? ActiveObject;
    public readonly List<PhysicsObject> ListStaticObjects = new();
    public readonly List<Constraint> Constraints = new();
    public readonly List<PhysicsObject> ListGravityObjects = new();

    private readonly List<CollisionPair> _collisionPairs = new();
    private readonly ManifoldPool _manifoldPool = new();
    private readonly CollisionPairPool _collisionPairPool = new();
    private readonly Queue<Constraint> _constraintRemovalQueue = new();
    private readonly Queue<object> _removalQueue = new();

    internal IEnumerable<PhysicsObject> GetMoveableObjects()
    {
        for (int i = ListStaticObjects.Count - 1; i >= 0; i--)
        {
            var obj = ListStaticObjects[i];
            if (!obj.Locked)
            {
                yield return obj;
            }
        }
    }

    internal static void SetVelocity(PhysicsObject physicsObject, Vector3 velocity)
    {
        physicsObject.Velocity = velocity;
    }

    #endregion

    #region Reusable Broadphase Fields

    private readonly Dictionary<(int, int, int), List<PhysicsObject>> _spatialHash = new();
    private readonly HashSet<(PhysicsObject, PhysicsObject)> _pairSet = new(new PhysicsObjectPairComparer());

    #endregion

    #region Public Methods

    public bool ActivateAtPoint(Vector3 p)
    {
        ActiveObject = ListStaticObjects.FirstOrDefault(physicsObject => physicsObject.Contains(p));
        return ActiveObject != null;
    }

    public void AddVelocityToActive(Vector3 velocityDelta)
    {
        if (ActiveObject == null || ActiveObject.Locked)
            return;

        ActiveObject.Velocity += velocityDelta;
    }

    public void SetVelocityOfActive(Vector3 velocity)
    {
        if (ActiveObject == null || ActiveObject.Locked)
            return;

        ActiveObject.Velocity = velocity;
    }

    public void FreezeStaticObjects()
    {
        foreach (var physicsObject in ListStaticObjects)
        {
            physicsObject.Velocity = Vector3.Zero;
        }
    }

    public Vector3 GetActiveObjectCenter()
    {
        return ActiveObject?.Center ?? Vector3.Zero;
    }

    public void MoveActiveTowardsPoint(Vector3 point)
    {
        if (ActiveObject == null)
            return;

        var delta = ActiveObject.Center - point;
        AddVelocityToActive(-delta / 10000);
    }

    public void HoldActiveAtPoint(Vector3 point)
    {
        if (ActiveObject == null)
            return;

        var delta = ActiveObject.Center - point;
        SetVelocityOfActive(-delta * 10);
    }

    public void ReleaseActiveObject()
    {
        ActiveObject = null;
    }

    public void DestroyObject(object obj)
    {
        if (obj is PhysicsObject po)
        {
            po.IsDestroyed = true;
        }
        _removalQueue.Enqueue(obj);
    }

    public void RemoveActiveObject()
    {
        if (ActiveObject != null)
            _removalQueue.Enqueue(ActiveObject);
        ActiveObject = null;
    }

    public void RemoveAllMoveableObjects()
    {
        foreach (PhysicsObject obj in GetMoveableObjects())
        {
            _removalQueue.Enqueue(obj);
        }
    }

    public void Tick(double elapsedTime)
    {
        LastTickSteps = 0;
        if (IsPaused)
            return;

        _accumulator += elapsedTime;

        // Avoid accumulator spiral of death by clamping
        if (_accumulator > 0.1f)
            _accumulator = 0.1f;

        while (_accumulator > _dt)
        {
            BroadPhase_GeneratePairs();
            UpdatePhysics(_dt * TimeScale);
            ProcessRemovalQueue();
            _accumulator -= _dt;
            LastTickSteps++;
        }
    }

    public void ProcessRemovalQueue()
    {
        while (_removalQueue.Count > 0)
        {
            var obj = _removalQueue.Dequeue();
            if (obj is PhysicsObject physicsObject)
            {
                foreach (var constraint in physicsObject.Constraints)
                {
                    _constraintRemovalQueue.Enqueue(constraint);
                }

                ListStaticObjects.Remove(physicsObject);
                ListGravityObjects.Remove(physicsObject);
            }
            else if (obj is Constraint constraint)
            {
                _constraintRemovalQueue.Enqueue(constraint);
            }
        }

        while (_constraintRemovalQueue.Count > 0)
        {
            var constraint = _constraintRemovalQueue.Dequeue();

            constraint.A.ConnectedObjects.Remove(constraint.B);
            constraint.B.ConnectedObjects.Remove(constraint.A);
            Constraints.Remove(constraint);
        }
    }

    public void HandleConstraints(float dt)
    {
        foreach (var constraint in Constraints)
        {
            constraint.ApplyConstraint(dt);

            if (constraint.CanBreak && constraint.IsBroken)
            {
                _constraintRemovalQueue.Enqueue(constraint);
            }
        }
    }

    #endregion

    #region Private Methods

    private void ApplyConstants(PhysicsObject obj, float dt)
    {
        if (obj.Locked)
            return;

        obj.Velocity += GetGravityVector(obj) * dt;
    }

    private Vector3 CalculatePointGravity(PhysicsObject obj)
    {
        var forces = Vector3.Zero;

        if (obj.Locked)
            return forces;

        foreach (var gpt in ListGravityObjects)
        {
            var diff = gpt.Center - obj.Center;
            PhysMath.RoundToZero(ref diff, 5F);

            // Apply inverse square law
            var falloffMultiplier = gpt.Mass / diff.LengthSquared();

            diff.X = (int)diff.X == 0 ? 0 : diff.X * falloffMultiplier;
            diff.Y = (int)diff.Y == 0 ? 0 : diff.Y * falloffMultiplier;
            diff.Z = (int)diff.Z == 0 ? 0 : diff.Z * falloffMultiplier;

            if (diff.Length() > .005F)
                forces += diff;
        }

        return forces;
    }

    private Vector3 GetGravityVector(PhysicsObject obj)
    {
        return CalculatePointGravity(obj) + Gravity * GravityScale;
    }

    private void UpdatePhysics(float dt)
    {
        float dtSubstep = dt / PHYSICS_ITERATIONS;

        for (int iter = 0; iter < PHYSICS_ITERATIONS; iter++)
        {
            for (int i = 0; i < _collisionPairs.Count; i++)
            {
                var pair = _collisionPairs[i];
                Manifold m = _manifoldPool.Get();
                m.A = pair.A;
                m.B = pair.B;

                if (Collision.Detect(m))
                {
                    // Accumulate contacts during substeps (finalized once per tick)
                    m.A.AddContact(m.B, m.ContactPoint, m.Normal);
                    m.B.AddContact(m.A, m.ContactPoint, -m.Normal);

                    Collision.ResolveCollision(m);
                    Collision.PositionalCorrection(m);
                    Collision.AngularPositionalCorrection(m);
                }

                _manifoldPool.Return(m);
            }

            HandleConstraints(dtSubstep);

            for (int i = 0; i < ListStaticObjects.Count; i++)
            {
                var obj = ListStaticObjects[i];
                ApplyConstants(obj, dtSubstep);
                obj.Update(dtSubstep);
            }
        }

        // Finalize contacts once per tick (not per substep) to fire events
        for (int i = 0; i < ListStaticObjects.Count; i++)
        {
            ListStaticObjects[i].FinalizeContacts();
        }
    }

    #endregion

    #region Broad Phase Collision Detection

    private void BroadPhase_GeneratePairs()
    {
        _collisionPairPool.ReturnAll(_collisionPairs);
        _collisionPairs.Clear();

        _spatialHash.Clear();
        _pairSet.Clear();

        float cellSize = SpatialHashCellSize;

        foreach (var obj in ListStaticObjects)
        {
            if (obj.IsDestroyed)
                continue;

            int minX = (int)MathF.Floor(obj.Aabb.Min.X / cellSize);
            int minY = (int)MathF.Floor(obj.Aabb.Min.Y / cellSize);
            int minZ = (int)MathF.Floor(obj.Aabb.Min.Z / cellSize);
            int maxX = (int)MathF.Floor(obj.Aabb.Max.X / cellSize);
            int maxY = (int)MathF.Floor(obj.Aabb.Max.Y / cellSize);
            int maxZ = (int)MathF.Floor(obj.Aabb.Max.Z / cellSize);

            for (int x = minX; x <= maxX; x++)
            for (int y = minY; y <= maxY; y++)
            for (int z = minZ; z <= maxZ; z++)
            {
                var key = (x, y, z);
                if (!_spatialHash.TryGetValue(key, out var cellList))
                {
                    cellList = new List<PhysicsObject>();
                    _spatialHash[key] = cellList;
                }
                cellList.Add(obj);
            }
        }

        foreach (var cell in _spatialHash.Values)
        {
            int count = cell.Count;
            for (int i = 0; i < count - 1; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    PhysicsObject objA = cell[i];
                    PhysicsObject objB = cell[j];

                    if ((objA.CollisionMask & objB.CollisionMask) == 0)
                        continue;

                    // Constrained objects don't collide with each other
                    if (objA.ConnectedObjects.Contains(objB))
                        continue;

                    if (_pairSet.Add((objA, objB)))
                    {
                        _collisionPairs.Add(_collisionPairPool.Get(objA, objB));
                    }
                }
            }
        }
    }

    #endregion

    #region Private Helper Classes

    /// <summary>
    /// Order-independent equality for collision pair tuples.
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
            return pair.Item1.GetHashCode() ^ pair.Item2.GetHashCode();
        }
    }

    #endregion
}
