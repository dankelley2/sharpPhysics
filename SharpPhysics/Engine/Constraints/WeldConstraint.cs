using System;
using System.Numerics;
using SharpPhysics.Engine.Objects;
using SharpPhysics.Engine.Helpers;

namespace SharpPhysics.Engine.Constraints
{
    /// <summary>
    /// A weld constraint holds two objects together so that the world-space positions
    /// of their respective local anchors remain coincident, and their relative angle remains constant.
    /// Uses impulse-based constraint solving with Baumgarte stabilization.
    /// </summary>
    public class WeldConstraint : Constraint
    {
        public float InitialRelativeAngle { get; private set; }

        private const float BaumgarteBias = 0.23f;
        private const float AngularBias = 0.20f;
        private const float MaxBiasVelocity = 400f;

        public WeldConstraint(PhysicsObject a, PhysicsObject b, Vector2 anchorA, Vector2 anchorB, bool canBreak = false)
            : base(a, b)
        {
            AnchorA = anchorA;
            AnchorB = anchorB;
            InitialRelativeAngle = b.Angle - a.Angle;
            CanBreak = canBreak;
        }

        public override void ApplyConstraint(float dt)
        {
            if (IsBroken) return;

            Vector2 rA = PhysMath.RotateVector(AnchorA, A.Angle);
            Vector2 rB = PhysMath.RotateVector(AnchorB, B.Angle);
            Vector2 worldAnchorA = A.Center + rA;
            Vector2 worldAnchorB = B.Center + rB;

            Vector2 posError = worldAnchorB - worldAnchorA;
            float angleError = (B.Angle - A.Angle) - InitialRelativeAngle;

            if (CanBreak && (posError.LengthSquared() > 30f || Math.Abs(angleError) > 0.5f))
            {
                IsBroken = true;
                return;
            }

            while (angleError > MathF.PI) angleError -= 2 * MathF.PI;
            while (angleError < -MathF.PI) angleError += 2 * MathF.PI;

            float invMassA = A.Locked ? 0f : A.IMass;
            float invMassB = B.Locked ? 0f : B.IMass;
            float invInertiaA = (A.Locked || !A.CanRotate) ? 0f : A.IInertia;
            float invInertiaB = (B.Locked || !B.CanRotate) ? 0f : B.IInertia;

            Vector2 vA = A.Velocity + PhysMath.Perpendicular(rA) * A.AngularVelocity;
            Vector2 vB = B.Velocity + PhysMath.Perpendicular(rB) * B.AngularVelocity;
            Vector2 relVel = vB - vA;

            ApplyLinearImpulse(Vector2.UnitX, rA, rB, relVel.X, posError.X, invMassA, invMassB, invInertiaA, invInertiaB, dt);
            ApplyLinearImpulse(Vector2.UnitY, rA, rB, relVel.Y, posError.Y, invMassA, invMassB, invInertiaA, invInertiaB, dt);

            float angularEffectiveMass = invInertiaA + invInertiaB;
            if (angularEffectiveMass < 1e-10f) return;

            float relAngVel = B.AngularVelocity - A.AngularVelocity;
            float angularBias = Math.Clamp((AngularBias / dt) * angleError, -MaxBiasVelocity, MaxBiasVelocity);
            float angularImpulse = -(relAngVel + angularBias) / angularEffectiveMass;

            if (!A.Locked && A.CanRotate)
                A.AngularVelocity -= angularImpulse * invInertiaA;
            if (!B.Locked && B.CanRotate)
                B.AngularVelocity += angularImpulse * invInertiaB;
        }

        private void ApplyLinearImpulse(Vector2 axis, Vector2 rA, Vector2 rB, float velError, float posError,
            float invMassA, float invMassB, float invInertiaA, float invInertiaB, float dt)
        {
            float rAxN = PhysMath.Cross(rA, axis);
            float rBxN = PhysMath.Cross(rB, axis);
            float effectiveMass = invMassA + invMassB + (rAxN * rAxN) * invInertiaA + (rBxN * rBxN) * invInertiaB;

            if (effectiveMass < 1e-10f) return;

            float bias = Math.Clamp(BaumgarteBias * posError / dt, -MaxBiasVelocity, MaxBiasVelocity);
            float impulseMag = -(velError + bias) / effectiveMass;
            Vector2 impulse = axis * impulseMag;

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
