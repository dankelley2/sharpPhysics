using System;
using System.Collections.Generic;
using SFML.System;
using physics.Engine.Shapes; // For IShape if needed
using physics.Engine.Structs;
using physics.Engine.Extensions;
using physics.Engine.Objects;
using physics.Engine.Helpers;

namespace physics.Engine.Constraints
{
    /// <summary>
    /// Base constraint type. Constraints link two PhysicsObjects.
    /// </summary>
    public abstract class Constraint
    {
        public PhysicsObject A { get; protected set; }
        public PhysicsObject B { get; protected set; }

        public Constraint(PhysicsObject a, PhysicsObject b)
        {
            A = a;
            B = b;
            // Mark the objects as connected so that collisions between them are ignored.
            if (!A.ConnectedObjects.Contains(B))
                A.ConnectedObjects.Add(B);
            if (!B.ConnectedObjects.Contains(A))
                B.ConnectedObjects.Add(A);
        }

        /// <summary>
        /// Applies the constraint correction (via impulses or position/angle adjustments).
        /// </summary>
        public abstract void ApplyConstraint(float dt);
    }

    /// <summary>
    /// A weld constraint holds two objects together so that the world-space positions
    /// of their respective local anchors remain coincident, and their relative angle remains constant.
    /// </summary>
    public class WeldConstraint : Constraint
    {
        /// <summary>
        /// Local anchor on object A (relative to its center).
        /// </summary>
        public Vector2f AnchorA { get; private set; }
        /// <summary>
        /// Local anchor on object B (relative to its center).
        /// </summary>
        public Vector2f AnchorB { get; private set; }
        /// <summary>
        /// The initial relative angle between the objects.
        /// </summary>
        public float InitialRelativeAngle { get; private set; }

        public WeldConstraint(PhysicsObject a, PhysicsObject b, Vector2f anchorA, Vector2f anchorB)
            : base(a, b)
        {
            AnchorA = anchorA;
            AnchorB = anchorB;
            InitialRelativeAngle = b.Angle - a.Angle;
        }

        public override void ApplyConstraint(float dt)
        {
            // Compute world-space positions of the anchor points.
            Vector2f worldAnchorA = A.Center + PhysMath.RotateVector(AnchorA, A.Angle);
            Vector2f worldAnchorB = B.Center + PhysMath.RotateVector(AnchorB, B.Angle);

            // Compute the error vector between the two anchors.
            Vector2f error = worldAnchorB - worldAnchorA;

            // Use a simple spring-damper model to compute a corrective impulse.
            float stiffness = 0.8f; // tune as needed
            float damping = 0.2f;   // tune as needed
            Vector2f correctiveImpulse = error * stiffness - (B.Velocity - A.Velocity) * damping;

            if (!A.Locked)
                A.Velocity += correctiveImpulse * A.IMass;
            if (!B.Locked)
                B.Velocity -= correctiveImpulse * B.IMass;

            // Angular correction: maintain the initial relative orientation.
            float angleError = (B.Angle - A.Angle) - InitialRelativeAngle;
            float angularStiffness = 0.8f; // tune as needed
            float angularImpulse = angleError * angularStiffness;
            if (!A.Locked && A.CanRotate)
                A.AngularVelocity += angularImpulse * A.IInertia;
            if (!B.Locked && B.CanRotate)
                B.AngularVelocity -= angularImpulse * B.IInertia;
        }
    }

    /// <summary>
    /// An AxisConstraint pins two objects together at specified local anchor points,
    /// allowing only rotation around the anchor.
    /// </summary>
    public class AxisConstraint : Constraint
    {
        /// <summary>
        /// The local anchor point on object A (relative to its center).
        /// </summary>
        public Vector2f AnchorA { get; private set; }
        /// <summary>
        /// The local anchor point on object B (relative to its center).
        /// </summary>
        public Vector2f AnchorB { get; private set; }

        /// <summary>
        /// Creates an axis constraint (a revolute joint) that ensures the two objects’ anchor points remain coincident.
        /// Only translation is corrected, so the objects may freely rotate around the anchor.
        /// </summary>
        /// <param name="a">Object A (e.g., the chassis)</param>
        /// <param name="b">Object B (e.g., a wheel)</param>
        /// <param name="anchorA">The local anchor on object A.</param>
        /// <param name="anchorB">The local anchor on object B.</param>
        public AxisConstraint(PhysicsObject a, PhysicsObject b, Vector2f anchorA, Vector2f anchorB)
            : base(a, b)
        {
            AnchorA = anchorA;
            AnchorB = anchorB;
        }

        public override void ApplyConstraint(float dt)
        {
            // Compute world-space positions of the anchors.
            Vector2f worldAnchorA = A.Center + PhysMath.RotateVector(AnchorA, A.Angle);
            Vector2f worldAnchorB = B.Center + PhysMath.RotateVector(AnchorB, B.Angle);

            // Compute the error (the difference between the anchor positions).
            Vector2f error = worldAnchorB - worldAnchorA;

            // If the error is negligible, nothing to do.
            if (error.LengthSquared() < 0.001f)
                return;

            // Instead of gradually correcting the error with impulses,
            // we want to almost instantaneously remove it.
            // If both objects are free, split the correction between them.
            if (!A.Locked && !B.Locked)
            {
                A.Move(error * 0.5f);
                B.Move(-error * 0.5f);
            }
            else if (!B.Locked)
            {
                B.Move(-error * 0.5f);
            }
            else if (!A.Locked)
            {
                A.Move(error * 0.5f);
            }

            // Now remove any relative velocity along the constraint direction.
            // Compute the unit error vector.
            Vector2f unitError = error.Normalize();
            Vector2f relativeVelocity = B.Velocity - A.Velocity;
            float relVelAlongError = Extensions.Extensions.DotProduct(relativeVelocity, unitError);
            Vector2f velCorrection = unitError * relVelAlongError;
            // Subtract the velocity component that would move the anchors apart.
            if (!B.Locked)
                B.Velocity -= velCorrection;
            if (!A.Locked)
                A.Velocity += velCorrection;
        }


    }
}
