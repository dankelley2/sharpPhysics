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

        public PhysicsObject activeObject;
        public Queue<PhysicsObject> removalQueue = new Queue<PhysicsObject>();
        public List<PhysicsObject> staticObjects = new List<PhysicsObject>();

        public PhysicsSystem()
        {
            gravity = new Vec2 { X = 0, Y = .2F };
            friction = .99F;
        }

        public Vec2 gravity { get; set; }
        public Vec2 gravityPoint { get; set; }
        public bool UsePointGravity { get; set; }
        public float friction { get; set; }

        public void Tick(long elapsedTime)
        {
            //accumulator += elapsedTime - frameStart;
            //frameStart = elapsedTime;

            ////Avoid accumulator spiral of death by clamping
            //if (accumulator > 0.2f)
            //    accumulator = 0.2f;

            //while (accumulator > _dt)
            //{
                UpdatePhysics(_dt);
            //    accumulator -= _dt;
            //}
        }

        private void UpdatePhysics(float timeDelta)
        {
            //reset moved state
            for (int i = 0; i < staticObjects.Count; i++)
            {
                staticObjects[i].Moved = false;
            }

            foreach (var objA in staticObjects)
            {
                foreach (var objB in staticObjects.Where(a => !a.Equals(objA)))
                {
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

                ApplyConstants(objA);
                objA.Move();
                objA.Moved = true;
            }

            if (removalQueue.Count > 0)
            {
                staticObjects.Remove(removalQueue.Dequeue());
            }
            
        }

        private void ApplyConstants(PhysicsObject obj)
        {
            if (obj.Locked) { return;}
            
            //AddGravity(obj);
            obj.Velocity *= friction;
            if (obj.Center.Y > 2000 || obj.Center.Y < -2000 || obj.Center.X > 2000 || obj.Center.X < -2000)
            {
                removalQueue.Enqueue(obj);
            }
        }

        private void AddGravity(PhysicsObject obj)
        {
            obj.Velocity = obj.Velocity + GetGravityVector(obj);
        }

        private Vec2 GetGravityVector(PhysicsObject obj)
        {
            return UsePointGravity ? (gravityPoint - obj.Center)/100 : gravity;
        }

        public bool ActivateAtPoint(PointF p)
        {
            activeObject = CheckObjectAtPoint(p);
            return activeObject != null && activeObject.Locked == false;
        }

        public void ReleaseActiveObject()
        {
            activeObject = null;
        }

        public void MoveActiveTowardsPoint(Vec2 point)
        {
            if (activeObject == null)
            {
                return;
            }

            var delta = activeObject.Center - point;
            AddVelocityToActive(-delta / 100);
        }

        public void SetGravityPoint(Vec2 point)
        {
            gravityPoint = point;
        }

        public PointF getActiveObjectCenter()
        {
            if (activeObject == null)
            {
                return new PointF();
            }

            return new PointF(activeObject.Center.X, activeObject.Center.Y);
        }

        public void removeActiveObject()
        {
            staticObjects.Remove(activeObject);
            activeObject = null;
        }

        public void freezeAll()
        {
            foreach (var physicsObject in staticObjects)
            {
                physicsObject.Velocity = new Vec2 {X=0,Y=0};
            }
        }

        public void AddVelocityToActive(Vec2 velocityDelta)
        {
            if (activeObject == null)
            {
                return;
            }

            activeObject.Velocity += velocityDelta;
        }

        private PhysicsObject CheckObjectAtPoint(PointF p)
        {
            foreach (var physicsObject in staticObjects)
            {
                if (physicsObject.Contains(p))
                {
                    return physicsObject;
                }
            }

            return null;
        }
    }

    public static class ExtensionMethods
    {
        public static float Remap(this float value, float from1, float to1, float from2, float to2)
        {
            return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
        }
        public static int Clamp(this int value, int min, int max)
        {
            return (value < min) ? min : (value > max) ? max : value;
        }
    }

}