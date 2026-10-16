#nullable enable
using System;
using System.Numerics;
using SharpPhysics.Engine.Helpers;
using SharpPhysics.Engine.Objects;

namespace SharpPhysics.Engine.Constraints;

/// <summary>
/// Pins two objects together at local anchor points while allowing free rotation about the anchor
/// (a ball-and-socket joint in 3D). Uses sequential impulses with Baumgarte stabilization.
/// </summary>
public class AxisConstraint : Constraint
{
    // Baumgarte stabilization factor - keep low to avoid over-correction
    private const float BaumgarteFactor = 0.05f;
    // Maximum bias velocity to prevent explosive corrections
    private const float MaxBiasVelocity = 300f;
    // Slop - ignore errors smaller than this (prevents jitter)
    private const float LinearSlop = 0.03f;

    public AxisConstraint(PhysicsObject a, PhysicsObject b, Vector3 anchorA, Vector3 anchorB)
        : base(a, b)
    {
        AnchorA = anchorA;
        AnchorB = anchorB;
    }

    public override void ApplyConstraint(float dt)
    {
        Vector3 rA = PhysMath.Rotate(AnchorA, A.Orientation);
        Vector3 rB = PhysMath.Rotate(AnchorB, B.Orientation);
        Vector3 error = (B.Center + rB) - (A.Center + rA);
        Vector3 relVel = RelativeVelocity(rA, rB);

        ApplyAxisImpulse(Vector3.UnitX, rA, rB, relVel.X, error.X, dt);
        ApplyAxisImpulse(Vector3.UnitY, rA, rB, relVel.Y, error.Y, dt);
        ApplyAxisImpulse(Vector3.UnitZ, rA, rB, relVel.Z, error.Z, dt);
    }

    private void ApplyAxisImpulse(Vector3 axis, Vector3 rA, Vector3 rB, float velError, float posError, float dt)
    {
        if (MathF.Abs(posError) < LinearSlop)
            return;

        float k = InverseMassAlong(axis, rA, rB);
        if (k < 1e-10f)
            return;

        float bias = Math.Clamp(BaumgarteFactor * posError / dt, -MaxBiasVelocity, MaxBiasVelocity);
        float impulseMag = -(velError + bias) / k;
        ApplyLinearImpulse(axis * impulseMag, rA, rB);
    }
}
