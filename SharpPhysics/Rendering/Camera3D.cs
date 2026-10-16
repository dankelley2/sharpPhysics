#nullable enable
using System;
using System.Numerics;
using SharpPhysics.Engine.Helpers;

namespace SharpPhysics.Rendering;

/// <summary>
/// Perspective camera orbiting a pivot, projecting world points into GameView coordinates.
/// With the default orientation it looks along +Z, and points on the pivot's Z plane map 1:1 onto X/Y.
/// </summary>
public class Camera3D
{
    private const float MinDepth = 1f;
    private const float DefaultDistance = 1000f;

    public Vector3 Pivot { get; set; }

    /// <summary>Distance from the camera to the pivot. Larger values flatten the perspective.</summary>
    public float Distance { get; set; } = DefaultDistance;

    /// <summary>Rotation from view space (X right, Y down, Z forward) to world space.</summary>
    public Quaternion Orientation { get; set; } = Quaternion.Identity;

    public Vector3 Position => Pivot + PhysMath.Rotate(new Vector3(0, 0, -Distance), Orientation);

    public Camera3D(Vector3 pivot)
    {
        Pivot = pivot;
    }

    public void Reset(Vector3 pivot)
    {
        Pivot = pivot;
        Distance = DefaultDistance;
        Orientation = Quaternion.Identity;
    }

    /// <summary>
    /// Moves the camera around the pivot, rotating about its own view axes (radians).
    /// Positive yaw moves the camera toward screen-left, positive pitch toward screen-down.
    /// </summary>
    public void Orbit(float yaw, float pitch)
    {
        var delta = Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw) * Quaternion.CreateFromAxisAngle(Vector3.UnitX, pitch);
        Orientation = Quaternion.Normalize(Orientation * delta);
    }

    public Vector3 ToView(Vector3 world) => PhysMath.InverseRotate(world - Pivot, Orientation);

    /// <summary>
    /// Perspective scale at a world point (1 on the plane through the pivot facing the camera).
    /// </summary>
    public float GetScale(Vector3 world) => Distance / MathF.Max(Distance + ToView(world).Z, MinDepth);

    public Vector2 Project(Vector3 world)
    {
        Vector3 view = ToView(world);
        float scale = Distance / MathF.Max(Distance + view.Z, MinDepth);
        return new Vector2(Pivot.X + view.X * scale, Pivot.Y + view.Y * scale);
    }

    /// <summary>
    /// World-space ray from the camera through a GameView point.
    /// </summary>
    public (Vector3 Origin, Vector3 Direction) GetRay(Vector2 viewPoint)
    {
        var direction = new Vector3(viewPoint.X - Pivot.X, viewPoint.Y - Pivot.Y, Distance);
        return (Position, Vector3.Normalize(PhysMath.Rotate(direction, Orientation)));
    }

    public bool IsFacing(Vector3 pointOnSurface, Vector3 normal) => Vector3.Dot(normal, Position - pointOnSurface) > 0f;

    /// <summary>
    /// World-to-clip matrix (System.Numerics row-vector convention) that reproduces <see cref="Project"/>
    /// for a GameView covering <paramref name="viewCenter"/> ± <paramref name="viewSize"/>/2 (Y down).
    /// Near/far are distances along the view axis from the camera.
    /// </summary>
    public Matrix4x4 GetViewProjection(Vector2 viewCenter, Vector2 viewSize, float near, float far)
    {
        var view = Matrix4x4.CreateTranslation(-Pivot) * Matrix4x4.CreateFromQuaternion(Quaternion.Conjugate(Orientation));

        float d = Distance;
        float left = viewCenter.X - viewSize.X / 2f;
        float top = viewCenter.Y - viewSize.Y / 2f;
        // Pivot position in NDC; Y is flipped because GameView is Y-down.
        float pivotNdcX = (Pivot.X - left) * 2f / viewSize.X - 1f;
        float pivotNdcY = 1f - (Pivot.Y - top) * 2f / viewSize.Y;
        float depthA = (far + near) / (far - near);
        float depthB = -2f * far * near / (far - near);

        // Clip w is the view-axis distance from the camera: Distance + view.Z.
        var projection = new Matrix4x4(
            2f * d / viewSize.X, 0f, 0f, 0f,
            0f, -2f * d / viewSize.Y, 0f, 0f,
            pivotNdcX, pivotNdcY, depthA, 1f,
            pivotNdcX * d, pivotNdcY * d, depthA * d + depthB, d);

        return view * projection;
    }
}
