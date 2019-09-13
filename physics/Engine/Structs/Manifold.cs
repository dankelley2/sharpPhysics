using System.Drawing;
using physics.Engine.Classes;

namespace physics.Engine.Structs
{
    
    public struct Manifold
    {
        public PhysicsObject A;
        public PhysicsObject B;
        public float Penetration;
        public Vec2 Normal;

    }
}