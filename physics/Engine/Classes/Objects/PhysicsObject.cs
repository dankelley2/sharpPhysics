using SFML.System;
using physics.Engine.Structs;
using physics.Engine.Shapes;
using System;
using physics.Engine.Classes;

namespace physics.Engine.Objects
{
    public abstract class PhysicsObject
    {
        public IShape Shape { get; protected set; }
        public AABB Aabb { get; protected set; }
        public Vector2f Center { get; protected set; }
        public Vector2f Velocity { get; set; }
        public float Restitution { get; set; }
        public float Mass { get; protected set; }
        public float IMass { get; protected set; }
        public bool Locked { get; set; }
        public SFMLShader Shader { get; set; }
        public Vector2f LastContactPoint { get; set; }
        public bool CanRotate { get; internal set; } = false;

        // Friction, in newtons
        public float Friction = 0.5f;

        /// <summary>
        /// Orientation in radians.
        /// </summary>
        public float Angle { get; set; }

        public PhysicsObject(IShape shape, Vector2f center, float restitution, bool locked, SFMLShader shader, float mass = 0)
        {
            Shape = shape;
            Center = center;
            Angle = 0;
            Velocity = new Vector2f(0, 0);
            Restitution = restitution;
            Locked = locked;
            Shader = shader;
            Mass = (mass == 0) ? shape.GetArea() : mass;
            IMass = 1 / Mass;
            Aabb = Shape.GetAABB(Center, Angle);
        }

        /// <summary>
        /// Moves the object based on its velocity and updates its AABB.
        /// </summary>
        public virtual void Move(float dt)
        {
            if (Mass >= 1000000)
                return;

            RoundSpeedToZero();
            Center += Velocity * dt;
            Aabb = Shape.GetAABB(Center, Angle);
        }

        protected void RoundSpeedToZero()
        {
            if (Math.Abs(Velocity.X) + Math.Abs(Velocity.Y) < 0.01f)
            {
                Velocity = new Vector2f(0, 0);
            }
        }

        /// <summary>
        /// Directly translates the object by a given vector.
        /// </summary>
        public virtual void Move(Vector2f dVector)
        {
            if (Locked)
                return;

            Center += dVector;
            Aabb = Shape.GetAABB(Center, Angle);
        }

        /// <summary>
        /// Updates rotation. By default, does nothing.
        /// </summary>
        public virtual void UpdateRotation(float dt)
        {
            // No rotation update by default.
        }

        /// <summary>
        /// Determines whether a given point (in world coordinates) lies within the object.
        /// This method delegates to the shape's own containment logic using the object's center and rotation.
        /// </summary>
        public bool Contains(Vector2f point)
        {
            return Shape.Contains(point, Center, Angle);
        }
    }
}
