using physics.Engine.Structs;

namespace physics.Engine.Classes
{
    
    public class Manifold
    {
        public PhysicsObject A;
        public PhysicsObject B;
        public float Penetration;
        public Vec2 Normal;

    }
}