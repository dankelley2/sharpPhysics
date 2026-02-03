using System;
using System.Numerics;
using SharpPhysics.Engine.Objects;
using SharpPhysics.Engine.Helpers;

namespace SharpPhysics.Engine.Constraints
{
    /// <summary>
    /// An AxisConstraint (revolute joint) pins two objects together at specified local anchor points,
    /// allowing free rotation around the anchor point.
    /// Uses Sequential Impulse with Baumgarte stabilization.
    /// Solves X and Y constraints independently for proper 2D point-to-point behavior.
    /// </summary>
    public class AxisConstraint : Constraint
    {
        // Baumgarte stabilization factor - keep low to avoid over-correction
        private const float BaumgarteFactor = 0.05f;
        // Maximum bias velocity to prevent explosive corrections
        private const float MaxBiasVelocity = 300f;
        // Slop - ignore errors smaller than this (prevents jitter)
        private const float LinearSlop = 0.03f;

        public AxisConstraint(PhysicsObject a, PhysicsObject b, Vector2 anchorA, Vector2 anchorB)
            : base(a, b)
        {
            AnchorA = anchorA;
            AnchorB = anchorB;
        }

        public override void ApplyConstraint(float dt)
        {
            // Compute world-space anchor positions using lever arms
            Vector2 rA = PhysMath.RotateVector(AnchorA, A.Angle);
            Vector2 rB = PhysMath.RotateVector(AnchorB, B.Angle);
            Vector2 worldAnchorA = A.Center + rA;
            Vector2 worldAnchorB = B.Center + rB;

            // Compute position error
            Vector2 error = worldAnchorB - worldAnchorA;

            // Get effective inverse masses (0 if locked)
            float invMassA = A.Locked ? 0f : A.IMass;
            float invMassB = B.Locked ? 0f : B.IMass;
            float invInertiaA = (A.Locked || !A.CanRotate) ? 0f : A.IInertia;
            float invInertiaB = (B.Locked || !B.CanRotate) ? 0f : B.IInertia;

            // Compute velocity at anchor points: v_anchor = v_center + ω × r
            Vector2 vA = A.Velocity + PhysMath.Perpendicular(rA) * A.AngularVelocity;
            Vector2 vB = B.Velocity + PhysMath.Perpendicular(rB) * B.AngularVelocity;
            Vector2 relVel = vB - vA;

            // Solve constraint in X and Y directions separately
            // This properly constrains both degrees of freedom for a point-to-point joint
            ApplyAxisImpulse(Vector2.UnitX, rA, rB, relVel.X, error.X, invMassA, invMassB, invInertiaA, invInertiaB, dt);
            ApplyAxisImpulse(Vector2.UnitY, rA, rB, relVel.Y, error.Y, invMassA, invMassB, invInertiaA, invInertiaB, dt);
        }

        private void ApplyAxisImpulse(Vector2 axis, Vector2 rA, Vector2 rB, float velError, float posError,
            float invMassA, float invMassB, float invInertiaA, float invInertiaB, float dt)
        {
            // Skip tiny errors to prevent jitter
            if (MathF.Abs(posError) < LinearSlop)
                return;

            // Effective mass for this axis: K = mA^-1 + mB^-1 + (rA × axis)² * IA^-1 + (rB × axis)² * IB^-1
            float rAxN = PhysMath.Cross(rA, axis);
            float rBxN = PhysMath.Cross(rB, axis);
            float effectiveMass = invMassA + invMassB +
                                  (rAxN * rAxN) * invInertiaA +
                                  (rBxN * rBxN) * invInertiaB;

            if (effectiveMass < 1e-10f)
                return;

            // Baumgarte bias - clamped to prevent explosion with small dt
            float bias = BaumgarteFactor * posError / dt;
            bias = Math.Clamp(bias, -MaxBiasVelocity, MaxBiasVelocity);

            // Impulse magnitude along this axis
            float impulseMag = -(velError + bias) / effectiveMass;
            Vector2 impulse = axis * impulseMag;

            // Apply impulse
            if (!A.Locked)
            {
                A.Velocity -= impulse * invMassA;
                if (A.CanRotate)
                    A.AngularVelocity -= PhysMath.Cross(rA, impulse) * invInertiaA;
            }
            if (!B.Locked)
            {
                B.Velocity += impulse * invMassB;
                if (B.CanRotate)
                    B.AngularVelocity += PhysMath.Cross(rB, impulse) * invInertiaB;
            }
        }
    }
}
