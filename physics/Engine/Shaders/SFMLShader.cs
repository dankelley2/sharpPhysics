using SFML.Graphics;
using physics.Engine.Objects;

namespace physics.Engine.Shaders
{
    public abstract class SFMLShader
    {
        public abstract void PreDraw(PhysicsObject obj, RenderTarget target);
        public abstract void Draw(PhysicsObject obj, RenderTarget target);
        public abstract void PostDraw(PhysicsObject obj, RenderTarget target);
    }
}
