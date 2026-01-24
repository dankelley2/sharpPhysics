using SFML.Graphics;
using physics.Engine.Objects;

namespace physics.Engine.Shaders
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
