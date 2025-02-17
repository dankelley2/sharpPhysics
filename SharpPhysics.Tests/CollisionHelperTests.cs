using Microsoft.VisualStudio.TestTools.UnitTesting;
using physics.Engine.Classes;
using physics.Engine.Structs;
using SFML.System;
using System;
using System.Collections.Generic;

namespace SharpPhysics.Tests
{


    [TestClass]
    public class CollisionHelperTests
    {
        public AABB DummyAABB(Vector2f min, Vector2f max)
        {
            return new AABB()
            {
                Min = min,
                Max = max
            };
        }
        [TestMethod]
        public void TestGetRectangleCorners_NoRotation()
        {
            // Create a box with no rotation.
            // AABB from (80,90) to (120,110) yields width=40, height=20 and center=(100,100).
            AABB aabb = DummyAABB(new Vector2f(80, 90), new Vector2f(120, 110));
            PhysicsObject obj = new PhysicsObject(aabb, PhysicsObject.Type.Box, 0.6f, false, null);
            obj.Angle = 0; // no rotation

            List<Vector2f> corners = CollisionHelpers.GetRectangleCorners(obj);

            // Expected: local corners: (20,10), (-20,10), (-20,-10), (20,-10)
            // After translating by center (100,100): (120,110), (80,110), (80,90), (120,90)
            Assert.AreEqual(4, corners.Count);
            Assert.AreEqual(new Vector2f(120, 110), corners[0]);
            Assert.AreEqual(new Vector2f(80, 110), corners[1]);
            Assert.AreEqual(new Vector2f(80, 90), corners[2]);
            Assert.AreEqual(new Vector2f(120, 90), corners[3]);
        }

        [TestMethod]
        public void TestGetRectangleCorners_WithRotation()
        {
            // Create a box with 90° rotation (pi/2 radians).
            AABB aabb = DummyAABB(new Vector2f(80, 90), new Vector2f(120, 110));
            PhysicsObject obj = new PhysicsObject(aabb, PhysicsObject.Type.Box, 0.6f, false, null);
            obj.Angle = (float)Math.PI / 2; // 90° rotation

            List<Vector2f> corners = CollisionHelpers.GetRectangleCorners(obj);

            // For a 90° rotation, the local corners (20,10), (-20,10), (-20,-10), (20,-10)
            // become (-10,20), (-10,-20), (10,-20), (10,20) when rotated,
            // and then translated by center (100,100) yields:
            // (90,120), (90,80), (110,80), (110,120)
            Assert.AreEqual(4, corners.Count);
            Assert.AreEqual(new Vector2f(90, 120), corners[0]);
            Assert.AreEqual(new Vector2f(90, 80), corners[1]);
            Assert.AreEqual(new Vector2f(110, 80), corners[2]);
            Assert.AreEqual(new Vector2f(110, 120), corners[3]);
        }

        [TestMethod]
        public void TestSutherlandHodgmanClip_NoClipping()
        {
            // When the subject polygon is entirely inside the clip polygon, output equals subject.
            List<Vector2f> subject = new List<Vector2f>
            {
                new Vector2f(1, 1),
                new Vector2f(3, 1),
                new Vector2f(3, 3),
                new Vector2f(1, 3)
            };
            List<Vector2f> clip = new List<Vector2f>
            {
                new Vector2f(0, 0),
                new Vector2f(4, 0),
                new Vector2f(4, 4),
                new Vector2f(0, 4)
            };

            List<Vector2f> output = CollisionHelpers.SutherlandHodgmanClip(subject, clip);

            Assert.AreEqual(subject.Count, output.Count);
            for (int i = 0; i < subject.Count; i++)
            {
                Assert.AreEqual(subject[i].X, output[i].X, 0.001, $"X mismatch at index {i}");
                Assert.AreEqual(subject[i].Y, output[i].Y, 0.001, $"Y mismatch at index {i}");
            }
        }// Helper method to normalize a polygon's vertex order.
        // This rotates the list so that the vertex with the smallest (X,Y) comes first.
        private List<Vector2f> NormalizePolygon(List<Vector2f> poly)
        {
            if (poly == null || poly.Count == 0)
                return poly;
            int bestIndex = 0;
            for (int i = 1; i < poly.Count; i++)
            {
                if (poly[i].X < poly[bestIndex].X ||
                   (Math.Abs(poly[i].X - poly[bestIndex].X) < 1e-6 && poly[i].Y < poly[bestIndex].Y))
                {
                    bestIndex = i;
                }
            }
            List<Vector2f> normalized = new List<Vector2f>();
            for (int i = 0; i < poly.Count; i++)
            {
                normalized.Add(poly[(bestIndex + i) % poly.Count]);
            }
            return normalized;
        }

        [TestMethod]
        public void TestSutherlandHodgmanClip_Sample1()
        {
            // Sample 1:
            // Subject polygon: (100,150), (200,250), (300,200)
            // Clipping Area (square): (150,150), (150,200), (200,200), (200,150)
            // Expected output: (150,162), (150,200), (200,200), (200,174)

            List<Vector2f> subject = new List<Vector2f>
            {
                new Vector2f(100, 150),
                new Vector2f(200, 250),
                new Vector2f(300, 200)
            };

            List<Vector2f> clip = new List<Vector2f>
            {
                new Vector2f(150, 150),
                new Vector2f(150, 200),
                new Vector2f(200, 200),
                new Vector2f(200, 150)
            };

            List<Vector2f> output = CollisionHelpers.SutherlandHodgmanClip(subject, clip);

            List<Vector2f> expected = new List<Vector2f>
            {
                new Vector2f(150, 162.5F),
                new Vector2f(150, 200),
                new Vector2f(200, 200),
                new Vector2f(200, 175)
            };

            List<Vector2f> normExpected = NormalizePolygon(expected);
            List<Vector2f> normOutput = NormalizePolygon(output);

            Assert.AreEqual(normExpected.Count, normOutput.Count, "Vertex count mismatch.");
            for (int i = 0; i < normExpected.Count; i++)
            {
                Assert.AreEqual(normExpected[i].X, normOutput[i].X, 0.01, $"X mismatch at index {i}");
                Assert.AreEqual(normExpected[i].Y, normOutput[i].Y, 0.01, $"Y mismatch at index {i}");
            }
        }

        [TestMethod]
        public void TestSutherlandHodgmanClip_WithClipping()
        {
            // Subject polygon partially outside the clip polygon.
            List<Vector2f> subject = new List<Vector2f>
            {
                new Vector2f(-1, -1),
                new Vector2f(-1, 2),
                new Vector2f(2, 2),
                new Vector2f(2, -1),
            };
            List<Vector2f> clip = new List<Vector2f>
            {
                new Vector2f(0, 0),
                new Vector2f(0, 1),
                new Vector2f(1, 1),
                new Vector2f(1, 0),
            };

            List<Vector2f> output = CollisionHelpers.SutherlandHodgmanClip(subject, clip);

            List<Vector2f> expected = new List<Vector2f>
            {
                new Vector2f(0, 0),
                new Vector2f(0, 1),
                new Vector2f(1, 1),
                new Vector2f(1, 0),
            };

            Assert.AreEqual(expected.Count, output.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i].X, output[i].X, 0.001, $"X mismatch at index {i}");
                Assert.AreEqual(expected[i].Y, output[i].Y, 0.001, $"Y mismatch at index {i}");
            }
        }

        [TestMethod]
        public void TestSutherlandHodgmanClip_Sample2()
        {
            // Sample 2:
            // Subject polygon: (100,150), (200,250), (300,200)
            // Clipping Area: (100,300), (300,300), (200,100)
            // Expected output: (242,185), (166,166), (150,200), (200,250), (260,220)

            List<Vector2f> subject = new List<Vector2f>
            {
                new Vector2f(100, 150),
                new Vector2f(200, 250),
                new Vector2f(300, 200)
            };

            List<Vector2f> clip = new List<Vector2f>
            {
                new Vector2f(100, 300),
                new Vector2f(300, 300),
                new Vector2f(200, 100)
            };

            List<Vector2f> output = CollisionHelpers.SutherlandHodgmanClip(subject, clip);

            List<Vector2f> expected = new List<Vector2f>
            {
                new Vector2f(242.85F, 185.71F),
                new Vector2f(166.66F, 166.66F),
                new Vector2f(150, 200),
                new Vector2f(200, 250),
                new Vector2f(260, 220)
            };

            List<Vector2f> normExpected = NormalizePolygon(expected);
            List<Vector2f> normOutput = NormalizePolygon(output);

            Assert.AreEqual(normExpected.Count, normOutput.Count, "Vertex count mismatch.");
            for (int i = 0; i < normExpected.Count; i++)
            {
                Assert.AreEqual(normExpected[i].X, normOutput[i].X, 0.01, $"X mismatch at index {i}");
                Assert.AreEqual(normExpected[i].Y, normOutput[i].Y, 0.01, $"Y mismatch at index {i}");
            }
        }


        [TestMethod]
        public void TestComputeIntersection()
        {
            // Intersection of lines (0,0)-(4,4) and (0,4)-(4,0) should be (2,2)
            Vector2f s = new Vector2f(0, 0);
            Vector2f e = new Vector2f(4, 4);
            Vector2f cp1 = new Vector2f(0, 4);
            Vector2f cp2 = new Vector2f(4, 0);
            Vector2f intersect = CollisionHelpers.ComputeIntersection(s, e, cp1, cp2);
            Assert.AreEqual(2, intersect.X, 0.001, "Intersection X incorrect");
            Assert.AreEqual(2, intersect.Y, 0.001, "Intersection Y incorrect");
        }

        [TestMethod]
        public void TestComputeCentroid()
        {
            // For a square with vertices (0,0), (4,0), (4,4), (0,4), the centroid should be (2,2)
            List<Vector2f> polygon = new List<Vector2f>
            {
                new Vector2f(0, 0),
                new Vector2f(4, 0),
                new Vector2f(4, 4),
                new Vector2f(0, 4)
            };
            Vector2f centroid = CollisionHelpers.ComputeCentroid(polygon);
            Assert.AreEqual(2, centroid.X, 0.001, "Centroid X incorrect");
            Assert.AreEqual(2, centroid.Y, 0.001, "Centroid Y incorrect");
        }

        [TestMethod]
        public void TestUpdateContactPoint_NoIntersection_Fallback()
        {
            // Two non-intersecting boxes: expect contact point to be midpoint of centers.
            AABB aabbA = DummyAABB(new Vector2f(0, 0), new Vector2f(10, 10));
            AABB aabbB = DummyAABB(new Vector2f(20, 20), new Vector2f(30, 30));
            PhysicsObject objA = new PhysicsObject(aabbA, PhysicsObject.Type.Box, 0.6f, false, null);
            PhysicsObject objB = new PhysicsObject(aabbB, PhysicsObject.Type.Box, 0.6f, false, null);
            Manifold m = new Manifold { A = objA, B = objB };

            CollisionHelpers.UpdateContactPoint(ref m);

            Vector2f expected = (objA.Center + objB.Center) * 0.5f;
            Assert.AreEqual(expected.X, m.ContactPoint.X, 0.001, "Contact point X fallback incorrect");
            Assert.AreEqual(expected.Y, m.ContactPoint.Y, 0.001, "Contact point Y fallback incorrect");
        }

        [TestMethod]
        public void TestUpdateContactPoint_WithIntersection()
        {
            // Two overlapping boxes. Their intersection is from (5,5) to (10,10).
            AABB aabbA = DummyAABB(new Vector2f(0, 0), new Vector2f(10, 10));
            AABB aabbB = DummyAABB(new Vector2f(5, 5), new Vector2f(15, 15));
            PhysicsObject objA = new PhysicsObject(aabbA, PhysicsObject.Type.Box, 0.6f, false, null);
            PhysicsObject objB = new PhysicsObject(aabbB, PhysicsObject.Type.Box, 0.6f, false, null);
            objA.Angle = 0;
            objB.Angle = 0;
            Manifold m = new Manifold { A = objA, B = objB };

            CollisionHelpers.UpdateContactPoint(ref m);
            // Intersection region's centroid should be (7.5, 7.5)
            Assert.AreEqual(7.5f, m.ContactPoint.X, 0.001, "Contact point X incorrect");
            Assert.AreEqual(7.5f, m.ContactPoint.Y, 0.001, "Contact point Y incorrect");
        }
    }
}
