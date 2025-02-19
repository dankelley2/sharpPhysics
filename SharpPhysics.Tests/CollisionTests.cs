using Microsoft.VisualStudio.TestTools.UnitTesting;
using SFML.System;
using physics.Engine;
using physics.Engine.Shapes;
using physics.Engine.Structs;
using System;
using physics.Engine.Classes;
using physics.Engine.Objects;

namespace SharpPhysics.Tests
{
    [TestClass]
    public class CollisionTests
    {
        #region AABB vs AABB (non‑manifold version)

        [TestMethod]
        public void Test_AABBvsAABB_NoOverlap()
        {
            AABB a = new AABB { Min = new Vector2f(0, 0), Max = new Vector2f(10, 10) };
            AABB b = new AABB { Min = new Vector2f(20, 20), Max = new Vector2f(30, 30) };
            bool collides = Collision.AABBvsAABB(a, b);
            Assert.IsFalse(collides, "Non-overlapping AABBs should not collide.");
        }

        [TestMethod]
        public void Test_AABBvsAABB_Overlap()
        {
            AABB a = new AABB { Min = new Vector2f(0, 0), Max = new Vector2f(10, 10) };
            AABB b = new AABB { Min = new Vector2f(5, 5), Max = new Vector2f(15, 15) };
            bool collides = Collision.AABBvsAABB(a, b);
            Assert.IsTrue(collides, "Overlapping AABBs should collide.");
        }

        #endregion

        #region AABB vs AABB (manifold version)

        [TestMethod]
        public void Test_AABBvsAABB_Manifold_NoRotation()
        {
            // Box A: AABB from (0,0) to (10,10) => center (5,5), width 10, height 10.
            // Box B: AABB from (5,5) to (15,15) => center (10,10), width 10, height 10.
            AABB aabbA = new AABB { Min = new Vector2f(0, 0), Max = new Vector2f(10, 10) };
            AABB aabbB = new AABB { Min = new Vector2f(5, 5), Max = new Vector2f(15, 15) };

            PhysicsObject A = new PhysicsObject(
                new BoxPhysShape(10, 10),
                new Vector2f(5, 5),
                0.6f, false, null, canRotate: true);

            PhysicsObject B = new PhysicsObject(
                new BoxPhysShape(10, 10),
                new Vector2f(10, 10),
                0.6f, false, null, canRotate: true);

            // No rotation.
            A.Angle = 0;
            B.Angle = 0;
            Manifold m = new Manifold { A = A, B = B };

            bool collides = Collision.AABBvsAABB(ref m);
            Assert.IsTrue(collides, "Boxes should collide.");
            Assert.IsTrue(m.Penetration > 0, "Penetration must be positive.");
            // Expect contact point to be the midpoint between centers.
            Vector2f expectedContact = (A.Center + B.Center) * 0.5f;
            Assert.AreEqual(expectedContact.X, m.ContactPoint.X, 0.001, "Contact point X mismatch.");
            Assert.AreEqual(expectedContact.Y, m.ContactPoint.Y, 0.001, "Contact point Y mismatch.");
        }

        #endregion

        #region Circle vs Circle

        [TestMethod]
        public void Test_CirclevsCircle_Overlap()
        {
            // Circle A: AABB from (0,0) to (10,10) => center (5,5), radius = 5.
            // Circle B: AABB from (7,7) to (17,17) => center (12,12), radius = 5.
            AABB aabbA = new AABB { Min = new Vector2f(0, 0), Max = new Vector2f(10, 10) };
            AABB aabbB = new AABB { Min = new Vector2f(7, 7), Max = new Vector2f(17, 17) };

            PhysicsObject A = new PhysicsObject(
                new CirclePhysShape(5),
                new Vector2f(5, 5),
                0.6f, false, null, canRotate: true);

            PhysicsObject B = new PhysicsObject(
                new CirclePhysShape(5),
                new Vector2f(12, 12),
                0.6f, false, null, canRotate: true);

            // For circles, rotation is irrelevant.
            A.Angle = 0;
            B.Angle = 0;
            Manifold m = new Manifold { A = A, B = B };

            bool collides = Collision.CirclevsCircle(ref m);
            Assert.IsTrue(collides, "Circles should collide.");
            Assert.IsTrue(m.Penetration > 0, "Penetration should be positive.");
            // Check that contact point was computed.
            Assert.IsTrue(m.ContactPoint.X != 0 || m.ContactPoint.Y != 0, "Contact point should be nonzero.");
        }

        #endregion

        #region AABB vs Circle

        [TestMethod]
        public void Test_AABBvsCircle_Overlap()
        {
            // Box: AABB from (0,0) to (20,20) => center (10,10), width=20, height=20.
            // Circle: AABB from (15,15) to (25,25) => center (20,20), radius = 5.
            AABB aabbBox = new AABB { Min = new Vector2f(0, 0), Max = new Vector2f(20, 20) };
            AABB aabbCircle = new AABB { Min = new Vector2f(15, 15), Max = new Vector2f(25, 25) };

            PhysicsObject box = new PhysicsObject(
                new BoxPhysShape(20, 20),
                new Vector2f(10, 10),
                0.6f, false, null, canRotate: true);

            PhysicsObject circle = new PhysicsObject(
                new CirclePhysShape(5),
                new Vector2f(20, 20),
                0.6f, false, null, canRotate: true);

            // Use 0 rotation for clarity.
            box.Angle = 0;
            circle.Angle = 0;
            Manifold m = new Manifold { A = box, B = circle };

            bool collides = Collision.AABBvsCircle(ref m);
            Assert.IsTrue(collides, "Box and circle should collide.");
            Assert.IsTrue(m.Penetration > 0, "Penetration should be positive.");
        }

        #endregion

        #region ResolveCollision (linear)

        [TestMethod]
        public void Test_ResolveCollision()
        {
            // Two boxes with opposing velocities.
            // Box A: AABB from (0,0) to (10,10) => center (5,5)
            // Box B: AABB from (8,8) to (18,18) => center (13,13)
            AABB aabbA = new AABB { Min = new Vector2f(0, 0), Max = new Vector2f(10, 10) };
            AABB aabbB = new AABB { Min = new Vector2f(8, 8), Max = new Vector2f(18, 18) };

            PhysicsObject A = new PhysicsObject(
                new BoxPhysShape(10, 10),
                new Vector2f(5, 5),
                0.8f, false, null, canRotate: true);

            PhysicsObject B = new PhysicsObject(
                new BoxPhysShape(10, 10),
                new Vector2f(13, 13),
                0.8f, false, null, canRotate: true);

            A.Velocity = new Vector2f(5, 0);
            B.Velocity = new Vector2f(-5, 0);
            A.Angle = 0;
            B.Angle = 0;
            Manifold m = new Manifold { A = A, B = B };
            // Simulate collision details:
            m.Normal = new Vector2f(1, 0);
            m.Penetration = 2;
            m.ContactPoint = (A.Center + B.Center) * 0.5f;

            Vector2f initialAVel = A.Velocity;
            Vector2f initialBVel = B.Velocity;

            Collision.ResolveCollision(ref m);

            Assert.AreNotEqual(initialAVel.X, A.Velocity.X, "A's velocity X should change after collision resolution.");
            Assert.AreNotEqual(initialBVel.X, B.Velocity.X, "B's velocity X should change after collision resolution.");
        }

        #endregion

        #region ResolveCollisionRotational

        [TestMethod]
        public void Test_ResolveCollisionRotational()
        {
            // Two circles with some angular velocity.
            // Circle A: center (5,5), radius=5.
            // Circle B: center (13,13), radius=5.
            PhysicsObject A = new PhysicsObject(
                new CirclePhysShape(5),
                new Vector2f(5, 5),
                0.8f, false, null, canRotate: true);

            PhysicsObject B = new PhysicsObject(
                new CirclePhysShape(5),
                new Vector2f(13, 13),
                0.8f, false, null, canRotate: true);

            A.AngularVelocity = 1.0f;
            B.AngularVelocity = -1.0f;
            A.Velocity = new Vector2f(2, 0);
            B.Velocity = new Vector2f(-2, 0);
            A.Angle = 0;
            B.Angle = 0;
            Manifold m = new Manifold { A = A, B = B };
            m.Normal = new Vector2f(1, 0);
            m.ContactPoint = (A.Center + B.Center) * 0.5f;
            m.Penetration = 2;

            float initialAAngVel = A.AngularVelocity;
            float initialBAngVel = B.AngularVelocity;

            Collision.ResolveCollisionRotational(ref m);

            Assert.AreNotEqual(initialAAngVel, A.AngularVelocity, "A's angular velocity should change after rotational resolution.");
            Assert.AreNotEqual(initialBAngVel, B.AngularVelocity, "B's angular velocity should change after rotational resolution.");
        }


        #endregion

        #region PositionalCorrection

        [TestMethod]
        public void Test_PositionalCorrection()
        {
            // Two overlapping boxes.
            // Box A: AABB from (0,0) to (10,10) => center (5,5)
            // Box B: AABB from (8,8) to (18,18) => center (13,13)
            AABB aabbA = new AABB { Min = new Vector2f(0, 0), Max = new Vector2f(10, 10) };
            AABB aabbB = new AABB { Min = new Vector2f(8, 8), Max = new Vector2f(18, 18) };

            PhysicsObject A = new PhysicsObject(
                new BoxPhysShape(10, 10),
                new Vector2f(5, 5),
                0.8f, false, null, canRotate: true);

            PhysicsObject B = new PhysicsObject(
                new BoxPhysShape(10, 10),
                new Vector2f(13, 13),
                0.8f, false, null, canRotate: true);

            A.Angle = 0;
            B.Angle = 0;
            Manifold m = new Manifold { A = A, B = B };
            m.Normal = new Vector2f(1, 0);
            m.Penetration = 2;
            Vector2f initialACenter = A.Center;
            Vector2f initialBCenter = B.Center;

            Collision.PositionalCorrection(ref m);

            // Expect the boxes to be moved apart along the normal.
            Assert.IsTrue(A.Center.X < initialACenter.X, "A should move opposite the normal.");
            Assert.IsTrue(B.Center.X > initialBCenter.X, "B should move along the normal.");
        }

        #endregion

        #region AngularPositionalCorrection

        [TestMethod]
        public void Test_AngularPositionalCorrection()
        {
            // Two boxes for which angular positional correction is applied.
            AABB aabbA = new AABB { Min = new Vector2f(0, 0), Max = new Vector2f(10, 10) };
            AABB aabbB = new AABB { Min = new Vector2f(8, 8), Max = new Vector2f(18, 18) };

            PhysicsObject A = new PhysicsObject(
                new BoxPhysShape(10, 10),
                new Vector2f(5, 5),
                0.8f, false, null, canRotate: true);

            PhysicsObject B = new PhysicsObject(
                new BoxPhysShape(10, 10),
                new Vector2f(13, 13),
                0.8f, false, null, canRotate: true);

            A.Angle = 0;
            B.Angle = 0;
            Manifold m = new Manifold { A = A, B = B };
            m.ContactPoint = (A.Center + B.Center) * 0.5f;
            m.Penetration = 2;
            m.Normal = new Vector2f(1, 0);

            float initialAAngle = A.Angle;
            float initialBAngle = B.Angle;

            Collision.AngularPositionalCorrection(ref m);

            Assert.AreNotEqual(initialAAngle, A.Angle, "A's angle should change after angular correction.");
            Assert.AreNotEqual(initialBAngle, B.Angle, "B's angle should change after angular correction.");
        }

        [TestMethod]
        public void Test_PositionalCorrection_PerpendicularBoxes()
        {
            // Box A (0°): Horizontal rectangle.
            // Dimensions: width = 20, height = 10, center = (10,10) => AABB: Min=(0,5), Max=(20,15)
            AABB aabbA = new AABB { Min = new Vector2f(0, 5), Max = new Vector2f(20, 15) };

            // Box B (90°): Vertical rectangle.
            // Dimensions: width = 10, height = 20, center = (15,10) => AABB: Min=(10,0), Max=(20,20)
            AABB aabbB = new AABB { Min = new Vector2f(10, 0), Max = new Vector2f(20, 20) };

            // Use RotatingPhysicsObject so angular correction is available.
            PhysicsObject boxA = new PhysicsObject(
                new BoxPhysShape(20, 10),
                new Vector2f(10, 10),
                0.6f, false, null, canRotate: true);

            PhysicsObject boxB = new PhysicsObject(
                new BoxPhysShape(10, 20),
                new Vector2f(15, 10),
                0.6f, false, null, canRotate: true);

            // Set box A angle to 0; box B angle to 90° (π/2 radians).
            boxA.Angle = 0;
            boxB.Angle = (float)(Math.PI / 2);

            // Set velocities to simulate collision.
            boxA.Velocity = new Vector2f(0, -50);
            boxB.Velocity = new Vector2f(0, 50);

            // Construct the manifold.
            Manifold m = new Manifold { A = boxA, B = boxB };

            // Perform collision detection with the manifold version.
            bool collision = Collision.AABBvsAABB(ref m);
            Assert.IsTrue(collision, "Boxes should be colliding.");

            // Record the initial separation along the collision normal.
            Vector2f initialDiff = boxB.Center - boxA.Center;
            float initialProj = Dot(initialDiff, m.Normal);

            // Call the positional correction.
            Collision.PositionalCorrection(ref m);

            // After correction, the centers should have moved apart along the collision normal.
            Vector2f newDiff = boxB.Center - boxA.Center;
            float newProj = Dot(newDiff, m.Normal);

            Assert.IsTrue(newProj > initialProj, "After positional correction, boxes should be separated further along the collision normal.");
        }

        #endregion

        // Helper function for dot product.
        private static float Dot(Vector2f a, Vector2f b)
        {
            return a.X * b.X + a.Y * b.Y;
        }
    }
}
