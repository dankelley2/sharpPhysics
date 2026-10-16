#nullable enable
using System.Numerics;
using SFML.Graphics;
using SharpPhysics.Engine.Objects;
using SharpPhysics.Rendering;
using SharpPhysics.Rendering.Shaders;

namespace SharpPhysics3d.Demo;

/// <summary>
/// The table bed: a felt box with pockets and the head string drawn on its top surface (z = 0).
/// </summary>
public class PoolTableShader : ObjectShader
{
    // Lifts markings off the felt so they don't z-fight with it.
    private const float DecalLift = 0.5f;
    private static readonly Color HeadStringColor = new(220, 235, 220);

    private readonly Color _feltColor;
    private readonly Vector2[] _pockets;
    private readonly float _pocketRadius;
    private readonly Vector2 _headStringStart;
    private readonly Vector2 _headStringEnd;

    public PoolTableShader(Color feltColor, Vector2[] pockets, float pocketRadius, Vector2 headStringStart, Vector2 headStringEnd)
    {
        _feltColor = feltColor;
        _pockets = pockets;
        _pocketRadius = pocketRadius;
        _headStringStart = headStringStart;
        _headStringEnd = headStringEnd;
    }

    public override void Draw(PhysicsObject obj, SceneRenderer scene)
    {
        scene.DrawShape(obj.Shape, obj.Center, obj.Orientation, _feltColor, specular: 0.05f);

        foreach (var pocket in _pockets)
            scene.DrawDisc(new Vector3(pocket, -DecalLift), _pocketRadius, Quaternion.Identity, Color.Black);

        scene.DrawLine(new Vector3(_headStringStart, -DecalLift), new Vector3(_headStringEnd, -DecalLift), HeadStringColor);
    }
}
