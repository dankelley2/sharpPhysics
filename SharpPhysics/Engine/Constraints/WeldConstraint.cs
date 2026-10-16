#nullable enable
using System;
using System.Numerics;
using SharpPhysics.Engine.Helpers;
using SharpPhysics.Engine.Objects;

namespace SharpPhysics.Engine.Constraints;

/// <summary>
/// Holds two objects together so their anchors stay coincident and their relative orientation stays fixed.
/// Uses impulse-based constraint solving with Baumgarte stabilization.
/// </summary>
public class WeldConstraint : Constraint
{
    private const float BaumgarteBias = 0.23f;
    private const float AngularBias = 0.20f;
    private const float MaxBiasVelocity = 400f;

    private static readonly Vector3[] Axes = { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ };

    public WeldConstraint(PhysicsObject a, PhysicsObject b, Vector3 anchorA, Vector3 anchorB, bool canBreak = false)
        : base(a, b)
    {
        AnchorA = anchorA;
        AnchorB = anchorB;
        CanBreak = canBreak;
    }

    public override void ApplyConstraint(float dt)
    {
        if (IsBroken) return;

        Vector3 rA = PhysMath.Rotate(AnchorA, A.Orientation);
        Vector3 rB = PhysMath.Rotate(AnchorB, B.Orientation);
        Vector3 posError = (B.Center + rB) - (A.Center + rA);
        Vector3 angleError = RelativeOrientationError();

        if (CanBreak && (posError.LengthSquared() > 30f || angleError.Length() > 0.5f))
        {
            IsBroken = true;
            return;
        }

        Vector3 relVel = RelativeVelocity(rA, rB);
        foreach (var axis in Axes)
        {
            float k = InverseMassAlong(axis, rA, rB);
            if (k < 1e-10f) continue;

            float bias = Math.Clamp(BaumgarteBias * Vector3.Dot(posError, axis) / dt, -MaxBiasVelocity, MaxBiasVelocity);
            float impulseMag = -(Vector3.Dot(relVel, axis) + bias) / k;
            ApplyLinearImpulse(axis * impulseMag, rA, rB);
        }

        foreach (var axis in Axes)
        {
            float k = AngularInverseMassAlong(axis);
            if (k < 1e-10f) continue;

            float relAngVel = Vector3.Dot(B.AngularVelocity - A.AngularVelocity, axis);
            float bias = Math.Clamp(AngularBias / dt * Vector3.Dot(angleError, axis), -MaxBiasVelocity, MaxBiasVelocity);
            float impulseMag = -(relAngVel + bias) / k;
            ApplyAngularImpulse(axis * impulseMag);
        }
    }
}
