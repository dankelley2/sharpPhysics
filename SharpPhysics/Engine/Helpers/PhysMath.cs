#nullable enable
using System;
using System.Numerics;

namespace SharpPhysics.Engine.Helpers;

public static class PhysMath
{
    public static void RoundToZero(ref Vector3 vector, float cutoff)
    {
        if (vector.LengthSquared() < cutoff * cutoff)
        {
            vector = Vector3.Zero;
        }
    }

    // Vector3.Transform(Vector3, Quaternion) loses all precision for tiny vectors (e.g. inverse inertia
    // products of pixel-scale bodies, ~1e-10), so rotate explicitly: v + w*t + q x t, with t = 2(q x v).
    public static Vector3 Rotate(Vector3 v, Quaternion q)
    {
        var u = new Vector3(q.X, q.Y, q.Z);
        Vector3 t = 2f * Vector3.Cross(u, v);
        return v + q.W * t + Vector3.Cross(u, t);
    }

    public static Vector3 InverseRotate(Vector3 v, Quaternion q) => Rotate(v, Quaternion.Conjugate(q));

    /// <summary>
    /// Advances an orientation by a world-space angular velocity.
    /// </summary>
    public static Quaternion IntegrateOrientation(Quaternion q, Vector3 angularVelocity, float dt)
    {
        var spin = new Quaternion(angularVelocity.X, angularVelocity.Y, angularVelocity.Z, 0f) * q;
        return Quaternion.Normalize(q + spin * (0.5f * dt));
    }

    /// <summary>
    /// Converts a rotation to axis * angle (shortest path), in radians.
    /// </summary>
    public static Vector3 ToRotationVector(Quaternion q)
    {
        if (q.W < 0f)
            q = Quaternion.Negate(q);

        var axis = new Vector3(q.X, q.Y, q.Z);
        float sinHalf = axis.Length();
        if (sinHalf < 1e-6f)
            return axis * 2f;

        float angle = 2f * MathF.Atan2(sinHalf, q.W);
        return axis * (angle / sinHalf);
    }

    public static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 ab = b - a;
        float lengthSq = ab.LengthSquared();
        if (lengthSq < 1e-12f)
            return a;

        float t = Math.Clamp(Vector3.Dot(p - a, ab) / lengthSq, 0f, 1f);
        return a + ab * t;
    }

    /// <summary>
    /// Shifts an inertia tensor from a body's center of mass to a point at <paramref name="offset"/>.
    /// </summary>
    public static Mat3 ParallelAxis(Mat3 inertia, float mass, Vector3 offset)
    {
        return inertia + (Mat3.Identity * offset.LengthSquared() - Mat3.OuterProduct(offset, offset)) * mass;
    }
}
