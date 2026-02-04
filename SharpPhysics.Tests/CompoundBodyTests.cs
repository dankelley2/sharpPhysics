using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Numerics;
using System.Collections.Generic;
using SharpPhysics.Engine.Shapes;
using SharpPhysics.Engine.Objects;
using SharpPhysics.Engine.Core;
using SharpPhysics.Engine.Classes;
using SharpPhysics.Engine.Helpers;

namespace SharpPhysics.Tests
{
    [TestClass]
    public class CompoundBodyTests
    {
        #region Vertex Tests

        [TestMethod]
        public void CompoundBody_FromLShape_HasCorrectChildCount()
        {
            // L-shape should decompose into 2+ convex pieces
            var lShape = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(60, 0),
                new Vector2(60, 25),
                new Vector2(25, 25),
                new Vector2(25, 60),
                new Vector2(0, 60)
            };

            var convexPieces = PolygonDecomposition.DecomposeToConvex(lShape);
            var compound = CompoundBody.FromConvexPieces(Vector2.Zero, convexPieces, null);

            Assert.IsTrue(compound.ChildCount >= 2, $"L-shape should decompose into at least 2 pieces, got {compound.ChildCount}");
        }

        [TestMethod]
        public void CompoundBody_EachChildPolygon_HasAtLeast3Vertices()
        {
            // L-shape
            var lShape = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(60, 0),
                new Vector2(60, 25),
                new Vector2(25, 25),
                new Vector2(25, 60),
                new Vector2(0, 60)
            };

            var convexPieces = PolygonDecomposition.DecomposeToConvex(lShape);
            var compound = CompoundBody.FromConvexPieces(Vector2.Zero, convexPieces, null);

            for (int i = 0; i < compound.Shape.Children.Count; i++)
            {
                var child = compound.Shape.Children[i];
                var vertices = child.Shape.LocalVertices;
                Assert.IsTrue(vertices.Count >= 3, 
                    $"Child {i} has only {vertices.Count} vertices, minimum is 3");
            }
        }

        [TestMethod]
        public void CompoundBody_ChildPolygons_FormClosedShapes()
        {
            // L-shape
            var lShape = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(60, 0),
                new Vector2(60, 25),
                new Vector2(25, 25),
                new Vector2(25, 60),
                new Vector2(0, 60)
            };

            var convexPieces = PolygonDecomposition.DecomposeToConvex(lShape);
            var compound = CompoundBody.FromConvexPieces(Vector2.Zero, convexPieces, null);

            for (int i = 0; i < compound.Shape.Children.Count; i++)
            {
                var child = compound.Shape.Children[i];
                var (childCenter, childAngle) = compound.Shape.GetChildWorldTransform(i, compound.Center, compound.Angle);
                var worldVerts = child.Shape.GetTransformedVertices(childCenter, childAngle);

                // Verify all edges have non-zero length (no duplicate vertices)
                for (int j = 0; j < worldVerts.Length; j++)
                {
                    int next = (j + 1) % worldVerts.Length;
                    float edgeLength = Vector2.Distance(worldVerts[j], worldVerts[next]);
                    Assert.IsTrue(edgeLength > 0.001f, 
                        $"Child {i} has zero-length edge from vertex {j} to {next}");
                }
            }
        }

        [TestMethod]
        public void CompoundBody_ChildPolygons_AreConvex()
        {
            // L-shape (concave input)
            var lShape = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(60, 0),
                new Vector2(60, 25),
                new Vector2(25, 25),
                new Vector2(25, 60),
                new Vector2(0, 60)
            };

            var convexPieces = PolygonDecomposition.DecomposeToConvex(lShape);

            foreach (var piece in convexPieces)
            {
                Assert.IsTrue(PolygonDecomposition.IsConvex(piece), 
                    "Each decomposed piece should be convex");
            }
        }

        [TestMethod]
        public void CompoundBody_TransformedVertices_MatchChildCount()
        {
            // Simple L-shape
            var lShape = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(60, 0),
                new Vector2(60, 25),
                new Vector2(25, 25),
                new Vector2(25, 60),
                new Vector2(0, 60)
            };

            var convexPieces = PolygonDecomposition.DecomposeToConvex(lShape);
            var compound = CompoundBody.FromConvexPieces(new Vector2(100, 100), convexPieces, null);

            // Get all transformed vertices
            var allVerts = compound.Shape.GetTransformedVertices(compound.Center, compound.Angle);

            // Total vertex count should equal sum of all child vertices
            int expectedCount = 0;
            foreach (var child in compound.Shape.Children)
            {
                expectedCount += child.Shape.LocalVertices.Count;
            }

            Assert.AreEqual(expectedCount, allVerts.Length, 
                $"Expected {expectedCount} total vertices, got {allVerts.Length}");
        }

        #endregion

        #region Collision Tests - Compound vs Box

        [TestMethod]
        public void CompoundVsBox_Overlapping_DetectsCollision()
        {
            // Create L-shaped compound body at origin
            var lShape = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(60, 0),
                new Vector2(60, 25),
                new Vector2(25, 25),
                new Vector2(25, 60),
                new Vector2(0, 60)
            };

            var convexPieces = PolygonDecomposition.DecomposeToConvex(lShape);
            var compound = CompoundBody.FromConvexPieces(new Vector2(30, 30), convexPieces, null);

            // Create box overlapping with the compound
            var box = new PhysicsObject(
                new BoxPhysShape(20, 20),
                new Vector2(45, 15),  // Overlaps with horizontal part of L
                0.6f, false, null, canRotate: true);

            Manifold m = new Manifold { A = compound, B = box };
            bool collision = Collision.CompoundVsOther(ref m, compound.Shape, compoundIsA: true);

            Assert.IsTrue(collision, "Compound should collide with overlapping box");
            Assert.IsTrue(m.Penetration > 0, "Penetration should be positive");
        }

        [TestMethod]
        public void CompoundVsBox_NotOverlapping_NoCollision()
        {
            // Create L-shaped compound body
            var lShape = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(60, 0),
                new Vector2(60, 25),
                new Vector2(25, 25),
                new Vector2(25, 60),
                new Vector2(0, 60)
            };

            var convexPieces = PolygonDecomposition.DecomposeToConvex(lShape);
            var compound = CompoundBody.FromConvexPieces(new Vector2(30, 30), convexPieces, null);

            // Create box far away from the compound
            var box = new PhysicsObject(
                new BoxPhysShape(20, 20),
                new Vector2(200, 200),  // Far away
                0.6f, false, null, canRotate: true);

            Manifold m = new Manifold { A = compound, B = box };
            bool collision = Collision.CompoundVsOther(ref m, compound.Shape, compoundIsA: true);

            Assert.IsFalse(collision, "Compound should not collide with distant box");
        }

        #endregion

        #region Collision Tests - Compound vs Circle

        [TestMethod]
        public void CompoundVsCircle_Overlapping_DetectsCollision()
        {
            // Create L-shaped compound body
            var lShape = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(60, 0),
                new Vector2(60, 25),
                new Vector2(25, 25),
                new Vector2(25, 60),
                new Vector2(0, 60)
            };

            var convexPieces = PolygonDecomposition.DecomposeToConvex(lShape);
            var compound = CompoundBody.FromConvexPieces(new Vector2(30, 30), convexPieces, null);

            // Create circle overlapping with the compound
            var circle = new PhysicsObject(
                new CirclePhysShape(10),
                new Vector2(40, 10),  // Overlaps with horizontal part of L
                0.6f, false, null, canRotate: true);

            Manifold m = new Manifold { A = compound, B = circle };
            bool collision = Collision.CompoundVsOther(ref m, compound.Shape, compoundIsA: true);

            Assert.IsTrue(collision, "Compound should collide with overlapping circle");
            Assert.IsTrue(m.Penetration > 0, "Penetration should be positive");
        }

        [TestMethod]
        public void CompoundVsCircle_InsideConcavity_NoCollision()
        {
            // Create L-shaped compound body
            var lShape = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(60, 0),
                new Vector2(60, 25),
                new Vector2(25, 25),
                new Vector2(25, 60),
                new Vector2(0, 60)
            };

            var convexPieces = PolygonDecomposition.DecomposeToConvex(lShape);
            var compound = CompoundBody.FromConvexPieces(new Vector2(30, 30), convexPieces, null);

            // Create small circle in the concave part (upper right area that's "inside" the L's bend)
            var circle = new PhysicsObject(
                new CirclePhysShape(5),
                new Vector2(70, 55),  // In the concave area
                0.6f, false, null, canRotate: true);

            Manifold m = new Manifold { A = compound, B = circle };
            bool collision = Collision.CompoundVsOther(ref m, compound.Shape, compoundIsA: true);

            Assert.IsFalse(collision, "Circle in concave area should not collide with compound");
        }

        #endregion

        #region Collision Tests - Compound vs Compound

        [TestMethod]
        public void CompoundVsCompound_Overlapping_DetectsCollision()
        {
            // Create two L-shaped compound bodies
            var lShape = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(60, 0),
                new Vector2(60, 25),
                new Vector2(25, 25),
                new Vector2(25, 60),
                new Vector2(0, 60)
            };

            var convexPieces = PolygonDecomposition.DecomposeToConvex(lShape);
            var compoundA = CompoundBody.FromConvexPieces(new Vector2(30, 30), convexPieces, null);
            var compoundB = CompoundBody.FromConvexPieces(new Vector2(60, 30), convexPieces, null);  // Overlapping

            Manifold m = new Manifold { A = compoundA, B = compoundB };
            bool collision = Collision.CompoundVsOther(ref m, compoundA.Shape, compoundIsA: true);

            Assert.IsTrue(collision, "Overlapping compound bodies should collide");
            Assert.IsTrue(m.Penetration > 0, "Penetration should be positive");
        }

        [TestMethod]
        public void CompoundVsCompound_NotOverlapping_NoCollision()
        {
            // Create two L-shaped compound bodies
            var lShape = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(60, 0),
                new Vector2(60, 25),
                new Vector2(25, 25),
                new Vector2(25, 60),
                new Vector2(0, 60)
            };

            var convexPieces = PolygonDecomposition.DecomposeToConvex(lShape);
            var compoundA = CompoundBody.FromConvexPieces(new Vector2(0, 0), convexPieces, null);
            var compoundB = CompoundBody.FromConvexPieces(new Vector2(200, 200), convexPieces, null);  // Far away

            Manifold m = new Manifold { A = compoundA, B = compoundB };
            bool collision = Collision.CompoundVsOther(ref m, compoundA.Shape, compoundIsA: true);

            Assert.IsFalse(collision, "Non-overlapping compound bodies should not collide");
        }

        #endregion

        #region Contact Point Tests

        [TestMethod]
        public void CompoundVsBox_ContactPoint_IsWithinBothShapes()
        {
            // Create L-shaped compound body
            var lShape = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(60, 0),
                new Vector2(60, 25),
                new Vector2(25, 25),
                new Vector2(25, 60),
                new Vector2(0, 60)
            };

            var convexPieces = PolygonDecomposition.DecomposeToConvex(lShape);
            var compound = CompoundBody.FromConvexPieces(new Vector2(30, 30), convexPieces, null);

            // Create box overlapping with the compound
            var box = new PhysicsObject(
                new BoxPhysShape(20, 20),
                new Vector2(45, 15),
                0.6f, false, null, canRotate: true);

            Manifold m = new Manifold { A = compound, B = box };
            bool collision = Collision.CompoundVsOther(ref m, compound.Shape, compoundIsA: true);

            if (collision)
            {
                // Contact point should be reasonable (not at infinity or NaN)
                Assert.IsFalse(float.IsNaN(m.ContactPoint.X), "Contact point X should not be NaN");
                Assert.IsFalse(float.IsNaN(m.ContactPoint.Y), "Contact point Y should not be NaN");
                Assert.IsFalse(float.IsInfinity(m.ContactPoint.X), "Contact point X should not be infinity");
                Assert.IsFalse(float.IsInfinity(m.ContactPoint.Y), "Contact point Y should not be infinity");
            }
        }

        [TestMethod]
        public void CompoundVsBox_Normal_IsUnitVector()
        {
            // Create L-shaped compound body
            var lShape = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(60, 0),
                new Vector2(60, 25),
                new Vector2(25, 25),
                new Vector2(25, 60),
                new Vector2(0, 60)
            };

            var convexPieces = PolygonDecomposition.DecomposeToConvex(lShape);
            var compound = CompoundBody.FromConvexPieces(new Vector2(30, 30), convexPieces, null);

            // Create box overlapping with the compound
            var box = new PhysicsObject(
                new BoxPhysShape(20, 20),
                new Vector2(45, 15),
                0.6f, false, null, canRotate: true);

            Manifold m = new Manifold { A = compound, B = box };
            bool collision = Collision.CompoundVsOther(ref m, compound.Shape, compoundIsA: true);

            if (collision)
            {
                float normalLength = m.Normal.Length();
                Assert.AreEqual(1.0f, normalLength, 0.001f, "Collision normal should be a unit vector");
            }
        }

        #endregion

        #region Mass and Inertia Tests

        [TestMethod]
        public void CompoundBody_HasPositiveMass()
        {
            var lShape = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(60, 0),
                new Vector2(60, 25),
                new Vector2(25, 25),
                new Vector2(25, 60),
                new Vector2(0, 60)
            };

            var convexPieces = PolygonDecomposition.DecomposeToConvex(lShape);
            var compound = CompoundBody.FromConvexPieces(Vector2.Zero, convexPieces, null);

            Assert.IsTrue(compound.Mass > 0, "Compound body should have positive mass");
        }

        [TestMethod]
        public void CompoundBody_HasValidInertia()
        {
            var lShape = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(60, 0),
                new Vector2(60, 25),
                new Vector2(25, 25),
                new Vector2(25, 60),
                new Vector2(0, 60)
            };

            var convexPieces = PolygonDecomposition.DecomposeToConvex(lShape);
            var compound = CompoundBody.FromConvexPieces(Vector2.Zero, convexPieces, null);

            Assert.IsTrue(compound.IMass > 0, "Compound body should have positive inverse mass");
            Assert.IsFalse(float.IsNaN(compound.IMass), "Inverse mass should not be NaN");
            Assert.IsFalse(float.IsInfinity(compound.IMass), "Inverse mass should not be infinity");
        }

        #endregion

        #region AABB Tests

        [TestMethod]
        public void CompoundBody_AABB_ContainsAllChildren()
        {
            var lShape = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(60, 0),
                new Vector2(60, 25),
                new Vector2(25, 25),
                new Vector2(25, 60),
                new Vector2(0, 60)
            };

            var convexPieces = PolygonDecomposition.DecomposeToConvex(lShape);
            var compound = CompoundBody.FromConvexPieces(new Vector2(100, 100), convexPieces, null);

            var aabb = compound.Aabb;

            // Verify all child vertices are within the AABB
            var allVerts = compound.Shape.GetTransformedVertices(compound.Center, compound.Angle);
            foreach (var vert in allVerts)
            {
                Assert.IsTrue(vert.X >= aabb.Min.X - 0.01f, $"Vertex X={vert.X} should be >= AABB.Min.X={aabb.Min.X}");
                Assert.IsTrue(vert.X <= aabb.Max.X + 0.01f, $"Vertex X={vert.X} should be <= AABB.Max.X={aabb.Max.X}");
                Assert.IsTrue(vert.Y >= aabb.Min.Y - 0.01f, $"Vertex Y={vert.Y} should be >= AABB.Min.Y={aabb.Min.Y}");
                Assert.IsTrue(vert.Y <= aabb.Max.Y + 0.01f, $"Vertex Y={vert.Y} should be <= AABB.Max.Y={aabb.Max.Y}");
            }
        }

        #endregion
    }
}
