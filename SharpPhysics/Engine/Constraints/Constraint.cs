using System.Numerics;
using SharpPhysics.Engine.Objects;

namespace SharpPhysics.Engine.Constraints
{
    /// <summary>
    /// Base constraint type. Constraints link two PhysicsObjects.
    /// </summary>
    public abstract class Constraint
    {
        public PhysicsObject A { get; set; }
        public PhysicsObject B { get; set; }

        // Anchor points in local space of each object
        public Vector2 AnchorA { get; set; }
        public Vector2 AnchorB { get; set; }

        // Initial relative angle between the two objects (B.Angle - A.Angle at creation time)
        public float InitialRelativeAngle { get; set; }

        // Arbitrary break point when the constraint is pushed past its limits
        public bool CanBreak { get; set; } = false;
        public bool IsBroken { get; protected set; } = false;

        protected Constraint(PhysicsObject a, PhysicsObject b)
        {
            A = a;
            B = b;

            // Store the initial relative angle
            InitialRelativeAngle = b.Angle - a.Angle;

            // Mark the objects as connected so that collisions between them are ignored.
            A.ConnectedObjects.Add(B);
            B.ConnectedObjects.Add(A);

            // Add constraints for tracking
            A.Constraints.Add(this);
            B.Constraints.Add(this);

            A.CanSleep = false;
            B.CanSleep = false;
        }

        /// <summary>
        /// Applies the constraint correction (via impulses or position/angle adjustments).
        /// </summary>
        public abstract void ApplyConstraint(float dt);
    }
}
