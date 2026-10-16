#nullable enable
using SFML.Graphics;
using SharpPhysics.Engine.Objects;

namespace SharpPhysics.Rendering.Shaders;

/// <summary>
/// Draws the object's shape as a lit solid in a single color.
/// </summary>
public class SolidShader : ObjectShader
{
    public Color Color { get; set; }
    public float Specular { get; set; }

    public SolidShader(Color color, float specular = 0.25f)
    {
        Color = color;
        Specular = specular;
    }

    public override void Draw(PhysicsObject obj, SceneRenderer scene)
    {
        scene.DrawShape(obj.Shape, obj.Center, obj.Orientation, Color, Specular);
    }
}
