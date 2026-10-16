#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using SharpPhysics.Engine.Constraints;
using SharpPhysics.Engine.Helpers;
using SharpPhysics.Engine.Shapes;
using SharpPhysics.Engine.Structs;
using SharpPhysics.Rendering.Shaders;

namespace SharpPhysics.Engine.Objects;

public class PhysicsObject
{
    public readonly uint CollisionMask;
    public IShape Shape { get; protected set; }
    public AABB Aabb { get; protected set; }
    public Vector3 Center { get; protected set; }
    public Vector3 Velocity { get; set; }
    public Quaternion Orientation { get; set; } = Quaternion.Identity;
    /// <summary>World-space angular velocity (axis * radians per second).</summary>
    public Vector3 AngularVelocity { get; set; }
    public float Restitution { get; set; }
    public float Mass { get; protected set; }
    public float IMass { get; protected set; }
    /// <summary>Local-space inertia tensor about the center of mass.</summary>
    public Mat3 Inertia { get; protected set; }
    public Mat3 IInertia { get; protected set; }
    public bool Locked { get; set; }
    public ObjectShader Shader { get; set; }
    public bool CanRotate { get; internal set; }
    public float Friction { get; set; } = 0.5f;
    public HashSet<PhysicsObject> ConnectedObjects { get; } = new();
    public List<Constraint> Constraints { get; } = new();

    public event Action<PhysicsObject, (Vector3, Vector3)>? ContactPointAdded;
    public event Action<PhysicsObject, (Vector3, Vector3)>? ContactPointRemoved;

    // Keyed by the other object; value is (point, normal).
    private readonly Dictionary<PhysicsObject, (Vector3, Vector3)> _contactPoints = new();
    private readonly Dictionary<PhysicsObject, (Vector3, Vector3)> _previousContactPoints = new();

    // Is in the process of being destroyed, will not collide
    internal bool IsDestroyed;

    internal float EffectiveIMass => Locked ? 0f : IMass;

    // TODO(3D): sleep/wake and support tracking are disabled until 3D contact handling is stable.
    // Re-enable by restoring these members and the calls in Update/AddContact/FinalizeContacts and PhysicsSystem.
    /*
    private static float SupportObjectNormalThreshold = 0.2f;
    public bool CanSleep = true;
    public bool Sleeping { get; private set; } = false;
    private float _sleepTimer = 0f;
    private Vector3 _prevCenter;
    private readonly HashSet<PhysicsObject> _supportingObjects = new();
    private readonly HashSet<PhysicsObject> _supportedObjects = new();
    private readonly Dictionary<PhysicsObject, (Vector3, Vector3)> _sleepingContactPoints = new();
    private bool _validateSupportsNextFrame = false;

    public void UpdateSleepState(float dt)
    {
        if (Locked)
        {
            _sleepTimer = 0f;
            return;
        }

        float displacement = (Center - _prevCenter).Length();
        if (displacement < PhysicsSystem.LinearSleepThreshold &&
            AngularVelocity.Length() < PhysicsSystem.AngularSleepThreshold)
        {
            _sleepTimer += dt;
            if (_sleepTimer >= PhysicsSystem.SleepTimeThreshold)
                Sleep();
        }
        else
        {
            _sleepTimer = 0f;
            if (Sleeping)
                Wake();
        }
    }

    private void ValidateSupports()
    {
        if (!Sleeping)
            return;

        bool shouldWake = _supportingObjects.Count == 0;
        foreach (var supportingObj in _supportingObjects)
        {
            if (!_sleepingContactPoints.TryGetValue(supportingObj, out var contactData) ||
                !supportingObj.Contains(contactData.Item1))
            {
                shouldWake = true;
                break;
            }
        }

        if (shouldWake)
            Wake();
    }

    public void Sleep()
    {
        if (Sleeping || !CanSleep) return;
        Sleeping = true;
        Velocity = Vector3.Zero;
        AngularVelocity = Vector3.Zero;

        _sleepingContactPoints.Clear();
        foreach (var contact in _previousContactPoints)
            _sleepingContactPoints[contact.Key] = contact.Value;
    }

    public void Wake()
    {
        if (!Sleeping) return;
        Sleeping = false;
        _sleepTimer = 0f;
        _sleepingContactPoints.Clear();

        foreach (var supported in _supportedObjects)
        {
            if (supported.Sleeping)
                supported.Wake();
        }
    }
    */

    public PhysicsObject(IShape shape, Vector3 center, float restitution, bool locked, ObjectShader shader, float mass = 0, bool canRotate = false, uint collisionMask = 0xFFFFFFFF)
    {
        Shape = shape;
        Center = center;
        Restitution = restitution;
        Locked = locked;
        Shader = shader;
        Mass = (mass == 0) ? shape.GetVolume() : mass;
        IMass = 1 / Mass;
        Inertia = shape.GetInertiaTensor(Mass);
        IInertia = Inertia.Inverse();
        CanRotate = canRotate;
        CollisionMask = collisionMask;
        Aabb = Shape.GetAABB(Center, Orientation);
    }

    public void Update(float dt)
    {
        if (IsDestroyed)
            return;

        Move(dt);
        UpdateRotation(dt);
    }

    /// <summary>
    /// Applies the world-space inverse inertia tensor. Returns zero for locked or non-rotating objects.
    /// </summary>
    public Vector3 ApplyInverseInertia(Vector3 worldVector)
    {
        if (Locked || !CanRotate)
            return Vector3.Zero;

        return PhysMath.Rotate(IInertia * PhysMath.InverseRotate(worldVector, Orientation), Orientation);
    }

    /// <summary>
    /// Inverse effective mass seen by an impulse along <paramref name="direction"/> applied at lever arm <paramref name="r"/>.
    /// </summary>
    internal float InverseMassAlong(Vector3 r, Vector3 direction)
    {
        Vector3 rxn = Vector3.Cross(r, direction);
        return EffectiveIMass + Vector3.Dot(rxn, ApplyInverseInertia(rxn));
    }

    public Vector3 GetVelocityAtPoint(Vector3 worldPoint)
    {
        return CanRotate
            ? Velocity + Vector3.Cross(AngularVelocity, worldPoint - Center)
            : Velocity;
    }

    public Vector3 LocalToWorld(Vector3 localPoint) => Center + PhysMath.Rotate(localPoint, Orientation);

    /// <summary>
    /// Applies an impulse at lever arm <paramref name="r"/> (world-space offset from the center).
    /// </summary>
    public void ApplyImpulse(Vector3 impulse, Vector3 r)
    {
        if (Locked)
            return;

        Velocity += impulse * IMass;
        AngularVelocity += ApplyInverseInertia(Vector3.Cross(r, impulse));
    }

    public void ApplyImpulseAtPoint(Vector3 impulse, Vector3 worldPoint) => ApplyImpulse(impulse, worldPoint - Center);

    public void ApplyAngularImpulse(Vector3 angularImpulse)
    {
        AngularVelocity += ApplyInverseInertia(angularImpulse);
    }

    public void AddContact(PhysicsObject obj, Vector3 point, Vector3 normal)
    {
        _contactPoints.TryAdd(obj, (point, normal));
    }

    /// <summary>
    /// Retrieve the most recent contact points.
    /// </summary>
    public Dictionary<PhysicsObject, (Vector3, Vector3)> GetContacts() => _previousContactPoints;

    /// <summary>
    /// Fires added/removed events against the previous frame's contacts, then rolls the cache forward.
    /// </summary>
    public void FinalizeContacts()
    {
        if (_contactPoints.Count == 0 && _previousContactPoints.Count == 0)
            return;

        var addedHandler = ContactPointAdded;
        var removedHandler = ContactPointRemoved;

        foreach (var kv in _contactPoints)
        {
            if (!_previousContactPoints.ContainsKey(kv.Key))
                addedHandler?.Invoke(kv.Key, kv.Value);
        }

        foreach (var kv in _previousContactPoints)
        {
            if (!_contactPoints.ContainsKey(kv.Key))
                removedHandler?.Invoke(kv.Key, kv.Value);
        }

        _previousContactPoints.Clear();
        foreach (var kv in _contactPoints)
            _previousContactPoints.Add(kv.Key, kv.Value);

        _contactPoints.Clear();
    }

    public virtual void Move(float dt)
    {
        if (Locked)
            return;

        RoundSpeedToZero();
        Center += Velocity * dt;
        Aabb = Shape.GetAABB(Center, Orientation);
    }

    protected void RoundSpeedToZero()
    {
        if (Math.Abs(Velocity.X) + Math.Abs(Velocity.Y) + Math.Abs(Velocity.Z) < 0.01f)
            Velocity = Vector3.Zero;
    }

    /// <summary>
    /// Directly translates the object by a given vector.
    /// </summary>
    public virtual void Move(Vector3 dVector)
    {
        if (Locked)
            return;

        Center += dVector;
        Aabb = Shape.GetAABB(Center, Orientation);
    }

    /// <summary>
    /// Applies a world-space rotation on top of the current orientation.
    /// </summary>
    public void RotateBy(Quaternion rotation)
    {
        if (Locked || !CanRotate)
            return;

        Orientation = Quaternion.Normalize(Quaternion.Concatenate(Orientation, rotation));
    }

    public virtual void UpdateRotation(float dt)
    {
        if (!CanRotate || Locked)
            return;

        Orientation = PhysMath.IntegrateOrientation(Orientation, AngularVelocity, dt);
        AngularVelocity *= 0.999f;
        if (AngularVelocity.LengthSquared() < 1e-6f)
            AngularVelocity = Vector3.Zero;

        Aabb = Shape.GetAABB(Center, Orientation);
    }

    public bool Contains(Vector3 point) => Shape.Contains(point, Center, Orientation);
}
