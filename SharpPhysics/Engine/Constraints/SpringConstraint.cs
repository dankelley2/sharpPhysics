using System;
using System.Numerics;
using SharpPhysics.Engine.Objects;
using SharpPhysics.Engine.Helpers;

namespace SharpPhysics.Engine.Constraints
{
    /// <summary>
    /// A linear spring constraint that applies force along the axis between two anchor points.
    /// Behaves like a suspension spring - stiff, symmetric push/pull to maintain rest length.
    /// Also maintains axis alignment to prevent "folding" behavior.
    /// Uses Box2D's soft constraint formulas for stable, tunable spring behavior.
    /// </summary>
    public class SpringConstraint : Constraint
    {
        /// <summary>
        /// Natural frequency in Hz. Controls spring stiffness.
        /// Higher values = stiffer spring, faster response.
        /// Typical values: 5-10 Hz (soft), 10-20 Hz (medium), 20-40 Hz (stiff suspension)
        /// </summary>
        public float Frequency { get; set; } = 1.0f;

        /// <summary>
        /// Damping ratio. Controls oscillation decay.
        /// 0.0 = no damping (perpetual bouncing)
        /// 0.5 = under-damped (some bounce)
        /// 0.7-0.8 = typical suspension (slight bounce, quick settle)
        /// 1.0 = critically damped (no overshoot)
        /// </summary>
        public float DampingRatio { get; set; } = 0.6f;

        /// <summary>
        /// Angular frequency in Hz for the orientation constraint.
        /// Controls how stiffly the spring resists bending/folding.
        /// Set to 0 to disable angular constraint (pure distance spring).
        /// Default is 0 for true suspension-like behavior.
        /// </summary>
        public float AngularFrequency { get; set; } = 0f;

        /// <summary>
        /// Angular damping ratio for the orientation constraint.
        /// </summary>
        public float AngularDampingRatio { get; set; } = 0.5f;

        /// <summary>
        /// Rest length of the spring. Spring pushes/pulls to maintain this distance.
        /// Set to 0 for anchor points that should stay together (like a stiff joint).
        /// </summary>
        public float RestLength { get; set; }

        /// <summary>
        /// Minimum length the spring can compress to (0 = no limit).
        /// </summary>
        public float MinLength { get; set; } = 0f;

        /// <summary>
        /// Maximum length the spring can extend to (0 = no limit).
        /// </summary>
        public float MaxLength { get; set; } = 0f;

        /// <summary>
        /// The initial angle of the spring axis itself.
        /// </summary>
        private readonly float _initialSpringAngle;

        /// <summary>
        /// The initial relative angle between A and B (B.Angle - A.Angle).
        /// Used for coupled angular constraint.
        /// </summary>
        private readonly float _initialRelativeAngle;

        public SpringConstraint(PhysicsObject a, PhysicsObject b, Vector2 anchorA, Vector2 anchorB, bool canBreak = false)
            : base(a, b)
        {
            AnchorA = anchorA;
            AnchorB = anchorB;
            CanBreak = canBreak;

            Frequency = Math.Min(Math.Max(1f,(a.IMass + b.IMass) / 2 * 5000),50f);

            // Compute initial rest length from anchor positions
            Vector2 worldAnchorA = a.Center + PhysMath.RotateVector(anchorA, a.Angle);
            Vector2 worldAnchorB = b.Center + PhysMath.RotateVector(anchorB, b.Angle);
            RestLength = Vector2.Distance(worldAnchorA, worldAnchorB);

            // Compute initial spring axis angle
            Vector2 delta = worldAnchorB - worldAnchorA;
            _initialSpringAngle = MathF.Atan2(delta.Y, delta.X);

            // Store initial relative angle between A and B (for coupled constraint)
            _initialRelativeAngle = b.Angle - a.Angle;
        }

        public override void ApplyConstraint(float dt)
        {
            if (IsBroken) return;

            // Get lever arms in world space
            Vector2 rA = PhysMath.RotateVector(AnchorA, A.Angle);
            Vector2 rB = PhysMath.RotateVector(AnchorB, B.Angle);
            Vector2 worldAnchorA = A.Center + rA;
            Vector2 worldAnchorB = B.Center + rB;

            // Compute spring axis and current length
            Vector2 delta = worldAnchorB - worldAnchorA;
            float currentLength = delta.Length();

            // Current spring axis angle
            float currentSpringAngle = MathF.Atan2(delta.Y, delta.X);

            // For zero-length springs, use a fallback direction
            Vector2 axis;
            float posError;

            if (currentLength < 1e-6f)
            {
                if (RestLength < 1e-6f)
                {
                    // Both at rest and zero length - nothing to do for linear
                    axis = new Vector2(MathF.Cos(_initialSpringAngle), MathF.Sin(_initialSpringAngle));
                    posError = 0f;
                }
                else
                {
                    // Should have length but collapsed - use initial direction
                    axis = new Vector2(MathF.Cos(_initialSpringAngle), MathF.Sin(_initialSpringAngle));
                    posError = -RestLength; // Fully compressed
                }
            }
            else
            {
                axis = delta / currentLength;

                // Position error: positive = stretched beyond rest, negative = compressed
                float targetLength = RestLength;
                if (MinLength > 0f && currentLength < MinLength)
                    targetLength = MinLength;
                else if (MaxLength > 0f && currentLength > MaxLength)
                    targetLength = MaxLength;

                posError = currentLength - targetLength;
            }

            // Break check
            if (CanBreak && MathF.Abs(posError) > 80f)
            {
                IsBroken = true;
                return;
            }

            // Get effective inverse masses
            float invMassA = A.Locked ? 0f : A.IMass;
            float invMassB = B.Locked ? 0f : B.IMass;
            float invInertiaA = (A.Locked || !A.CanRotate) ? 0f : A.IInertia;
            float invInertiaB = (B.Locked || !B.CanRotate) ? 0f : B.IInertia;

            // Apply linear (distance) constraint
            ApplyLinearConstraint(axis, rA, rB, posError, invMassA, invMassB, invInertiaA, invInertiaB, dt);

            // Apply angular (orientation) constraint to prevent folding
            if (AngularFrequency > 0f)
            {
                ApplyAngularConstraint(currentSpringAngle, invInertiaA, invInertiaB, dt);
            }
        }

        private void ApplyLinearConstraint(Vector2 axis, Vector2 rA, Vector2 rB, float posError,
            float invMassA, float invMassB, float invInertiaA, float invInertiaB, float dt)
        {
            // Compute velocity at anchor points
            Vector2 vA = A.Velocity + PhysMath.Perpendicular(rA) * A.AngularVelocity;
            Vector2 vB = B.Velocity + PhysMath.Perpendicular(rB) * B.AngularVelocity;

            // Relative velocity along the spring axis
            float velError = Vector2.Dot(vB - vA, axis);

            // Compute K (effective mass inverse) along the spring axis
            float rAxN = PhysMath.Cross(rA, axis);
            float rBxN = PhysMath.Cross(rB, axis);
            float K = invMassA + invMassB +
                      (rAxN * rAxN) * invInertiaA +
                      (rBxN * rBxN) * invInertiaB;

            if (K < 1e-10f) return;

            // Effective mass seen by the constraint
            float effectiveMass = 1.0f / K;

            // Box2D soft constraint parameters
            float omega = 2.0f * MathF.PI * Frequency;
            float springK = effectiveMass * omega * omega;
            float dampingC = 2.0f * effectiveMass * DampingRatio * omega;

            float d = dampingC + dt * springK;
            if (d < 1e-10f) return;

            float gamma = 1.0f / (dt * d);
            float beta = dt * springK / d;

            // Bias velocity from position error
            float bias = (beta / dt) * posError;

            // Soft constraint impulse
            float impulseMag = -(velError + bias) / (K + gamma);
            Vector2 impulse = axis * impulseMag;

            // Apply impulse to bodies
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

        private void ApplyAngularConstraint(float currentSpringAngle, float invInertiaA, float invInertiaB, float dt)
        {
            // Use COUPLED angular constraint like WeldConstraint
            // This maintains the relative angle between A and B, not absolute angles
            float angularEffectiveMassInv = invInertiaA + invInertiaB;
            if (angularEffectiveMassInv < 1e-10f) return;

            // Relative angle error (same as WeldConstraint)
            float angleError = (B.Angle - A.Angle) - _initialRelativeAngle;
            angleError = NormalizeAngle(angleError);

            // Relative angular velocity
            float relAngVel = B.AngularVelocity - A.AngularVelocity;

            // Effective mass for coupled constraint
            float effectiveMass = 1.0f / angularEffectiveMassInv;

            // Soft constraint parameters
            float omega = 2.0f * MathF.PI * AngularFrequency;
            float springK = effectiveMass * omega * omega;
            float dampingC = 2.0f * effectiveMass * AngularDampingRatio * omega;

            float d = dampingC + dt * springK;
            if (d < 1e-10f) return;

            float gamma = 1.0f / (dt * d);
            float beta = dt * springK / d;

            // Bias from angle error
            float bias = (beta / dt) * angleError;

            // Coupled impulse (like WeldConstraint but with soft constraint math)
            float angularImpulse = -(relAngVel + bias) / (angularEffectiveMassInv + gamma);

            // Apply OPPOSITE impulses to couple the bodies together
            if (!A.Locked && A.CanRotate)
                A.AngularVelocity -= angularImpulse * invInertiaA;
            if (!B.Locked && B.CanRotate)
                B.AngularVelocity += angularImpulse * invInertiaB;
        }

        private static float NormalizeAngle(float angle)
        {
            while (angle > MathF.PI) angle -= 2f * MathF.PI;
            while (angle < -MathF.PI) angle += 2f * MathF.PI;
            return angle;
        }
    }
}
