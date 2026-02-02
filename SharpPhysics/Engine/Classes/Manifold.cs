using SharpPhysics.Engine.Objects;
using System.Numerics;

namespace SharpPhysics.Engine.Classes
{
    public class Manifold
    {
        public PhysicsObject A;
        public PhysicsObject B;
        public float Penetration;
        public Vector2 Normal;
        public Vector2 ContactPoint;

        public void Reset()
        {
            A = null;
            B = null;
            Penetration = 0;
            Normal = new Vector2(0, 0);
            ContactPoint = new Vector2(0, 0);
        }
    }
}
