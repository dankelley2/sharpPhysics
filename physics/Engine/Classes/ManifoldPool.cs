using System.Collections.Concurrent;

namespace physics.Engine.Classes
{
    public class ManifoldPool
    {
        private readonly ConcurrentStack<Manifold> _pool = new ConcurrentStack<Manifold>();

        public Manifold Get()
        {
            if (_pool.TryPop(out Manifold m))
            {
                m.Reset();
                return m;
            }
            return new Manifold();
        }

        public void Return(Manifold m)
        {
            m.Reset();
            _pool.Push(m);
        }
    }
}
