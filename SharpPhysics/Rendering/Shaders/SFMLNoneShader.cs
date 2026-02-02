using SFML.Graphics;
using SharpPhysics.Engine.Objects;

namespace SharpPhysics.Rendering.Shaders
{
    public class SFMLNoneShader : SFMLShader
    {

        public override void PreDraw(PhysicsObject obj, RenderTarget target)
        {
            // Do nothing
        }

        public override void Draw(PhysicsObject obj, RenderTarget target)
        {
            // Do nothing
        }

        public override void PostDraw(PhysicsObject obj, RenderTarget target)
        {
        }
    }
}
