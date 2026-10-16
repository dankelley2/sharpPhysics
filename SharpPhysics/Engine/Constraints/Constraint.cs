#nullable enable
using System.Numerics;
using SharpPhysics.Engine.Helpers;
using SharpPhysics.Engine.Objects;

namespace SharpPhysics.Engine.Constraints;

/// <summary>
/// Base constraint type. Constraints link two PhysicsObjects.
/// </summary>
public abstract class Constraint
{
    public PhysicsObject A { get; set; }
    public PhysicsObject B { get; set; }

    // Anchor points in local space of each object
    public Vector3 AnchorA { get; set; }
    public Vector3 AnchorB { get; set; }

    /// <summary>
    /// B's orientation relative to A at creation time (conj(A) * B).
    /// </summary>
    public Quaternion InitialRelativeOrientation { get; set; }

    // Arbitrary break point when the constraint is pushed past its limits
    public bool CanBreak { get; set; } = false;
    public bool IsBroken { get; protected set; } = false;

    protected Constraint(PhysicsObject a, PhysicsObject b)
    {
        A = a;
        B = b;

        InitialRelativeOrientation = Quaternion.Conjugate(a.Orientation) * b.Orientation;

        // Mark the objects as connected so that collisions between them are ignored.
        A.ConnectedObjects.Add(B);
        B.ConnectedObjects.Add(A);

        A.Constraints.Add(this);
        B.Constraints.Add(this);
    }

    public abstract void ApplyConstraint(float dt);

    protected Vector3 RelativeVelocity(Vector3 rA, Vector3 rB)
    {
        Vector3 vA = A.Velocity + Vector3.Cross(A.AngularVelocity, rA);
        Vector3 vB = B.Velocity + Vector3.Cross(B.AngularVelocity, rB);
        return vB - vA;
    }

    protected float InverseMassAlong(Vector3 axis, Vector3 rA, Vector3 rB)
    {
        return A.InverseMassAlong(rA, axis) + B.InverseMassAlong(rB, axis);
    }

    protected float AngularInverseMassAlong(Vector3 axis)
    {
        return Vector3.Dot(axis, A.ApplyInverseInertia(axis)) + Vector3.Dot(axis, B.ApplyInverseInertia(axis));
    }

    /// <summary>
    /// Applies <paramref name="impulse"/> to B and its opposite to A.
    /// </summary>
    protected void ApplyLinearImpulse(Vector3 impulse, Vector3 rA, Vector3 rB)
    {
        A.ApplyImpulse(-impulse, rA);
        B.ApplyImpulse(impulse, rB);
    }

    /// <summary>
    /// Applies <paramref name="impulse"/> to B and its opposite to A.
    /// </summary>
    protected void ApplyAngularImpulse(Vector3 impulse)
    {
        A.ApplyAngularImpulse(-impulse);
        B.ApplyAngularImpulse(impulse);
    }

    /// <summary>
    /// World-space rotation vector (axis * angle) from B's target orientation to its current orientation.
    /// </summary>
    protected Vector3 RelativeOrientationError()
    {
        Quaternion target = A.Orientation * InitialRelativeOrientation;
        return PhysMath.ToRotationVector(B.Orientation * Quaternion.Conjugate(target));
    }
}
