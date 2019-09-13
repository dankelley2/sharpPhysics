using System.Drawing;
using physics.Engine.Classes;

namespace physics.Engine.Structs
{
    
    public struct Manifold
    {
        public PhysicsObject A;
        public PhysicsObject B;
        public Vec2 ANewVelocity;
        public Vec2 BNewVelocity;
        public float Penetration;
        public Vec2 Normal;

    }
}