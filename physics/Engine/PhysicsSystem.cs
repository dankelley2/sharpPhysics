using System;
using System.Collections.Generic;
using System.Drawing;
using physics.Engine.Classes;
using physics.Engine.Helpers;
using physics.Engine.Structs;

namespace physics.Engine
{
    public class PhysicsSystem
    {
        #region Public Properties

        public Vec2 Gravity { get; set; }

        public float Friction { get; set; }

        #endregion

        #region Local Declarations

        public static PhysicsObject ActiveObject;

        public static readonly List<aShader> ListShaders = new List<aShader>();

        public static readonly List<CollisionPair> ListCollisionPairs = new List<CollisionPair>();
               
        public static readonly List<PhysicsObject> ListGravityObjects = new List<PhysicsObject>();
               
        public static readonly List<PhysicsObject> ListStaticObjects = new List<PhysicsObject>();
               
        public static readonly Queue<PhysicsObject> RemovalQueue = new Queue<PhysicsObject>();

        #endregion

        #region Constructors

        public PhysicsSystem()
        {
            Gravity = new Vec2 {X = 0, Y = .1F};
            Friction = .995F;
        }

        #endregion

        #region Public Methods


        public static PhysicsObject CreateStaticCircle(Vec2 loc, int radius, float restitution, bool locked, aShader shader)
        {
            var oAabb = new AABB
            {
                Min = new Vec2 { X = loc.X - radius, Y = loc.Y - radius },
                Max = new Vec2 { X = loc.X + radius, Y = loc.Y + radius }
            };
            PhysMath.CorrectBoundingBox(ref oAabb);
            var obj = new PhysicsObject(oAabb, PhysicsObject.Type.Circle, restitution, locked, shader);
            ListStaticObjects.Add(obj);
            return obj;
        }

        public static PhysicsObject CreateStaticBox(Vec2 start, Vec2 end, bool locked, aShader shader, float mass)
        {
            var oAabb = new AABB
            {
                Min = new Vec2 { X = start.X, Y = start.Y },
                Max = new Vec2 { X = end.X, Y = end.Y }
            };
            PhysMath.CorrectBoundingBox(ref oAabb);
            var obj = new PhysicsObject(oAabb, PhysicsObject.Type.Box, .95F, locked, shader, mass);
            ListStaticObjects.Add(obj);
            return obj;
        }

        public bool ActivateAtPoint(PointF p)
        {
            ActiveObject = CheckObjectAtPoint(p);

            if (ActiveObject == null || ActiveObject.Locked)
            {
                ActiveObject = null;
                return false;
            }

            return true;
        }

        public void AddVelocityToActive(Vec2 velocityDelta)
        {
            if (ActiveObject == null)
            {
                return;
            }

            ActiveObject.Velocity += velocityDelta;
        }

        public void FreezeStaticObjects()
        {
            foreach (var physicsObject in ListStaticObjects)
            {
                physicsObject.Velocity = new Vec2 {X = 0, Y = 0};
            }
        }

        public PointF GetActiveObjectCenter()
        {
            if (ActiveObject == null)
            {
                return new PointF();
            }

            return new PointF(ActiveObject.Center.X, ActiveObject.Center.Y);
        }

        public void MoveActiveTowardsPoint(Vec2 point)
        {
            if (ActiveObject == null)
            {
                return;
            }

            var delta = ActiveObject.Center - point;
            AddVelocityToActive(-delta / 100);
        }

        public void ReleaseActiveObject()
        {
            ActiveObject = null;
        }

        public void RemoveActiveObject()
        {
            ListStaticObjects.Remove(ActiveObject);
            ActiveObject = null;
        }

        public void Tick(long elapsedTime)
        {
            BroadPhase_GeneratePairs();
            UpdatePhysics();
            RemoveOutOfScopeObjects();
        }

        #endregion

        #region Private Methods

        private void AddGravity(PhysicsObject obj)
        {
            obj.Velocity += GetGravityVector(obj);
        }

        private void ApplyConstants(PhysicsObject obj)
        {
            if (obj.Locked)
            {
                return;
            }

            AddGravity(obj);
            obj.Velocity *= Friction;

            if (obj.Center.Y > 2000 || obj.Center.Y < -2000 || obj.Center.X > 2000 || obj.Center.X < -2000)
            {
                RemovalQueue.Enqueue(obj);
            }
        }

        private Vec2 CalculatePointGravity(PhysicsObject obj)
        {
            var forces = new Vec2(0, 0);

            if (obj.Locked)
            {
                return forces;
            }

            foreach (var gpt in ListGravityObjects)
            {
                var diff = gpt.Center - obj.Center;
                PhysMath.RoundToZero(ref diff, 5F);

                //apply inverse square law
                var falloffMultiplier = (gpt.Mass / 1000) / diff.LengthSquared;

                diff.X = (int) diff.X == 0 ? 0 : diff.X * falloffMultiplier;
                diff.Y = (int) diff.Y == 0 ? 0 : diff.Y * falloffMultiplier;

                if (diff.Length > .005F)
                {
                    forces += diff;
                }
            }

            return forces;
        }

        private PhysicsObject CheckObjectAtPoint(PointF p)
        {
            foreach (var physicsObject in ListStaticObjects)
            {
                if (physicsObject.Contains(p))
                {
                    return physicsObject;
                }
            }

            return null;
        }

        private Vec2 GetGravityVector(PhysicsObject obj)
        {
            return CalculatePointGravity(obj) + Gravity;
        }

        private void RemoveOutOfScopeObjects()
        {
            if (RemovalQueue.Count > 0)
            {
                ListStaticObjects.Remove(RemovalQueue.Dequeue());
            }
        }

        private void UpdatePhysics()
        {
            foreach (var pair in ListCollisionPairs)
            {
                var objA = pair.A;
                var objB = pair.B;

                var m = new Manifold();
                var collision = false;

                if (objA.ShapeType == PhysicsObject.Type.Circle && objB.ShapeType == PhysicsObject.Type.Box)
                {
                    m.A = objB;
                    m.B = objA;
                }
                else
                {
                    m.A = objA;
                    m.B = objB;
                }

                //Box vs anything
                if (m.A.ShapeType == PhysicsObject.Type.Box)
                {
                    if (m.B.ShapeType == PhysicsObject.Type.Box)
                    {
                        //continue;
                        if (Collision.AABBvsAABB(ref m))
                        {
                            collision = true;
                        }
                    }

                    if (m.B.ShapeType == PhysicsObject.Type.Circle)
                    {
                        if (Collision.AABBvsCircle(ref m))
                        {
                            collision = true;
                        }
                    }
                }

                //Circle Circle
                else
                {
                    if (m.B.ShapeType == PhysicsObject.Type.Circle)
                    {
                        if (Collision.CirclevsCircle(ref m))
                        {
                            collision = true;
                        }
                    }
                }

                //Resolve Collision
                if (collision)
                {
                    Collision.ResolveCollision(ref m);
                    Collision.PositionalCorrection(ref m);
                    objA.LastCollision = m;
                    objB.LastCollision = m;
                }
            }

            for (var i = 0; i < ListStaticObjects.Count; i++)
            {
                ApplyConstants(ListStaticObjects[i]);
                ListStaticObjects[i].Move();
            }
        }

        #endregion

        #region Private Events

        private void BroadPhase_GeneratePairs()
        {
            ListCollisionPairs.Clear();

            AABB a_bb;
            AABB b_bb;

            PhysicsObject A;
            PhysicsObject B;

            for (var i = 0; i < ListStaticObjects.Count; i++)
            {
                ListStaticObjects[i].LastCollision = null;
                for (var j = i; j < ListStaticObjects.Count; j++)
                {
                    if (j == i)
                    {
                        continue;
                    }

                    A = ListStaticObjects[i];
                    B = ListStaticObjects[j];

                    a_bb = A.Aabb;
                    b_bb = B.Aabb;

                    if (Collision.AABBvsAABB(a_bb, b_bb))
                    {
                        ListCollisionPairs.Add(new CollisionPair(A, B));
                    }
                }
            }
        }

        #endregion
    }
}
