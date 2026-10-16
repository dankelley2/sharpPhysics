#nullable enable
using SharpPhysics.Engine.Objects;

namespace SharpPhysics.Rendering.Shaders;

/// <summary>
/// Decides how a physics object is drawn in the depth-tested 3D scene.
/// </summary>
public abstract class ObjectShader
{
    public abstract void Draw(PhysicsObject obj, SceneRenderer scene);
}
