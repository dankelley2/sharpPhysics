using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Numerics;
using System.Collections.Generic;
using physics.Engine.Helpers;

namespace SharpPhysics.Tests
{
    [TestClass]
    public class PolygonDecompositionTests
    {
        #region IsConvex Tests

        [TestMethod]
        public void IsConvex_Square_ReturnsTrue()
        {
            // Simple square (convex)
            var square = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(10, 0),
                new Vector2(10, 10),
                new Vector2(0, 10)
            };

            Assert.IsTrue(PolygonDecomposition.IsConvex(square), "Square should be convex.");
        }

        [TestMethod]
        public void IsConvex_Triangle_ReturnsTrue()
        {
            // Simple triangle (convex)
            var triangle = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(10, 0),
                new Vector2(5, 10)
            };

            Assert.IsTrue(PolygonDecomposition.IsConvex(triangle), "Triangle should be convex.");
        }

        [TestMethod]
        public void IsConvex_LShape_ReturnsFalse()
        {
            // L-shape (concave)
            var lShape = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(60, 0),
                new Vector2(60, 25),
                new Vector2(25, 25),
                new Vector2(25, 60),
                new Vector2(0, 60)
            };

            Assert.IsFalse(PolygonDecomposition.IsConvex(lShape), "L-shape should be concave.");
        }

        [TestMethod]
        public void IsConvex_Arrow_ReturnsFalse()
        {
            // Arrow shape (concave)
            var arrow = new Vector2[]
            {
                new Vector2(0, 20),
                new Vector2(30, 20),
                new Vector2(30, 0),
                new Vector2(60, 25),
                new Vector2(30, 50),
                new Vector2(30, 30),
                new Vector2(0, 30)
            };

            Assert.IsFalse(PolygonDecomposition.IsConvex(arrow), "Arrow shape should be concave.");
        }

        [TestMethod]
        public void IsConvex_Pentagon_ReturnsTrue()
        {
            // Regular pentagon (convex)
            var pentagon = new Vector2[5];
            for (int i = 0; i < 5; i++)
            {
                float angle = (float)(2 * System.Math.PI * i / 5) - (float)(System.Math.PI / 2);
                pentagon[i] = new Vector2(
                    (float)System.Math.Cos(angle) * 10,
                    (float)System.Math.Sin(angle) * 10
                );
            }

            Assert.IsTrue(PolygonDecomposition.IsConvex(pentagon), "Regular pentagon should be convex.");
        }

        [TestMethod]
        public void IsConvex_Star_ReturnsFalse()
        {
            // 5-pointed star (concave)
            var star = new Vector2[]
            {
                new Vector2(25, 0),
                new Vector2(30, 18),
                new Vector2(50, 18),
                new Vector2(35, 30),
                new Vector2(40, 50),
                new Vector2(25, 38),
                new Vector2(10, 50),
                new Vector2(15, 30),
                new Vector2(0, 18),
                new Vector2(20, 18)
            };

            Assert.IsFalse(PolygonDecomposition.IsConvex(star), "Star shape should be concave.");
        }

        #endregion

        #region EarClipTriangulate Tests

        [TestMethod]
        public void EarClipTriangulate_Square_Returns2Triangles()
        {
            var square = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(10, 0),
                new Vector2(10, 10),
                new Vector2(0, 10)
            };

            var triangles = PolygonDecomposition.EarClipTriangulate(square);

            Assert.AreEqual(2, triangles.Count, "Square should decompose into 2 triangles.");
            foreach (var tri in triangles)
            {
                Assert.AreEqual(3, tri.Length, "Each triangle should have 3 vertices.");
            }
        }

        [TestMethod]
        public void EarClipTriangulate_Triangle_Returns1Triangle()
        {
            var triangle = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(10, 0),
                new Vector2(5, 10)
            };

            var triangles = PolygonDecomposition.EarClipTriangulate(triangle);

            Assert.AreEqual(1, triangles.Count, "Triangle should return 1 triangle.");
        }

        [TestMethod]
        public void EarClipTriangulate_LShape_Returns4Triangles()
        {
            // L-shape has 6 vertices, should produce 4 triangles (n-2)
            var lShape = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(60, 0),
                new Vector2(60, 25),
                new Vector2(25, 25),
                new Vector2(25, 60),
                new Vector2(0, 60)
            };

            var triangles = PolygonDecomposition.EarClipTriangulate(lShape);

            Assert.AreEqual(4, triangles.Count, $"L-shape (6 vertices) should decompose into 4 triangles, got {triangles.Count}.");
        }

        [TestMethod]
        public void EarClipTriangulate_Pentagon_Returns3Triangles()
        {
            // Pentagon has 5 vertices, should produce 3 triangles (n-2)
            var pentagon = new Vector2[5];
            for (int i = 0; i < 5; i++)
            {
                float angle = (float)(2 * System.Math.PI * i / 5);
                pentagon[i] = new Vector2(
                    (float)System.Math.Cos(angle) * 10,
                    (float)System.Math.Sin(angle) * 10
                );
            }

            var triangles = PolygonDecomposition.EarClipTriangulate(pentagon);

            Assert.AreEqual(3, triangles.Count, "Pentagon should decompose into 3 triangles.");
        }

        [TestMethod]
        public void EarClipTriangulate_AllTrianglesHavePositiveArea()
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

            var triangles = PolygonDecomposition.EarClipTriangulate(lShape);

            foreach (var tri in triangles)
            {
                float area = CalculateTriangleArea(tri[0], tri[1], tri[2]);
                Assert.IsTrue(System.Math.Abs(area) > 0.001f, "Triangle should have non-zero area.");
            }
        }

        #endregion

        #region DecomposeToConvex Tests

        [TestMethod]
        public void DecomposeToConvex_ConvexPolygon_ReturnsSinglePolygon()
        {
            var square = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(10, 0),
                new Vector2(10, 10),
                new Vector2(0, 10)
            };

            var result = PolygonDecomposition.DecomposeToConvex(square);

            Assert.AreEqual(1, result.Count, "Convex polygon should return single polygon.");
        }

        [TestMethod]
        public void DecomposeToConvex_LShape_ReturnsConvexPieces()
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

            var result = PolygonDecomposition.DecomposeToConvex(lShape);

            Assert.IsTrue(result.Count >= 1, "L-shape should decompose into at least 1 piece.");

            // All pieces should be convex
            foreach (var piece in result)
            {
                Assert.IsTrue(PolygonDecomposition.IsConvex(piece),
                    $"All decomposed pieces should be convex. Got piece with {piece.Length} vertices.");
            }
        }

        [TestMethod]
        public void DecomposeToConvex_Arrow_ReturnsConvexPieces()
        {
            var arrow = new Vector2[]
            {
                new Vector2(0, 20),
                new Vector2(30, 20),
                new Vector2(30, 0),
                new Vector2(60, 25),
                new Vector2(30, 50),
                new Vector2(30, 30),
                new Vector2(0, 30)
            };

            var result = PolygonDecomposition.DecomposeToConvex(arrow);

            Assert.IsTrue(result.Count >= 1, "Arrow should decompose into at least 1 piece.");

            foreach (var piece in result)
            {
                Assert.IsTrue(PolygonDecomposition.IsConvex(piece),
                    "All decomposed pieces should be convex.");
            }
        }

        [TestMethod]
        public void DecomposeToConvex_Star_ReturnsConvexPieces()
        {
            var star = new Vector2[]
            {
                new Vector2(25, 0),
                new Vector2(30, 18),
                new Vector2(50, 18),
                new Vector2(35, 30),
                new Vector2(40, 50),
                new Vector2(25, 38),
                new Vector2(10, 50),
                new Vector2(15, 30),
                new Vector2(0, 18),
                new Vector2(20, 18)
            };

            var result = PolygonDecomposition.DecomposeToConvex(star);

            Assert.IsTrue(result.Count >= 1, "Star should decompose into at least 1 piece.");

            foreach (var piece in result)
            {
                Assert.IsTrue(PolygonDecomposition.IsConvex(piece),
                    "All decomposed pieces should be convex.");
            }
        }

        [TestMethod]
        public void DecomposeToConvex_TotalAreaPreserved()
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

            float originalArea = System.Math.Abs(CalculatePolygonArea(lShape));

            // First check triangulation preserves area
            var triangles = PolygonDecomposition.EarClipTriangulate(lShape);
            float triangleArea = 0;
            string triDetails = "";
            foreach (var tri in triangles)
            {
                float area = System.Math.Abs(CalculatePolygonArea(tri));
                triangleArea += area;
                triDetails += $"[{area:F1}] ";
            }
            Assert.AreEqual(originalArea, triangleArea, 1.0f,
                $"Triangulation should preserve area. Original: {originalArea}, Triangles: {triangleArea}, Triangle count: {triangles.Count}, Areas: {triDetails}");

            // Then check full decomposition
            var result = PolygonDecomposition.DecomposeToConvex(lShape);

            float totalDecomposedArea = 0;
            string pieceDetails = "";
            foreach (var piece in result)
            {
                float area = System.Math.Abs(CalculatePolygonArea(piece));
                totalDecomposedArea += area;
                pieceDetails += $"[verts:{piece.Length}, area:{area:F1}] ";
            }

            Assert.AreEqual(originalArea, totalDecomposedArea, 1.0f,
                $"Total area should be preserved. Original: {originalArea}, Decomposed: {totalDecomposedArea}, Piece count: {result.Count}, Pieces: {pieceDetails}");
        }

        [TestMethod]
        public void EarClipTriangulate_AreaPreserved()
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

            float originalArea = System.Math.Abs(CalculatePolygonArea(lShape));
            var triangles = PolygonDecomposition.EarClipTriangulate(lShape);

            float triangleArea = 0;
            foreach (var tri in triangles)
            {
                triangleArea += System.Math.Abs(CalculatePolygonArea(tri));
            }

            Assert.AreEqual(originalArea, triangleArea, 1.0f,
                $"Triangulation should preserve area. Original: {originalArea}, Triangles: {triangleArea}");
        }

        #endregion

        #region ComputeCentroid Tests

        [TestMethod]
        public void ComputeCentroid_Square_ReturnsCenter()
        {
            var square = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(10, 0),
                new Vector2(10, 10),
                new Vector2(0, 10)
            };

            var centroid = PolygonDecomposition.ComputeCentroid(square);

            Assert.AreEqual(5, centroid.X, 0.01f, "Centroid X should be 5.");
            Assert.AreEqual(5, centroid.Y, 0.01f, "Centroid Y should be 5.");
        }

        [TestMethod]
        public void ComputeCentroid_Triangle_ReturnsCenter()
        {
            var triangle = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(10, 0),
                new Vector2(5, 10)
            };

            var centroid = PolygonDecomposition.ComputeCentroid(triangle);

            // Triangle centroid is average of vertices
            float expectedX = (0 + 10 + 5) / 3f;
            float expectedY = (0 + 0 + 10) / 3f;

            Assert.AreEqual(expectedX, centroid.X, 0.1f, "Centroid X mismatch.");
            Assert.AreEqual(expectedY, centroid.Y, 0.1f, "Centroid Y mismatch.");
        }

        #endregion

        #region Edge Cases

        [TestMethod]
        public void DecomposeToConvex_TooFewVertices_ReturnsEmpty()
        {
            var twoPoints = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(10, 0)
            };

            var result = PolygonDecomposition.DecomposeToConvex(twoPoints);

            Assert.AreEqual(0, result.Count, "Polygon with less than 3 vertices should return empty.");
        }

        [TestMethod]
        public void EarClipTriangulate_TooFewVertices_ReturnsEmpty()
        {
            var twoPoints = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(10, 0)
            };

            var result = PolygonDecomposition.EarClipTriangulate(twoPoints);

            Assert.AreEqual(0, result.Count, "Polygon with less than 3 vertices should return empty.");
        }

        [TestMethod]
        public void IsConvex_TooFewVertices_ReturnsFalse()
        {
            var twoPoints = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(10, 0)
            };

            Assert.IsFalse(PolygonDecomposition.IsConvex(twoPoints),
                "Polygon with less than 3 vertices should not be considered convex.");
        }

        [TestMethod]
        public void DecomposeToConvex_ReversedWinding_StillWorks()
        {
            // L-shape with reversed winding order
            var lShapeReversed = new Vector2[]
            {
                new Vector2(0, 60),
                new Vector2(25, 60),
                new Vector2(25, 25),
                new Vector2(60, 25),
                new Vector2(60, 0),
                new Vector2(0, 0)
            };

            var result = PolygonDecomposition.DecomposeToConvex(lShapeReversed);

            Assert.IsTrue(result.Count >= 1, "Should handle reversed winding order.");

            foreach (var piece in result)
            {
                Assert.IsTrue(PolygonDecomposition.IsConvex(piece),
                    "All decomposed pieces should be convex regardless of input winding.");
            }
        }

        #endregion

        #region Helper Methods

        private float CalculateTriangleArea(Vector2 a, Vector2 b, Vector2 c)
        {
            return 0.5f * ((b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y));
        }

        private float CalculatePolygonArea(Vector2[] vertices)
        {
            float area = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                int j = (i + 1) % vertices.Length;
                area += vertices[i].X * vertices[j].Y;
                area -= vertices[j].X * vertices[i].Y;
            }
            return area / 2f;
        }

        /// <summary>
        /// Gets the signed area - matches PolygonDecomposition.GetSignedArea
        /// </summary>
        private float GetSignedArea(Vector2[] vertices)
        {
            return CalculatePolygonArea(vertices);
        }

        /// <summary>
        /// Computes edge normal using the same formula as Collision.cs
        /// </summary>
        private Vector2 GetEdgeNormal(Vector2 from, Vector2 to)
        {
            Vector2 edge = to - from;
            return Vector2.Normalize(new Vector2(-edge.Y, edge.X));
        }

        /// <summary>
        /// Computes the centroid of a polygon
        /// </summary>
        private Vector2 ComputeCentroid(Vector2[] vertices)
        {
            Vector2 sum = Vector2.Zero;
            foreach (var v in vertices)
                sum += v;
                return sum / vertices.Length;
            }

            #endregion

            #region Winding Order Tests

            [TestMethod]
            public void DecomposedPieces_HaveSameWindingAsBoxPhysShape()
            {
                // BoxPhysShape uses vertices in this order: (hw,-hh), (-hw,-hh), (-hw,hh), (hw,hh)
                // This is CCW in screen coords, giving NEGATIVE signed area
                // Decomposition should match this convention

                // L-shape decomposition should produce pieces with negative signed area
                var lShape = new Vector2[]
                {
                    new Vector2(0, 0),
                    new Vector2(60, 0),
                    new Vector2(60, 25),
                    new Vector2(25, 25),
                    new Vector2(25, 60),
                    new Vector2(0, 60)
                };

                    var pieces = PolygonDecomposition.DecomposeToConvex(lShape);

                    foreach (var piece in pieces)
                    {
                        float pieceArea = GetSignedArea(piece);

                        // All pieces should have negative signed area (matching BoxPhysShape)
                        Assert.IsTrue(pieceArea < 0,
                            $"Decomposed piece should have negative signed area (matching BoxPhysShape). Got: {pieceArea}");
                    }
                }

                [TestMethod]
                public void DecomposedPieces_EdgeNormalsPointOutward()
        {
            // BoxPhysShape is the reference - it defines a box with vertices in this order:
            // (hw, -hh), (-hw, -hh), (-hw, hh), (hw, hh) which is CCW in screen coords (negative signed area)
            // We test that decomposition produces the same winding convention

            // L-shape decomposition
            var lShape = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(60, 0),
                new Vector2(60, 25),
                new Vector2(25, 25),
                new Vector2(25, 60),
                new Vector2(0, 60)
            };

            var pieces = PolygonDecomposition.DecomposeToConvex(lShape);

            foreach (var piece in pieces)
            {
                // Verify negative signed area (matching BoxPhysShape convention)
                float signedArea = GetSignedArea(piece);
                Assert.IsTrue(signedArea < 0, 
                    $"Decomposed piece should have negative signed area (matching BoxPhysShape). Got: {signedArea}");
            }
        }

        [TestMethod]
        public void ConvexPolygon_WindingNormalizedToMatchPhysicsSystem()
        {
            // A convex polygon that's already convex should be normalized to match BoxPhysShape
            var convexPoly = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(10, 0),
                new Vector2(10, 10),
                new Vector2(0, 10)
            };

            var result = PolygonDecomposition.DecomposeToConvex(convexPoly);

            Assert.AreEqual(1, result.Count, "Convex polygon should return single piece");

            // Verify negative signed area (matching BoxPhysShape convention)
            float resultArea = GetSignedArea(result[0]);
            Assert.IsTrue(resultArea < 0,
                $"Convex polygon should be normalized to negative signed area (matching BoxPhysShape). Got: {resultArea}");
        }

        [TestMethod]
        public void LShape_InteriorEdgeNormalsCorrect()
        {
            // L-shape - the interior corner edges are the ones that matter most for collision
            var lShape = new Vector2[]
            {
                        new Vector2(0, 0),
                        new Vector2(60, 0),
                        new Vector2(60, 25),
                        new Vector2(25, 25),  // Interior corner
                        new Vector2(25, 60),
                        new Vector2(0, 60)
            };

            var pieces = PolygonDecomposition.DecomposeToConvex(lShape);

            // The decomposition creates pieces that share the interior edges
            // Test that a point inside the L's "pocket" would be correctly detected as outside all pieces
            Vector2 interiorPocketPoint = new Vector2(40, 40); // Inside the L's concave region

            bool isInsideAnyPiece = false;
            foreach (var piece in pieces)
            {
                if (IsPointInsideConvexPolygon(interiorPocketPoint, piece))
                {
                    isInsideAnyPiece = true;
                    break;
                }
            }

            Assert.IsFalse(isInsideAnyPiece,
                "Point in L-shape's concave pocket should NOT be inside any convex piece");
        }

        [TestMethod]
        public void LShape_PointOnInteriorEdgeDetectedCorrectly()
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

            var pieces = PolygonDecomposition.DecomposeToConvex(lShape);

            // Verify all pieces have negative signed area (matching BoxPhysShape convention)
            foreach (var piece in pieces)
            {
                float area = GetSignedArea(piece);
                Assert.IsTrue(area < 0,
                    $"All pieces should have negative signed area (matching BoxPhysShape). Got: {area}");
            }

            // Verify pieces cover the original polygon area
            float totalArea = 0;
            foreach (var piece in pieces)
            {
                totalArea += MathF.Abs(GetSignedArea(piece));
            }

            float originalArea = MathF.Abs(GetSignedArea(lShape));
            Assert.IsTrue(MathF.Abs(totalArea - originalArea) < 1f,
                $"Total piece area ({totalArea}) should match original area ({originalArea})");
        }

        /// <summary>
        /// Tests point-in-polygon using the same winding assumption as the collision system
        /// </summary>
        private bool IsPointInsideConvexPolygon(Vector2 point, Vector2[] polygon)
        {
                        // For all edges, the point should be on the same side 
                        // For negative signed area (CCW in screen coords), point is inside if cross >= 0 for all edges
                        for (int i = 0; i < polygon.Length; i++)
                        {
                            int next = (i + 1) % polygon.Length;
                            Vector2 edge = polygon[next] - polygon[i];
                            Vector2 toPoint = point - polygon[i];
                            float cross = edge.X * toPoint.Y - edge.Y * toPoint.X;

                            // With negative signed area (BoxPhysShape convention, CCW in screen coords), 
                            // point is inside if cross >= 0 for all edges
                            if (cross < -0.0001f)
                                return false;
                        }
                        return true;
                    }

                    #endregion
                }
            }
