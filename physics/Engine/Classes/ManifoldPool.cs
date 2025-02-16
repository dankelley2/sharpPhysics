using System.Collections.Generic;

namespace physics.Engine.Classes
{
    public class ManifoldPool
    {
        private readonly Stack<Manifold> _pool = new Stack<Manifold>();

        public Manifold Get()
        {
            if (_pool.Count > 0)
            {
                Manifold m = _pool.Pop();
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
