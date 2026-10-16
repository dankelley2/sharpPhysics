#nullable enable
using System.Collections.Generic;
using System.Numerics;

namespace SharpPhysics.Engine.Shapes;

public class BoxPhysShape : PolyhedronPhysShape
{
    public override ShapeTypeEnum ShapeType => ShapeTypeEnum.Box;

    public float Width { get; }
    public float Height { get; }
    public float Depth { get; }

    public BoxPhysShape(float width, float height, float depth)
        : base(CreateCorners(width, height, depth))
    {
        Width = width;
        Height = height;
        Depth = depth;
    }

    private static IEnumerable<Vector3> CreateCorners(float width, float height, float depth)
    {
        var half = new Vector3(width, height, depth) * 0.5f;
        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
            yield return new Vector3(x, y, z) * half;
    }
}
