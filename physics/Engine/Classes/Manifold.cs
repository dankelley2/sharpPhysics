using physics.Engine.Structs;
using SFML.System;

namespace physics.Engine.Classes
{
    
    public class Manifold
    {
        public PhysicsObject A;
        public PhysicsObject B;
        public float Penetration;
        public Vector2f Normal;

    }
}