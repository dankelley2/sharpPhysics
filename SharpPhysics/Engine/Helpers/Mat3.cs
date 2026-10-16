#nullable enable
using System;
using System.Numerics;

namespace SharpPhysics.Engine.Helpers;

/// <summary>
/// Row-major 3x3 matrix (multiplies column vectors). Used for inertia tensors.
/// </summary>
public readonly struct Mat3
{
    public readonly float M11, M12, M13;
    public readonly float M21, M22, M23;
    public readonly float M31, M32, M33;

    public Mat3(
        float m11, float m12, float m13,
        float m21, float m22, float m23,
        float m31, float m32, float m33)
    {
        M11 = m11; M12 = m12; M13 = m13;
        M21 = m21; M22 = m22; M23 = m23;
        M31 = m31; M32 = m32; M33 = m33;
    }

    public static Mat3 Zero => default;
    public static Mat3 Identity => new(1, 0, 0, 0, 1, 0, 0, 0, 1);

    public static Mat3 Diagonal(float x, float y, float z) => new(x, 0, 0, 0, y, 0, 0, 0, z);

    public static Mat3 OuterProduct(Vector3 a, Vector3 b) => new(
        a.X * b.X, a.X * b.Y, a.X * b.Z,
        a.Y * b.X, a.Y * b.Y, a.Y * b.Z,
        a.Z * b.X, a.Z * b.Y, a.Z * b.Z);

    /// <summary>
    /// Rotation matrix equivalent to <see cref="PhysMath.Rotate(Vector3, Quaternion)"/>.
    /// </summary>
    public static Mat3 FromQuaternion(Quaternion q)
    {
        float xx = q.X * q.X, yy = q.Y * q.Y, zz = q.Z * q.Z;
        float xy = q.X * q.Y, xz = q.X * q.Z, yz = q.Y * q.Z;
        float wx = q.W * q.X, wy = q.W * q.Y, wz = q.W * q.Z;

        return new Mat3(
            1 - 2 * (yy + zz), 2 * (xy - wz), 2 * (xz + wy),
            2 * (xy + wz), 1 - 2 * (xx + zz), 2 * (yz - wx),
            2 * (xz - wy), 2 * (yz + wx), 1 - 2 * (xx + yy));
    }

    public float Trace => M11 + M22 + M33;

    public float Determinant =>
        M11 * (M22 * M33 - M23 * M32) -
        M12 * (M21 * M33 - M23 * M31) +
        M13 * (M21 * M32 - M22 * M31);

    public Mat3 Transpose() => new(M11, M21, M31, M12, M22, M32, M13, M23, M33);

    /// <summary>
    /// Returns the inverse, or <see cref="Zero"/> if the matrix is singular.
    /// </summary>
    public Mat3 Inverse()
    {
        // Normalize first so large inertia tensors don't overflow the determinant.
        float scale = MathF.Max(MathF.Max(MathF.Max(MathF.Abs(M11), MathF.Abs(M12)), MathF.Max(MathF.Abs(M13), MathF.Abs(M21))),
                      MathF.Max(MathF.Max(MathF.Abs(M22), MathF.Abs(M23)), MathF.Max(MathF.Max(MathF.Abs(M31), MathF.Abs(M32)), MathF.Abs(M33))));
        if (scale == 0f)
            return Zero;

        Mat3 n = this * (1f / scale);
        float det = n.Determinant;
        if (MathF.Abs(det) < 1e-12f)
            return Zero;

        float invDet = 1f / (det * scale);
        return new Mat3(
            (n.M22 * n.M33 - n.M23 * n.M32) * invDet, (n.M13 * n.M32 - n.M12 * n.M33) * invDet, (n.M12 * n.M23 - n.M13 * n.M22) * invDet,
            (n.M23 * n.M31 - n.M21 * n.M33) * invDet, (n.M11 * n.M33 - n.M13 * n.M31) * invDet, (n.M13 * n.M21 - n.M11 * n.M23) * invDet,
            (n.M21 * n.M32 - n.M22 * n.M31) * invDet, (n.M12 * n.M31 - n.M11 * n.M32) * invDet, (n.M11 * n.M22 - n.M12 * n.M21) * invDet);
    }

    public static Vector3 operator *(Mat3 m, Vector3 v) => new(
        m.M11 * v.X + m.M12 * v.Y + m.M13 * v.Z,
        m.M21 * v.X + m.M22 * v.Y + m.M23 * v.Z,
        m.M31 * v.X + m.M32 * v.Y + m.M33 * v.Z);

    public static Mat3 operator *(Mat3 a, Mat3 b) => new(
        a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31, a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32, a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33,
        a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31, a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32, a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33,
        a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31, a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32, a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33);

    public static Mat3 operator *(Mat3 m, float s) => new(
        m.M11 * s, m.M12 * s, m.M13 * s,
        m.M21 * s, m.M22 * s, m.M23 * s,
        m.M31 * s, m.M32 * s, m.M33 * s);

    public static Mat3 operator +(Mat3 a, Mat3 b) => new(
        a.M11 + b.M11, a.M12 + b.M12, a.M13 + b.M13,
        a.M21 + b.M21, a.M22 + b.M22, a.M23 + b.M23,
        a.M31 + b.M31, a.M32 + b.M32, a.M33 + b.M33);

    public static Mat3 operator -(Mat3 a, Mat3 b) => new(
        a.M11 - b.M11, a.M12 - b.M12, a.M13 - b.M13,
        a.M21 - b.M21, a.M22 - b.M22, a.M23 - b.M23,
        a.M31 - b.M31, a.M32 - b.M32, a.M33 - b.M33);
}
