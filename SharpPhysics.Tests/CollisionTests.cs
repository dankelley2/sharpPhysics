using Microsoft.VisualStudio.TestTools.UnitTesting;
using SFML.System;
using physics.Engine;
using physics.Engine.Classes;
using physics.Engine.Structs;
using System;

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
            AABB aabbA = new AABB { Min = new Vector2f(0, 0), Max = new Vector2f(10, 10) };
            AABB aabbB = new AABB { Min = new Vector2f(5, 5), Max = new Vector2f(15, 15) };
            PhysicsObject A = new PhysicsObject(aabbA, PhysicsObject.Type.Box, 0.6f, false, null);
            PhysicsObject B = new PhysicsObject(aabbB, PhysicsObject.Type.Box, 0.6f, false, null);
            // No rotation
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
            // Create two circles with diameter 10.
            // A's AABB from (0,0) to (10,10) => center (5,5), radius=5.
            // B's AABB from (7,7) to (17,17) => center (12,12), radius=5.
            AABB aabbA = new AABB { Min = new Vector2f(0, 0), Max = new Vector2f(10, 10) };
            AABB aabbB = new AABB { Min = new Vector2f(7, 7), Max = new Vector2f(17, 17) };
            PhysicsObject A = new PhysicsObject(aabbA, PhysicsObject.Type.Circle, 0.6f, false, null);
            PhysicsObject B = new PhysicsObject(aabbB, PhysicsObject.Type.Circle, 0.6f, false, null);
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
            // Box: AABB from (0,0) to (20,20) => center (10,10)
            // Circle: AABB from (15,15) to (25,25) => center (20,20), radius = 5.
            AABB aabbBox = new AABB { Min = new Vector2f(0, 0), Max = new Vector2f(20, 20) };
            AABB aabbCircle = new AABB { Min = new Vector2f(15, 15), Max = new Vector2f(25, 25) };
            PhysicsObject box = new PhysicsObject(aabbBox, PhysicsObject.Type.Box, 0.6f, false, null);
            PhysicsObject circle = new PhysicsObject(aabbCircle, PhysicsObject.Type.Circle, 0.6f, false, null);
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
            AABB aabbA = new AABB { Min = new Vector2f(0, 0), Max = new Vector2f(10, 10) };
            AABB aabbB = new AABB { Min = new Vector2f(8, 8), Max = new Vector2f(18, 18) };
            PhysicsObject A = new PhysicsObject(aabbA, PhysicsObject.Type.Box, 0.8f, false, null);
            PhysicsObject B = new PhysicsObject(aabbB, PhysicsObject.Type.Box, 0.8f, false, null);
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
            AABB aabbA = new AABB { Min = new Vector2f(0, 0), Max = new Vector2f(10, 10) };
            AABB aabbB = new AABB { Min = new Vector2f(8, 8), Max = new Vector2f(18, 18) };
            PhysicsObject A = new PhysicsObject(aabbA, PhysicsObject.Type.Circle, 0.8f, false, null);
            PhysicsObject B = new PhysicsObject(aabbB, PhysicsObject.Type.Circle, 0.8f, false, null);
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
            // Create two overlapping boxes.
            AABB aabbA = new AABB { Min = new Vector2f(0, 0), Max = new Vector2f(10, 10) };
            AABB aabbB = new AABB { Min = new Vector2f(8, 8), Max = new Vector2f(18, 18) };
            PhysicsObject A = new PhysicsObject(aabbA, PhysicsObject.Type.Box, 0.8f, false, null);
            PhysicsObject B = new PhysicsObject(aabbB, PhysicsObject.Type.Box, 0.8f, false, null);
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
            // Create two boxes with a contact point set.
            AABB aabbA = new AABB { Min = new Vector2f(0, 0), Max = new Vector2f(10, 10) };
            AABB aabbB = new AABB { Min = new Vector2f(8, 8), Max = new Vector2f(18, 18) };
            PhysicsObject A = new PhysicsObject(aabbA, PhysicsObject.Type.Box, 0.8f, false, null);
            PhysicsObject B = new PhysicsObject(aabbB, PhysicsObject.Type.Box, 0.8f, false, null);
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
            // Let's choose dimensions so that Box A has width = 20 and height = 10.
            // We'll place it so that its center is at (10,10); thus, AABB: Min=(0,5), Max=(20,15)
            AABB aabbA = new AABB { Min = new Vector2f(0, 5), Max = new Vector2f(20, 15) };

            // Box B (90°): Vertical rectangle.
            // Let its physical dimensions be width = 10 and height = 20.
            // We'll position it so that its center is at (15,10); thus, AABB: Min=(10,0), Max=(20,20)
            AABB aabbB = new AABB { Min = new Vector2f(10, 0), Max = new Vector2f(20, 20) };

            // Create the PhysicsObjects.
            // For Box A, angle remains 0.
            PhysicsObject boxA = new PhysicsObject(aabbA, PhysicsObject.Type.Box, 0.6f, false, null);
            // For Box B, set angle = π/2 (90°).
            PhysicsObject boxB = new PhysicsObject(aabbB, PhysicsObject.Type.Box, 0.6f, false, null);
            boxA.Angle = 0;
            boxB.Angle = (float)(Math.PI / 2);

            // For clarity, set velocities to zero (we want to see positional correction effect only).
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
        // Helper function for dot product if needed (not used directly in tests).
        private static float Dot(Vector2f a, Vector2f b)
        {
            return a.X * b.X + a.Y * b.Y;
        }
    }
}
