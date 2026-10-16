#nullable enable
using SharpPhysics.Engine.Objects;

namespace SharpPhysics.Rendering.Shaders;

public class NoneShader : ObjectShader
{
    public override void Draw(PhysicsObject obj, SceneRenderer scene)
    {
    }
}
