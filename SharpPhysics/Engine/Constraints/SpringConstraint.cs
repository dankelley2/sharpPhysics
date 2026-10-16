#nullable enable
using System;
using System.Numerics;
using SharpPhysics.Engine.Helpers;
using SharpPhysics.Engine.Objects;

namespace SharpPhysics.Engine.Constraints;

/// <summary>
/// A linear spring that pushes/pulls along the axis between two anchor points to maintain its rest length.
/// Optionally resists changes in relative orientation. Uses Box2D-style soft constraint formulas.
/// </summary>
public class SpringConstraint : Constraint
{
    /// <summary>
    /// Natural frequency in Hz. Higher values = stiffer spring.
    /// Typical values: 5-10 Hz (soft), 10-20 Hz (medium), 20-40 Hz (stiff suspension)
    /// </summary>
    public float Frequency { get; set; } = 1.0f;

    /// <summary>
    /// Damping ratio. 0 = no damping, 1 = critically damped.
    /// </summary>
    public float DampingRatio { get; set; } = 0.6f;

    /// <summary>
    /// Angular frequency in Hz for the orientation constraint. 0 disables it (pure distance spring).
    /// </summary>
    public float AngularFrequency { get; set; } = 0f;

    public float AngularDampingRatio { get; set; } = 0.5f;

    /// <summary>
    /// Rest length of the spring. Set to 0 for anchor points that should stay together.
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

    // Fallback direction when the anchors coincide.
    private readonly Vector3 _initialAxis;

    public SpringConstraint(PhysicsObject a, PhysicsObject b, Vector3 anchorA, Vector3 anchorB, bool canBreak = false)
        : base(a, b)
    {
        AnchorA = anchorA;
        AnchorB = anchorB;
        CanBreak = canBreak;

        // Mass is volume (L^3) in pixels; IMass^(2/3) keeps the original area-based (L^2) tuning.
        float areaScaleIMassA = MathF.Pow(a.IMass, 2f / 3f);
        float areaScaleIMassB = MathF.Pow(b.IMass, 2f / 3f);
        Frequency = Math.Min(Math.Max(1f, (areaScaleIMassA + areaScaleIMassB) / 2 * 5000), 50f);

        Vector3 delta = b.LocalToWorld(anchorB) - a.LocalToWorld(anchorA);
        RestLength = delta.Length();
        _initialAxis = RestLength > 1e-6f ? delta / RestLength : Vector3.UnitX;
    }

    public override void ApplyConstraint(float dt)
    {
        if (IsBroken) return;

        Vector3 rA = PhysMath.Rotate(AnchorA, A.Orientation);
        Vector3 rB = PhysMath.Rotate(AnchorB, B.Orientation);
        Vector3 delta = (B.Center + rB) - (A.Center + rA);
        float currentLength = delta.Length();

        Vector3 axis;
        float posError;

        if (currentLength < 1e-6f)
        {
            axis = _initialAxis;
            posError = -RestLength;
        }
        else
        {
            axis = delta / currentLength;

            float targetLength = RestLength;
            if (MinLength > 0f && currentLength < MinLength)
                targetLength = MinLength;
            else if (MaxLength > 0f && currentLength > MaxLength)
                targetLength = MaxLength;

            // Positive = stretched, negative = compressed
            posError = currentLength - targetLength;
        }

        if (CanBreak && MathF.Abs(posError) > 80f)
        {
            IsBroken = true;
            return;
        }

        ApplyLinearConstraint(axis, rA, rB, posError, dt);

        if (AngularFrequency > 0f)
        {
            ApplyAngularConstraint(dt);
        }
    }

    private void ApplyLinearConstraint(Vector3 axis, Vector3 rA, Vector3 rB, float posError, float dt)
    {
        float velError = Vector3.Dot(RelativeVelocity(rA, rB), axis);

        float k = InverseMassAlong(axis, rA, rB);
        if (k < 1e-10f) return;

        if (!TrySoftConstraint(1f / k, Frequency, DampingRatio, dt, out float gamma, out float beta))
            return;

        float bias = (beta / dt) * posError;
        float impulseMag = -(velError + bias) / (k + gamma);
        ApplyLinearImpulse(axis * impulseMag, rA, rB);
    }

    private void ApplyAngularConstraint(float dt)
    {
        Vector3 error = RelativeOrientationError();
        float angle = error.Length();
        if (angle < 1e-6f) return;

        Vector3 axis = error / angle;
        float k = AngularInverseMassAlong(axis);
        if (k < 1e-10f) return;

        if (!TrySoftConstraint(1f / k, AngularFrequency, AngularDampingRatio, dt, out float gamma, out float beta))
            return;

        float relAngVel = Vector3.Dot(B.AngularVelocity - A.AngularVelocity, axis);
        float bias = (beta / dt) * angle;
        float impulseMag = -(relAngVel + bias) / (k + gamma);
        ApplyAngularImpulse(axis * impulseMag);
    }

    private static bool TrySoftConstraint(float effectiveMass, float frequency, float dampingRatio, float dt, out float gamma, out float beta)
    {
        float omega = 2.0f * MathF.PI * frequency;
        float springK = effectiveMass * omega * omega;
        float dampingC = 2.0f * effectiveMass * dampingRatio * omega;

        float d = dampingC + dt * springK;
        if (d < 1e-10f)
        {
            gamma = beta = 0f;
            return false;
        }

        gamma = 1.0f / (dt * d);
        beta = dt * springK / d;
        return true;
    }
}
