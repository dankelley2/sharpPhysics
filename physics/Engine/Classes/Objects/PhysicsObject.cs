using SFML.System;
using physics.Engine.Structs;
using physics.Engine.Shapes;
using System;
using physics.Engine.Classes;
using System.Collections.Generic;
using physics.Engine.Shaders;
using System.Numerics;
using physics.Engine.Extensions;
using System.Linq;

namespace physics.Engine.Objects
{
    public class PhysicsObject
    {
        public readonly Guid Id = Guid.NewGuid();
        public IShape Shape { get; protected set; }
        public AABB Aabb { get; protected set; }
        public Vector2f Center { get; protected set; }
        public Vector2f Velocity { get; set; }
        public float Restitution { get; set; }
        public float Mass { get; protected set; }
        public float IMass { get; protected set; }
        public bool Locked { get; set; }
        public SFMLShader Shader { get; set; }
        public bool CanRotate { get; internal set; } = false;
        public readonly List<PhysicsObject> ConnectedObjects = new List<PhysicsObject>();
        public float AngularVelocity { get; set; }
        public float Inertia { get; private set; }
        public float IInertia { get; private set; }

        // Friction, in newtons
        public float Friction = 0.5f;

        /// <summary>
        /// Orientation in radians.
        /// </summary>
        public float Angle { get; set; }

        // --- New caching and events ---
        // Event fired when a new contact point is added.
        public event Action<PhysicsObject, (Vector2f, Vector2f)> ContactPointAdded;
        // Event fired when a contact point is removed.
        public event Action<PhysicsObject, (Vector2f, Vector2f)> ContactPointRemoved;
        // Current contact points. The key is the object, and the value is a tuple of (point, normal).
        private readonly Dictionary<PhysicsObject, (Vector2f, Vector2f)> _contactPoints = new Dictionary<PhysicsObject, (Vector2f, Vector2f)>();
        // Cached contacts from the previous update.
        private readonly Dictionary<PhysicsObject, (Vector2f, Vector2f)> _previousContactPoints = new Dictionary<PhysicsObject, (Vector2f, Vector2f)>();

        // --- Sleep/Wake state management ---
        public bool Sleeping { get; private set; } = false;
        private float sleepTimer = 0f;
        // The threshold is now based on the actual movement (displacement) during the update.
        private const float LinearSleepThreshold = 0.05f; // Units: adjust based on your world scale.
        private const float AngularSleepThreshold = 0.1f;
        private const float SleepTimeThreshold = 0.7f; // e.g. 0.8 seconds of inactivity.

        // Store the previous center to compute displacement.
        private Vector2f _prevCenter;

        public PhysicsObject(IShape shape, Vector2f center, float restitution, bool locked, SFMLShader shader, float mass = 0, bool canRotate = false)
        {
            Shape = shape;
            Center = center;
            _prevCenter = center; // initialize previous center.
            Angle = 0;
            Velocity = new Vector2f(0, 0);
            Restitution = restitution;
            Locked = locked;
            Shader = shader;
            Mass = (mass == 0) ? shape.GetArea() : mass;
            IMass = 1 / Mass;
            Aabb = Shape.GetAABB(Center, Angle);
            AngularVelocity = 0;
            Inertia = Shape.GetMomentOfInertia(Mass);
            IInertia = (Inertia != 0) ? 1 / Inertia : 0;
            CanRotate = canRotate;
        }

        /// <summary>
        /// Runs the object's update logic.
        /// </summary>
        public void Update(float dt)
        {
            if (!Sleeping)
            {
                Move(dt);
                UpdateRotation(dt);
            }
            // Update sleep state based on actual displacement (movement) rather than instantaneous velocity.
            UpdateSleepState(dt);
            UpdateContactPoints();
            // Update the previous center for next frame comparison.
            _prevCenter = Center;
        }

        /// <summary>
        /// Checks whether the object has moved less than the threshold between updates.
        /// If so, accumulates a timer. Once the timer exceeds a set threshold, the object is put to sleep.
        /// If movement exceeds the threshold, the timer is reset.
        /// </summary>
        public void UpdateSleepState(float dt)
        {
            if (Locked)
            {
                sleepTimer = 0f;
                return;
            }

            // Compute displacement as the distance between the current center and previous center.
            float displacement = (Center - _prevCenter).Length();

            // Compare displacement against the threshold and also check angular movement.
            if (displacement < LinearSleepThreshold && Math.Abs(AngularVelocity) < AngularSleepThreshold)
            {
                sleepTimer += dt;
                if (sleepTimer >= SleepTimeThreshold)
                {
                    Sleep();
                }
            }
            else
            {
                sleepTimer = 0f;
                if (Sleeping)
                    Wake();
            }
        }

        /// <summary>
        /// Puts the object to sleep: sets Sleeping to true, and zeroes out velocity and angular velocity.
        /// </summary>
        public void Sleep()
        {
            if (Sleeping) return;
            Sleeping = true;
            Velocity = new Vector2f(0, 0);
            AngularVelocity = 0;
            // Optionally, clear contact caches if desired.
        }

        /// <summary>
        /// Wakes the object from sleep.
        /// </summary>
        public void Wake()
        {
            if (!Sleeping) return;
            Sleeping = false;
            sleepTimer = 0f;
            // Optionally, propagate wake to contacting objects.
        }

        /// <summary>
        /// Safe method to add a contact point to the dictionary.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="point"></param>
        /// <param name="normal"></param>
        public void AddContact(PhysicsObject obj, Vector2f point, Vector2f normal)
        {
            if (!_contactPoints.ContainsKey(obj))
            {
                _contactPoints[obj] = (point, normal);
            }
        }

        /// <summary>
        /// Retrieve the most recent contact points.
        /// </summary>
        /// <returns></returns>
        public Dictionary<PhysicsObject, (Vector2f, Vector2f)> GetContacts(){
            return _previousContactPoints;
        }

        /// <summary>
        /// Compares the current contact points to the previous frame’s contact points,
        /// fires events for added and removed contacts, updates the cache, and then clears the current list.
        /// This method is written to be allocation-efficient.
        /// </summary>
        public void UpdateContactPoints()
        {
            // If both the current and previous contacts are empty, there's nothing to do.
            if (_contactPoints.Count == 0 && _previousContactPoints.Count == 0)
                return;

            // If there are no subscribers to either event, update the cache and clear the current contacts.
            if (ContactPointAdded == null && ContactPointRemoved == null)
            {
                _previousContactPoints.Clear();
                foreach (KeyValuePair<PhysicsObject, (Vector2f, Vector2f)> kv in _contactPoints)
                {
                    _previousContactPoints.Add(kv.Key, kv.Value);
                }
                _contactPoints.Clear();
                return;
            }

            // Cache the event delegates locally to avoid repeated null checks.
            var addedHandler = ContactPointAdded;
            var removedHandler = ContactPointRemoved;

            // Fire events for newly added contacts.
            foreach (KeyValuePair<PhysicsObject, (Vector2f, Vector2f)> kv in _contactPoints)
            {
                if (!_previousContactPoints.ContainsKey(kv.Key))
                {
                    addedHandler?.Invoke(kv.Key, kv.Value);
                }
            }

            // Fire events for contacts that were removed.
            foreach (KeyValuePair<PhysicsObject, (Vector2f, Vector2f)> kv in _previousContactPoints)
            {
                if (!_contactPoints.ContainsKey(kv.Key))
                {
                    removedHandler?.Invoke(kv.Key, kv.Value);
                }
            }

            // Update the cached contacts by clearing and repopulating the dictionary.
            _previousContactPoints.Clear();
            foreach (KeyValuePair<PhysicsObject, (Vector2f, Vector2f)> kv in _contactPoints)
            {
                _previousContactPoints.Add(kv.Key, kv.Value);
            }

            // Clear the current contact points for the next update cycle.
            _contactPoints.Clear();
        }

        /// <summary>
        /// Moves the object based on its velocity and updates its AABB.
        /// </summary>
        public virtual void Move(float dt)
        {
            if (Locked)
                return;

            RoundSpeedToZero();
            Center += Velocity * dt;
            // Update AABB including current rotation.
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
            // Update AABB including current rotation.
            Aabb = Shape.GetAABB(Center, Angle);
        }

        /// <summary>
        /// Updates rotation. By default, does nothing.
        /// </summary>
        public virtual void UpdateRotation(float dt)
        {
            if (!CanRotate || Locked)
                return;

            Angle += AngularVelocity * dt;
            AngularVelocity *= 0.999f; // Apply angular damping.
            if (Math.Abs(AngularVelocity) < 0.001f)
                AngularVelocity = 0;

            Aabb = Shape.GetAABB(Center, Angle);
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