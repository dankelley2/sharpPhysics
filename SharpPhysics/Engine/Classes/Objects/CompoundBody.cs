using System.Collections.Generic;
using System.Numerics;
using SharpPhysics.Engine.Constraints;

namespace SharpPhysics.Engine.Objects
{
    /// <summary>
    /// Represents a compound physics body made up of multiple PhysicsObjects
    /// connected by constraints. Used for concave polygon decomposition.
    /// </summary>
    public class CompoundBody
    {
        /// <summary>
        /// All physics objects that make up this compound body.
        /// </summary>
        public List<PhysicsObject> Parts { get; } = new List<PhysicsObject>();

        /// <summary>
        /// All constraints connecting the parts together.
        /// </summary>
        public List<Constraint> Constraints { get; } = new List<Constraint>();

        /// <summary>
        /// The primary/root part of the compound body (typically the first or largest piece).
        /// Use this for applying forces or getting approximate position.
        /// </summary>
        public PhysicsObject? RootPart => Parts.Count > 0 ? Parts[0] : null;

        /// <summary>
        /// Gets the approximate center of mass of the compound body.
        /// </summary>
        public Vector2 Center
        {
            get
            {
                if (Parts.Count == 0)
                    return Vector2.Zero;

                Vector2 weightedSum = Vector2.Zero;
                float totalMass = 0f;

                foreach (var part in Parts)
                {
                    weightedSum += part.Center * part.Mass;
                    totalMass += part.Mass;
                }

                return totalMass > 0 ? weightedSum / totalMass : Parts[0].Center;
            }
        }

        /// <summary>
        /// Gets the total mass of all parts.
        /// </summary>
        public float TotalMass
        {
            get
            {
                float mass = 0f;
                foreach (var part in Parts)
                    mass += part.Mass;
                return mass;
            }
        }

        /// <summary>
        /// Locks all parts of the compound body.
        /// </summary>
        public void Lock()
        {
            foreach (var part in Parts)
                part.Locked = true;
        }

        /// <summary>
        /// Unlocks all parts of the compound body.
        /// </summary>
        public void Unlock()
        {
            foreach (var part in Parts)
                part.Locked = false;
        }

        /// <summary>
        /// Wakes all parts of the compound body from sleep.
        /// </summary>
        public void Wake()
        {
            foreach (var part in Parts)
            {
                if (part.Sleeping)
                    part.Wake();
            }
        }

        /// <summary>
        /// Applies a velocity to all parts of the compound body.
        /// </summary>
        public void SetVelocity(Vector2 velocity)
        {
            foreach (var part in Parts)
                part.Velocity = velocity;
        }

        /// <summary>
        /// Adds velocity to all parts of the compound body.
        /// </summary>
        public void AddVelocity(Vector2 velocityDelta)
        {
            foreach (var part in Parts)
                part.Velocity += velocityDelta;
        }

        /// <summary>
        /// Checks if any constraint in the compound body has broken.
        /// </summary>
        public bool HasBrokenConstraints()
        {
            foreach (var constraint in Constraints)
            {
                if (constraint.IsBroken)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Gets a list of parts that have separated due to broken constraints.
        /// </summary>
        public List<PhysicsObject> GetSeparatedParts()
        {
            var separated = new List<PhysicsObject>();
            var connected = new HashSet<PhysicsObject>();

            if (Parts.Count == 0)
                return separated;

            // BFS from root to find all still-connected parts
            var queue = new Queue<PhysicsObject>();
            queue.Enqueue(Parts[0]);
            connected.Add(Parts[0]);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                foreach (var constraint in Constraints)
                {
                    if (constraint.IsBroken)
                        continue;

                    PhysicsObject? neighbor = null;
                    if (constraint.A == current && !connected.Contains(constraint.B))
                        neighbor = constraint.B;
                    else if (constraint.B == current && !connected.Contains(constraint.A))
                        neighbor = constraint.A;

                    if (neighbor != null)
                    {
                        connected.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }

            // Any part not in connected set is separated
            foreach (var part in Parts)
            {
                if (!connected.Contains(part))
                    separated.Add(part);
            }

            return separated;
        }
    }
}
