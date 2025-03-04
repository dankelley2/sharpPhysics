﻿using physics.Engine.Objects;
using physics.Engine.Structs;
using SFML.System;

namespace physics.Engine.Classes
{
    public class Manifold
    {
        public PhysicsObject A;
        public PhysicsObject B;
        public float Penetration;
        public Vector2f Normal;
        public Vector2f ContactPoint;

        public void Reset()
        {
            A = null;
            B = null;
            Penetration = 0;
            Normal = new Vector2f(0, 0);
            ContactPoint = new Vector2f(0, 0);
        }
    }
}
