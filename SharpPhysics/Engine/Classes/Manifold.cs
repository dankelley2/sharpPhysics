#nullable enable
using System.Numerics;
using SharpPhysics.Engine.Objects;

namespace SharpPhysics.Engine.Classes;

public class Manifold
{
    public const int MaxContactPoints = 8;

    public PhysicsObject A = null!;
    public PhysicsObject B = null!;
    public float Penetration;
    /// <summary>Unit collision normal pointing from A to B.</summary>
    public Vector3 Normal;
    /// <summary>Average of <see cref="ContactPoints"/>.</summary>
    public Vector3 ContactPoint;
    public readonly Vector3[] ContactPoints = new Vector3[MaxContactPoints];
    public int ContactCount;

    public void Reset()
    {
        A = null!;
        B = null!;
        Penetration = 0;
        Normal = Vector3.Zero;
        ContactPoint = Vector3.Zero;
        ContactCount = 0;
    }
}
