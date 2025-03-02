
using physics.Engine.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace physics.Engine.Classes
{
    public class CollisionPair : IEquatable<CollisionPair>
    {
        public readonly PhysicsObject A;
        public readonly PhysicsObject B;

        public CollisionPair(PhysicsObject A, PhysicsObject B)
        {
            this.A = A;
            this.B = B;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CollisionPair);
        }

        public bool Equals(CollisionPair other)
        {
            if (other == null)
                return false;

            return (this.A.Equals(other.A) && this.B.Equals(other.B)) ||
                   (this.A.Equals(other.B) && this.B.Equals(other.A));
        }

        public override int GetHashCode()
        {
            int hashA = A.GetHashCode();
            int hashB = B.GetHashCode();
            return hashA ^ hashB;
        }

        public static bool operator ==(CollisionPair left, CollisionPair right)
        {
            if (left.A.Aabb.Min == right.A.Aabb.Min && left.A.Aabb.Max == right.A.Aabb.Max &&
                left.B.Aabb.Min == right.B.Aabb.Min && left.B.Aabb.Max == right.B.Aabb.Max ||

                left.A.Aabb.Min == right.B.Aabb.Min && left.A.Aabb.Max == right.B.Aabb.Max &&
                left.B.Aabb.Min == right.A.Aabb.Min && left.B.Aabb.Max == right.A.Aabb.Max)
            {
                return true;
            }

            return false;
        }

        public static bool operator !=(CollisionPair left, CollisionPair right)
        {
            if (left.A.Aabb.Min == right.A.Aabb.Min && left.A.Aabb.Max == right.A.Aabb.Max &&
                left.B.Aabb.Min == right.B.Aabb.Min && left.B.Aabb.Max == right.B.Aabb.Max ||

                left.A.Aabb.Min == right.B.Aabb.Min && left.A.Aabb.Max == right.B.Aabb.Max &&
                left.B.Aabb.Min == right.A.Aabb.Min && left.B.Aabb.Max == right.A.Aabb.Max)
            {
                return false;
            }

            return true;
        }

    }
}
