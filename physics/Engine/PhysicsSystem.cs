using System;
using System.Collections.Generic;
using System.Linq;
using SFML.System;
using physics.Engine.Classes;
using physics.Engine.Classes.ObjectTemplates;
using physics.Engine.Helpers;
using physics.Engine.Structs;

namespace physics.Engine
{
    public class PhysicsSystem
    {
        public static readonly List<PhysicsObject> ListStaticObjects = new List<PhysicsObject>();
        public static readonly List<PhysicsObject> ListGravityObjects = new List<PhysicsObject>();
        public static PhysicsObject ActiveObject;
        public static readonly List<SFMLShader> ListShaders = new List<SFMLShader>();
        public static readonly List<CollisionPair> ListCollisionPairs = new List<CollisionPair>();

        internal IEnumerable<PhysicsObject> GetMoveableObjects()
        {
            return ListStaticObjects.Where(x => !x.Locked);
        }

        public bool ActivateAtPoint(Vector2f p)
        {
            foreach (PhysicsObject obj in ListStaticObjects)
            {
                if (obj.Contains(p))
                {
                    ActiveObject = obj;
                    return true;
                }
            }
            return false;
        }

        public void ReleaseActiveObject()
        {
            ActiveObject = null;
        }

        public void RemoveActiveObject()
        {
            if (ActiveObject != null)
            {
                ListStaticObjects.Remove(ActiveObject);
                ActiveObject = null;
            }
        }

        public void HoldActiveAtPoint(Vec2 p)
        {
            if (ActiveObject != null)
            {
                Vec2 delta = p - ActiveObject.Center;
                ActiveObject.Move(delta);
            }
        }

        public void AddVelocityToActive(Vec2 v)
        {
            if (ActiveObject != null)
            {
                ActiveObject.Velocity = v;
            }
        }

        public void SetVelocityOfActive(Vec2 v)
        {
            if (ActiveObject != null)
            {
                ActiveObject.Velocity = v;
            }
        }

        public void SetVelocity(PhysicsObject obj, Vec2 v)
        {
            obj.Velocity = v;
        }

        public void FreezeStaticObjects()
        {
            foreach (PhysicsObject obj in ListStaticObjects)
            {
                obj.Velocity = new Vec2(0, 0);
            }
        }

        public void Tick(double elapsedTime)
        {
            foreach (PhysicsObject obj in ListStaticObjects)
            {
                obj.Move((float)elapsedTime);
            }

            ListCollisionPairs.Clear();
            for (int i = 0; i < ListStaticObjects.Count; i++)
            {
                for (int j = i + 1; j < ListStaticObjects.Count; j++)
                {
                    ListCollisionPairs.Add(new CollisionPair(ListStaticObjects[i], ListStaticObjects[j]));
                }
            }

            foreach (CollisionPair pair in ListCollisionPairs)
            {
                Collision.ResolveCollision(pair);
            }

            foreach (PhysicsObject obj in ListGravityObjects)
            {
                foreach (PhysicsObject obj2 in ListStaticObjects)
                {
                    if (obj != obj2)
                    {
                        Vec2 delta = obj.Center - obj2.Center;
                        float distance = delta.Length;
                        if (distance < 200)
                        {
                            float force = 1 / (distance * distance) * 100000;
                            Vec2 direction = Vec2.Normalize(delta);
                            obj2.Velocity += direction * force * (float)elapsedTime;
                        }
                    }
                }
            }
        }

        public static PhysicsObject CreateStaticCircle(Vec2 loc, int radius, float restitution, bool locked, SFMLShader shader)
        {
            var oAabb = new AABB
            {
                Min = new Vec2 { X = loc.X - radius, Y = loc.Y - radius },
                Max = new Vec2 { X = loc.X + radius, Y = loc.Y + radius }
            };
            var obj = new PhysicsObject(oAabb, PhysicsObject.Type.Circle, restitution, locked, shader);
            ListStaticObjects.Add(obj);
            return obj;
        }

        public static PhysicsObject CreateStaticBox(Vec2 start, Vec2 end, bool locked, SFMLShader shader, float mass)
        {
            var oAabb = new AABB
            {
                Min = new Vec2 { X = start.X, Y = start.Y },
                Max = new Vec2 { X = end.X, Y = end.Y }
            };
            var obj = new PhysicsObject(oAabb, PhysicsObject.Type.Box, 0.8f, locked, shader, mass);
            ListStaticObjects.Add(obj);
            return obj;
        }
    }
}
