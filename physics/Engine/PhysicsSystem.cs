using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using physics.Engine.Classes;
using physics.Engine.Structs;

namespace physics.Engine
{
    internal class PhysicsSystem
    {

        public const float FPS = 10;
        private const float _dt = 1 / FPS;
        private float accumulator = 0;
        private long frameStart = 0;

        public PhysicsObject ActiveObject;
        public Queue<PhysicsObject> RemovalQueue = new Queue<PhysicsObject>();
        public List<PhysicsObject> StaticObjects = new List<PhysicsObject>();
        public List<CollisionPair> Pairs = new List<CollisionPair>();

        public Vec2 Gravity { get; set; }
        public Vec2 GravityPoint { get; set; }
        public bool UsePointGravity { get; set; }
        public float Friction { get; set; }

        public PhysicsSystem()
        {
            Gravity = new Vec2 { X = 0, Y = .2F };
            Friction = .99F;
        }

        public void Tick(long elapsedTime)
        {
            //accumulator += elapsedTime - frameStart;
            //frameStart = elapsedTime;

            ////Avoid accumulator spiral of death by clamping
            //if (accumulator > 0.2f)
            //    accumulator = 0.2f;

            //while (accumulator > _dt)
            //{
            BroadPhase_GeneratePairs();
            UpdatePhysics(_dt);
            RemoveOutOfScopeObjects();
            //    accumulator -= _dt;
            //}
        }

        private void BroadPhase_GeneratePairs()
        {
            Pairs.Clear();

            AABB a_bb;
            AABB b_bb;

            PhysicsObject A;
            PhysicsObject B;

            for (int i = 0; i < StaticObjects.Count; i++)
            {
                for (int j = i; j < StaticObjects.Count; j++)
                {
                    if (j == i) { continue; }

                    A = StaticObjects[i];
                    B = StaticObjects[j];

                    a_bb = A.Aabb;
                    b_bb = B.Aabb;

                    if (Collision.AABBvsAABB(a_bb, b_bb))
                    {
                        Pairs.Add(new CollisionPair(A,B));
                    }
                }
            }
        }

        private void UpdatePhysics(float timeDelta)
        {
            foreach (var pair in Pairs)
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
                }
            }

            for (int i = 0; i < StaticObjects.Count; i++)
            {
                ApplyConstants(StaticObjects[i]);
                StaticObjects[i].Move();
            }
        }


        private void RemoveOutOfScopeObjects()
        {
            if (RemovalQueue.Count > 0)
            {
                StaticObjects.Remove(RemovalQueue.Dequeue());
            }
        }

        private void ApplyConstants(PhysicsObject obj)
        {
            if (obj.Locked) { return;}
            
            //AddGravity(obj);
            obj.Velocity *= Friction;
            if (obj.Center.Y > 2000 || obj.Center.Y < -2000 || obj.Center.X > 2000 || obj.Center.X < -2000)
            {
                RemovalQueue.Enqueue(obj);
            }
        }

        private void AddGravity(PhysicsObject obj)
        {
            obj.Velocity = obj.Velocity + GetGravityVector(obj);
        }

        #region NonEssentials

        private Vec2 GetGravityVector(PhysicsObject obj)
        {
            return UsePointGravity ? (GravityPoint - obj.Center) / 100 : Gravity;
        }

        public bool ActivateAtPoint(PointF p)
        {
            ActiveObject = CheckObjectAtPoint(p);
            return ActiveObject != null && ActiveObject.Locked == false;
        }

        public void ReleaseActiveObject()
        {
            ActiveObject = null;
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

        public void SetGravityPoint(Vec2 point)
        {
            GravityPoint = point;
        }

        public PointF getActiveObjectCenter()
        {
            if (ActiveObject == null)
            {
                return new PointF();
            }

            return new PointF(ActiveObject.Center.X, ActiveObject.Center.Y);
        }

        public void removeActiveObject()
        {
            StaticObjects.Remove(ActiveObject);
            ActiveObject = null;
        }

        public void freezeAll()
        {
            foreach (var physicsObject in StaticObjects)
            {
                physicsObject.Velocity = new Vec2 { X = 0, Y = 0 };
            }
        }

        public void AddVelocityToActive(Vec2 velocityDelta)
        {
            if (ActiveObject == null)
            {
                return;
            }

            ActiveObject.Velocity += velocityDelta;
        }

        private PhysicsObject CheckObjectAtPoint(PointF p)
        {
            foreach (var physicsObject in StaticObjects)
            {
                if (physicsObject.Contains(p))
                {
                    return physicsObject;
                }
            }

            return null;
        }

        #endregion
    }

}