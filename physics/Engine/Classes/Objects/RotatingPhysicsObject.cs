using SFML.System;
using physics.Engine.Shapes;
using System;
using physics.Engine.Structs;
using SFML.Graphics;
using System.Net.Sockets;

namespace physics.Engine.Objects
{
    public class RotatingPhysicsObject : PhysicsObject
    {
        public float AngularVelocity { get; set; }
        public float Inertia { get; private set; }
        public float IInertia { get; private set; }

        public RotatingPhysicsObject(IShape shape, Vector2f center, float restitution, bool locked, SFMLShader shader, float mass = 0)
            : base(shape, center, restitution, locked, shader, mass)
        {
            AngularVelocity = 0;
            Inertia = Shape.GetMomentOfInertia(Mass);
            IInertia = (Inertia != 0) ? 1 / Inertia : 0;
            CanRotate = true;
        }

        public override void UpdateRotation(float dt)
        {
            if (Locked)
                return;

            Angle += AngularVelocity * dt;
            AngularVelocity *= 0.9999f; // Apply angular damping.
            if (Math.Abs(AngularVelocity) < 0.0001f)
                AngularVelocity = 0;

            // Update AABB to reflect the new rotation.
            Aabb = Shape.GetAABB(Center, Angle);
        }

        public override void Move(float dt)
        {
            if (Mass >= 1000000)
                return;

            RoundSpeedToZero();
            Center += Velocity * dt;
            // Update AABB including current rotation.
            Aabb = Shape.GetAABB(Center, Angle);
        }
    }
}
