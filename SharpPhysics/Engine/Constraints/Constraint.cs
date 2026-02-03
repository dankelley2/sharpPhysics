using System.Numerics;
using SharpPhysics.Engine.Objects;

namespace SharpPhysics.Engine.Constraints
{
    /// <summary>
    /// Base constraint type. Constraints link two PhysicsObjects.
    /// </summary>
    public abstract class Constraint
    {
        public PhysicsObject A { get; protected set; }
        public PhysicsObject B { get; protected set; }

        // Anchor points in local space of each object
        public Vector2 AnchorA { get; protected set; }
        public Vector2 AnchorB { get; protected set; }

        // Arbitrary break point when the constraint is pushed past its limits
        public bool CanBreak { get; set; } = false;
        public bool IsBroken { get; protected set; } = false;

        protected Constraint(PhysicsObject a, PhysicsObject b)
        {
            A = a;
            B = b;
            // Mark the objects as connected so that collisions between them are ignored.
            if (!A.ConnectedObjects.Contains(B))
                A.ConnectedObjects.Add(B);
            if (!B.ConnectedObjects.Contains(A))
                B.ConnectedObjects.Add(A);

            A.CanSleep = false;
            B.CanSleep = false;
        }

        /// <summary>
        /// Applies the constraint correction (via impulses or position/angle adjustments).
        /// </summary>
        public abstract void ApplyConstraint(float dt);
    }
}
