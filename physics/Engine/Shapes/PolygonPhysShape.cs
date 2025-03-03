using SFML.System;
using physics.Engine.Structs;
using physics.Engine.Helpers;
using System;
using System.Collections.Generic;

namespace physics.Engine.Shapes
{
    public class PolygonPhysShape : IShape
    {
        /// <summary>
        /// Local-space vertices of the polygon, in clockwise or counterclockwise order.
        /// </summary>
        public List<Vector2f> LocalVertices { get; }
        List<Vector2f> IShape.LocalVertices { get => LocalVertices; set => throw new NotImplementedException(); }

        // Precomputed bounding box in local space, just for convenience (or you can compute on the fly).
        private float _localMinX;
        private float _localMaxX;
        private float _localMinY;
        private float _localMaxY;

        /// <summary>
        /// Constructs a new polygon shape from a list of local vertices.
        /// The vertices should define a closed, convex polygon in either clockwise or CCW order.
        /// </summary>
        public PolygonPhysShape(IEnumerable<Vector2f> vertices)
        {
            LocalVertices = new List<Vector2f>(vertices);

            // Precompute local bounding extents for width/height.
            // Also, you may want to ensure the polygon is convex here (optional).
            _localMinX = float.MaxValue;
            _localMaxX = float.MinValue;
            _localMinY = float.MaxValue;
            _localMaxY = float.MinValue;

            foreach (var v in LocalVertices)
            {
                if (v.X < _localMinX) _localMinX = v.X;
                if (v.X > _localMaxX) _localMaxX = v.X;
                if (v.Y < _localMinY) _localMinY = v.Y;
                if (v.Y > _localMaxY) _localMaxY = v.Y;
            }
        }

        /// <summary>
        /// The Axis-Aligned Bounding Box for this polygon when placed at 'center' and rotated by 'angle'.
        /// </summary>
        public AABB GetAABB(Vector2f center, float angle)
        {
            // We'll rotate each local vertex by 'angle', offset by center, and track min & max.
            float cos = (float)Math.Cos(angle);
            float sin = (float)Math.Sin(angle);

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;

            foreach (var v in LocalVertices)
            {
                // Rotate point v by angle around origin, then translate by center
                float rx = v.X * cos - v.Y * sin;
                float ry = v.X * sin + v.Y * cos;
                float worldX = center.X + rx;
                float worldY = center.Y + ry;

                if (worldX < minX) minX = worldX;
                if (worldX > maxX) maxX = worldX;
                if (worldY < minY) minY = worldY;
                if (worldY > maxY) maxY = worldY;
            }

            return new AABB
            {
                Min = new Vector2f(minX, minY),
                Max = new Vector2f(maxX, maxY)
            };
        }

        /// <summary>
        /// Returns the polygon's area (using the shoelace formula).
        /// </summary>
        public float GetArea()
        {
            // Shoelace formula for the local vertices.
            // area = |(sum over i of (x_i * y_{i+1} - x_{i+1} * y_i)) / 2|
            float area = 0f;
            int count = LocalVertices.Count;
            for (int i = 0; i < count; i++)
            {
                int j = (i + 1) % count;
                area += LocalVertices[i].X * LocalVertices[j].Y - LocalVertices[j].X * LocalVertices[i].Y;
            }
            return Math.Abs(area) * 0.5f;
        }

        /// <summary>
        /// Moment of inertia for a solid convex polygon of uniform density.
        /// This uses a standard polygon inertia derivation: (1/12) * mass * (width^2 + height^2) for a bounding box 
        /// or a more direct formula for general polygons. Below is a typical direct approach for polygons.
        /// 
        /// For more complex shapes or concave polygons, additional checks may be needed.
        /// </summary>
        public float GetMomentOfInertia(float mass)
        {
            // We'll use a standard formula for polygon inertia about the origin, then scale by mass / area.
            // Reference: https://en.wikipedia.org/wiki/List_of_moments_of_inertia#List_of_2D_shapes
            // You might want to compute area once, store it, etc.

            float area = GetArea();
            if (area < 1e-6f)
                return 0f; // Degenerate polygon

            float denom = 0;
            float numer = 0;

            // We iterate over edges
            for (int i = 0; i < LocalVertices.Count; i++)
            {
                int j = (i + 1) % LocalVertices.Count;
                Vector2f v0 = LocalVertices[i];
                Vector2f v1 = LocalVertices[j];

                float cross = (v0.X * v1.Y - v1.X * v0.Y);
                float x0_sq = v0.X * v0.X + v0.X * v1.X + v1.X * v1.X;
                float y0_sq = v0.Y * v0.Y + v0.Y * v1.Y + v1.Y * v1.Y;

                numer += cross * (x0_sq + y0_sq);
                denom += cross;
            }

            // scale factor
            float iPoly = (1f / 12f) * numer;
            iPoly = Math.Abs(iPoly);

            // The “raw” polygon inertia is about the origin; we must divide by absolute cross-sum to handle sign
            float crossSum = Math.Abs(denom);
            iPoly /= crossSum;

            // Scale by mass. 
            // Because we used the polygon formula that’s effectively for “unit density,” multiply by total mass / area:
            float iResult = iPoly * (mass / area);

            return iResult;
        }

        /// <summary>
        /// Returns true if the given world-space point is inside this polygon, assuming the polygon is 
        /// positioned at 'center' with rotation 'angle'.
        /// </summary>
        public bool Contains(Vector2f point, Vector2f center, float angle)
        {
            // Transform 'point' into local space of the polygon
            Vector2f local = WorldToLocalPoint(point, center, angle);

            // Ray-casting or winding number approach. Here's a simple ray-cast:
            // (For brevity, a naive winding or crossing approach is shown.)
            int count = LocalVertices.Count;
            bool inside = false;
            for (int i = 0; i < count; i++)
            {
                int j = (i + 1) % count;
                Vector2f v0 = LocalVertices[i];
                Vector2f v1 = LocalVertices[j];

                bool intersect = ((v0.Y > local.Y) != (v1.Y > local.Y))
                                 && (local.X < (v1.X - v0.X) * (local.Y - v0.Y) / (v1.Y - v0.Y) + v0.X);
                if (intersect)
                    inside = !inside;
            }
            return inside;
        }

        /// <summary>
        /// Returns the total width in local space (without rotation). 
        /// This is simply maxX - minX across the polygon's local vertices.
        /// </summary>
        public float GetWidth()
        {
            return _localMaxX - _localMinX;
        }

        /// <summary>
        /// Returns the total height in local space (without rotation).
        /// This is simply maxY - minY across the polygon's local vertices.
        /// </summary>
        public float GetHeight()
        {
            return _localMaxY - _localMinY;
        }

        public Vector2f WorldToLocalPoint(Vector2f worldPoint, Vector2f center, float angle)
        {
            // Translate
            Vector2f translated = worldPoint - center;

            // Rotate by -angle
            float cos = (float)Math.Cos(-angle);
            float sin = (float)Math.Sin(-angle);
            float rx = translated.X * cos - translated.Y * sin;
            float ry = translated.X * sin + translated.Y * cos;

            return new Vector2f(rx, ry);
        }
    }
}
