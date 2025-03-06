using SFML.System;
using physics.Engine.Structs;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace physics.Engine.Shapes
{
    public class PolygonPhysShape : IShape
    {
        /// <summary>
        /// Local-space vertices of the polygon, in clockwise or counterclockwise order.
        /// </summary>
        public List<Vector2> LocalVertices { get; set; }

        // Precomputed bounding box in local space, just for convenience (or you can compute on the fly).
        private float _localMinX;
        private float _localMaxX;
        private float _localMinY;
        private float _localMaxY;

        /// <summary>
        /// Constructs a new polygon shape from a list of local vertices.
        /// The vertices should define a closed, convex polygon in either clockwise or CCW order.
        /// </summary>
        public PolygonPhysShape(IEnumerable<Vector2> vertices)
        {
            LocalVertices = new(vertices);

            Vector2 centroid = CollisionHelpers.ComputeCentroid(LocalVertices);

            // Then shift each vertex so the centroid is at (0,0)
            for (int i = 0; i < LocalVertices.Count; i++)
            {
                var v = LocalVertices[i];
                v -= centroid;
                LocalVertices[i] = v;
            }

            // 4) Recalculate _localMinX, etc., now that we've shifted everything
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
        public AABB GetAABB(Vector2 center, float angle)
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
                Min = new Vector2(minX, minY),
                Max = new Vector2(maxX, maxY)
            };
        }

        /// <summary>
        /// Returns the polygon's area (using the shoelace formula), 
        /// always returning a positive value for both CW and CCW vertices.
        /// </summary>
        public float GetArea()
        {
            float total = 0f;
            int count = LocalVertices.Count;

            // Sum over edges, possibly negative for CW ordering
            for (int i = 0; i < count; i++)
            {
                int j = (i + 1) % count;
                total += (LocalVertices[i].X * LocalVertices[j].Y) 
                    - (LocalVertices[j].X * LocalVertices[i].Y);
            }

            // Multiply by 0.5 and take absolute value so the area is positive
            return Math.Abs(total * 0.5f);
        }

        public float GetMomentOfInertia(float mass)
        {
            // 1) Compute the polygon’s area using the shoelace formula.
            float area = GetArea(); 
            if (area < 1e-6f)
                return 0f; // Degenerate polygon

            float crossSum = 0f;
            float numer = 0f;

            // 2) Sum over each edge in the polygon.
            for (int i = 0; i < LocalVertices.Count; i++)
            {
                int j = (i + 1) % LocalVertices.Count;
                Vector2 v0 = LocalVertices[i];
                Vector2 v1 = LocalVertices[j];

                float cross = (v0.X * v1.Y - v1.X * v0.Y);
                float termX = (v0.X * v0.X) + (v0.X * v1.X) + (v1.X * v1.X);
                float termY = (v0.Y * v0.Y) + (v0.Y * v1.Y) + (v1.Y * v1.Y);

                numer += cross * (termX + termY);
                crossSum += cross;
            }

            crossSum = Math.Abs(crossSum);
            if (crossSum < 1e-8f)
                return 0f;  // Nearly degenerate polygon

            // 3) Use the standard formula:
            // I = (mass/(6 * sum(cross))) * numer
            float iPoly = (mass * numer) / (6f * crossSum);
            return Math.Abs(iPoly);
        }


        /// <summary>
        /// Returns true if the given world-space point is inside this polygon, assuming the polygon is 
        /// positioned at 'center' with rotation 'angle'.
        /// </summary>
        public bool Contains(Vector2 point, Vector2 center, float angle)
        {
            // Transform 'point' into local space of the polygon
            Vector2 local = WorldToLocalPoint(point, center, angle);

            // Ray-casting or winding number approach. Here's a simple ray-cast:
            // (For brevity, a naive winding or crossing approach is shown.)
            int count = LocalVertices.Count;
            bool inside = false;
            for (int i = 0; i < count; i++)
            {
                int j = (i + 1) % count;
                Vector2 v0 = LocalVertices[i];
                Vector2 v1 = LocalVertices[j];

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

        public Vector2 WorldToLocalPoint(Vector2 worldPoint, Vector2 center, float angle)
        {
            // Translate
            Vector2 translated = worldPoint - center;

            // Rotate by -angle
            float cos = (float)Math.Cos(-angle);
            float sin = (float)Math.Sin(-angle);
            float rx = translated.X * cos - translated.Y * sin;
            float ry = translated.X * sin + translated.Y * cos;

            return new Vector2(rx, ry);
        }
    }
}

