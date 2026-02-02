
using SharpPhysics.Engine.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

#pragma warning disable CS8767

namespace SharpPhysics.Engine.Classes
{
    public sealed class CollisionPair : IEquatable<CollisionPair>
    {
        public PhysicsObject A { get; private set; }
        public PhysicsObject B { get; private set; }

        public CollisionPair(PhysicsObject A, PhysicsObject B)
        {
            this.A = A;
            this.B = B;
        }

        public void Set(PhysicsObject a, PhysicsObject b)
        {
            A = a;
            B = b;
        }

        public void Reset()
        {
            A = null;
            B = null;
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
            return left is not null && left.Equals(right);
        }

        public static bool operator !=(CollisionPair left, CollisionPair right)
        {
            return !(left == right);
        }

    }
}
#pragma warning restore CS8767
