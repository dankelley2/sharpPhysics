using physics.Engine.Structs;
using physics.Engine.Shapes;
using System;
using System.Collections.Generic;
using physics.Engine.Shaders;
using System.Numerics;

namespace physics.Engine.Objects
{
    public class PhysicsObject
    {
        public static float SupportObjectNormalThreshold = 0.2f;
        public IShape Shape { get; protected set; }
        public AABB Aabb { get; protected set; }
        public Vector2 Center { get; protected set; }
        public Vector2 Velocity { get; set; }
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
        public static float Friction { get; set; }= 0.5f;

        /// <summary>
        /// Orientation in radians.
        /// </summary>
        public float Angle { get; set; }

        // --- New caching and events ---
        // Event fired when a new contact point is added.
        public event Action<PhysicsObject, (Vector2, Vector2)> ContactPointAdded;
        // Event fired when a contact point is removed.
        public event Action<PhysicsObject, (Vector2, Vector2)> ContactPointRemoved;
        // Current contact points. The key is the object, and the value is a tuple of (point, normal).
        private readonly Dictionary<PhysicsObject, (Vector2, Vector2)> _contactPoints = new Dictionary<PhysicsObject, (Vector2, Vector2)>();
        // Cached contacts from the previous update.
        private readonly Dictionary<PhysicsObject, (Vector2, Vector2)> _previousContactPoints = new Dictionary<PhysicsObject, (Vector2, Vector2)>();

        // --- Sleep/Wake state management ---
        public bool Sleeping { get; private set; } = false;
        private float sleepTimer = 0f;

        // Store the previous center to compute displacement.
        private Vector2 _prevCenter;

        // Support network tracking - objects that this object is resting on
        private readonly HashSet<PhysicsObject> _supportingObjects = new HashSet<PhysicsObject>();
        // Objects that are being supported by this object
        private readonly HashSet<PhysicsObject> _supportedObjects = new HashSet<PhysicsObject>();
        // Persistent contact points for sleeping objects
        private readonly Dictionary<PhysicsObject, (Vector2, Vector2)> _sleepingContactPoints = new Dictionary<PhysicsObject, (Vector2, Vector2)>();
        // Position when going to sleep, to detect if supporting objects have moved
        private Vector2 _sleepPosition;
        // Whether to validate supports next frame
        private bool _validateSupportsNextFrame = false;

        public PhysicsObject(IShape shape, Vector2 center, float restitution, bool locked, SFMLShader shader, float mass = 0, bool canRotate = false)
        {
            Shape = shape;
            Center = center;
            _prevCenter = center; // initialize previous center.
            Angle = 0;
            Velocity = new Vector2(0, 0);
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
                
                // Notify any objects we're supporting if we moved significantly
                if (_supportedObjects.Count > 0 && (Center - _prevCenter).Length() > 0.01f)
                {
                    foreach (var supported in _supportedObjects)
                    {
                        supported._validateSupportsNextFrame = true;
                    }
                }
            }
            else if (_validateSupportsNextFrame)
            {
                // Check if supports are still valid
                ValidateSupports();
                _validateSupportsNextFrame = false;
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
            if (displacement < PhysicsSystem.LinearSleepThreshold &&
                Math.Abs(AngularVelocity) < PhysicsSystem.AngularSleepThreshold)
            {
                sleepTimer += dt;
                if (sleepTimer >= PhysicsSystem.SleepTimeThreshold)
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
        /// Validates if supports are still in place. If not, wakes the object.
        /// </summary>
        private void ValidateSupports()
        {
            if (!Sleeping)
                return;

            bool shouldWake = false;
            
            // If we have no supports, we should wake up
            if (_supportingObjects.Count == 0)
            {
                shouldWake = true;
            }
            else
            {
                // Check if any supporting object has moved away
                foreach (var supportingObj in _supportingObjects)
                {
                    if (_sleepingContactPoints.TryGetValue(supportingObj, out var contactData))
                    {
                        Vector2 contactPoint = contactData.Item1;
                        Vector2 normal = contactData.Item2;
                        
                        // Simple check: is the point still contained within the supporting object?
                        // A more robust check would recompute the actual contact point
                        if (!supportingObj.Contains(contactPoint))
                        {
                            shouldWake = true;
                            break;
                        }
                    }
                    else
                    {
                        // No contact data for a supporting object is suspicious, wake up
                        shouldWake = true;
                        break;
                    }
                }
            }

            if (shouldWake)
            {
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
            Velocity = new Vector2(0, 0);
            AngularVelocity = 0;
            
            // Store the sleep position
            _sleepPosition = Center;
            
            // Preserve current contacts for sleeping state
            _sleepingContactPoints.Clear();
            foreach (var contact in _previousContactPoints)
            {
                _sleepingContactPoints[contact.Key] = contact.Value;
            }
        }

        /// <summary>
        /// Wakes the object from sleep.
        /// </summary>
        public void Wake()
        {
            if (!Sleeping) return;
            Sleeping = false;
            sleepTimer = 0f;
            
            // Clear the sleeping contacts
            _sleepingContactPoints.Clear();
            
            // Recursively wake objects resting on us
            foreach (var supported in _supportedObjects)
            {
                if (supported.Sleeping)
                {
                    supported.Wake();
                }
            }
        }

        /// <summary>
        /// Safe method to add a contact point to the dictionary.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="point"></param>
        /// <param name="normal"></param>
        public void AddContact(PhysicsObject obj, Vector2 point, Vector2 normal)
        {
            if (!_contactPoints.ContainsKey(obj))
            {
                _contactPoints[obj] = (point, normal);
                
                // If normal points upward, this object might be supporting us
                if (normal.Y > SupportObjectNormalThreshold) // Y is negative when pointing up in many engines
                {
                    _supportingObjects.Add(obj);
                    obj._supportedObjects.Add(this);
                }
            }
        }

        /// <summary>
        /// Retrieve the most recent contact points.
        /// </summary>
        /// <returns></returns>
        public Dictionary<PhysicsObject, (Vector2, Vector2)> GetContacts(){
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

            // Cache the event delegates locally to avoid repeated null checks.
            var addedHandler = ContactPointAdded;
            var removedHandler = ContactPointRemoved;

            // Fire events for newly added contacts.
            foreach (KeyValuePair<PhysicsObject, (Vector2, Vector2)> kv in _contactPoints)
            {
                if (!_previousContactPoints.ContainsKey(kv.Key))
                {
                    addedHandler?.Invoke(kv.Key, kv.Value);
                }
            }

            // Fire events for contacts that were removed.
            foreach (KeyValuePair<PhysicsObject, (Vector2, Vector2)> kv in _previousContactPoints)
            {
                if (!_contactPoints.ContainsKey(kv.Key))
                {
                    removedHandler?.Invoke(kv.Key, kv.Value);
                    
                    // Update support networks when contacts are lost
                    if (_supportingObjects.Contains(kv.Key))
                    {
                        _supportingObjects.Remove(kv.Key);
                        kv.Key._supportedObjects.Remove(this);
                    }
                }
            }

            // Update the cached contacts
            _previousContactPoints.Clear();
            foreach (KeyValuePair<PhysicsObject, (Vector2, Vector2)> kv in _contactPoints)
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
                Velocity = new Vector2(0, 0);
            }
        }

        /// <summary>
        /// Directly translates the object by a given vector.
        /// </summary>
        public virtual void Move(Vector2 dVector)
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
        public bool Contains(Vector2 point)
        {
            return Shape.Contains(point, Center, Angle);
        }
    }
}