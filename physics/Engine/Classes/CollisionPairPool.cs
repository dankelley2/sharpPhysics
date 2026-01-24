using System.Collections.Generic;
using physics.Engine.Objects;

namespace physics.Engine.Classes
{
    public class CollisionPairPool
    {
        private readonly Stack<CollisionPair> _pool = new Stack<CollisionPair>();

        public CollisionPair Get(PhysicsObject a, PhysicsObject b)
        {
            if (_pool.Count > 0)
            {
                CollisionPair pair = _pool.Pop();
                pair.Set(a, b);
                return pair;
            }
            return new CollisionPair(a, b);
        }

        public void Return(CollisionPair pair)
        {
            pair.Reset();
            _pool.Push(pair);
        }

        public void ReturnAll(List<CollisionPair> pairs)
        {
            for (int i = 0; i < pairs.Count; i++)
            {
                Return(pairs[i]);
            }
        }
    }
}
