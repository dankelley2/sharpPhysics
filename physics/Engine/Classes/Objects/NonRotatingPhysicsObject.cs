using SFML.System;
using physics.Engine.Shapes;

namespace physics.Engine.Objects
{
    public class NonRotatingPhysicsObject : PhysicsObject
    {
        public NonRotatingPhysicsObject(IShape shape, Vector2f center, float restitution, bool locked, SFMLShader shader, float mass = 0)
            : base(shape, center, restitution, locked, shader, mass)
        {
        }

        // No rotation update for nonrotating objects.
        public override void UpdateRotation(float dt)
        {
            // Intentionally left blank.
        }
    }
}
