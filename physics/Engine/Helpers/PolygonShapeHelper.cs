using SFML.System;
using System;
using System.Collections.Generic;

namespace physics.Engine.Helpers
{
    public static class PolygonShapeHelper
    {
        /// <summary>
        /// Creates vertices for a circle (convex polygon approximation) of a given radius and resolution in CCW order.
        /// Then reverses the list before returning.
        /// </summary>
        public static List<Vector2f> CreateCircleVertices(int resolution, float radius)
        {
            List<Vector2f> vertices = new List<Vector2f>();
            float twoPi = (float)(Math.PI * 2);
            // CCW order: iterate from 0 to 2π.
            for (int i = 0; i < resolution; i++)
            {
                float angle = twoPi * i / resolution;
                float x = radius * (float)Math.Cos(angle);
                float y = radius * (float)Math.Sin(angle);
                vertices.Add(new Vector2f(x, y));
            }
            vertices.Reverse();
            return vertices;
        }

        /// <summary>
        /// Creates vertices for a box of a given width and height in CCW order.
        /// For a box centered at (0,0), the vertices are:
        /// bottom-left, top-left, top-right, bottom-right.
        /// Then reverses the list before returning.
        /// </summary>
        public static List<Vector2f> CreateBoxVertices(float width, float height)
        {
            List<Vector2f> vertices = new List<Vector2f>
            {
                new Vector2f(-width / 2f, -height / 2f), // bottom-left
                new Vector2f(-width / 2f,  height / 2f), // top-left
                new Vector2f( width / 2f,  height / 2f), // top-right
                new Vector2f( width / 2f, -height / 2f)  // bottom-right
            };
            vertices.Reverse();
            return vertices;
        }

        /// <summary>
        /// Creates vertices for a vertical capsule shape in CCW order.
        /// The capsule is built from two semicircular caps (top and bottom) and two vertical edges.
        /// Then reverses the list before returning.
        /// Parameters:
        ///   resolution: number of segments for each semicircular cap (minimum 2 recommended).
        ///   radius: radius of the semicircular caps.
        ///   bodyLength: vertical distance between the flat edges of the caps.
        /// The overall capsule height will be bodyLength + 2 * radius.
        /// 
        /// Vertex ordering (CCW):
        ///   1. Start at the bottom-right corner.
        ///   2. Right vertical edge (from bottom to top).
        ///   3. Top cap (arc from right to left).
        ///   4. Left vertical edge (from top to bottom).
        ///   5. Bottom cap (arc from left to right).
        /// Then the list is reversed.
        /// </summary>
        public static List<Vector2f> CreateCapsuleVertices(int resolution, float radius, float bodyLength)
        {
            if (resolution < 2)
                throw new ArgumentException("Resolution must be at least 2.", nameof(resolution));

            List<Vector2f> vertices = new List<Vector2f>();
            float halfBody = bodyLength / 2f;
            
            // Define centers for the caps.
            Vector2f topCenter = new Vector2f(0, halfBody);
            Vector2f bottomCenter = new Vector2f(0, -halfBody);

            // --- Right Vertical Edge ---
            // Start at the bottom-right vertex.
            Vector2f start = new Vector2f(radius, -halfBody);
            vertices.Add(start);
            // Next vertex: top-right.
            Vector2f rightEdge = new Vector2f(radius, halfBody);
            vertices.Add(rightEdge);

            // --- Top Cap ---
            // Generate top cap vertices (arc from right to left) about topCenter.
            // Let angle vary from 0 to π.
            for (int i = 1; i <= resolution; i++)
            {
                float angle = (float)Math.PI * i / resolution; // from 0 to π.
                float x = topCenter.X + radius * (float)Math.Cos(angle);
                float y = topCenter.Y + radius * (float)Math.Sin(angle);
                vertices.Add(new Vector2f(x, y));
            }

            // --- Left Vertical Edge ---
            // Add the left vertical edge from top to bottom.
            Vector2f leftEdge = new Vector2f(-radius, -halfBody);
            vertices.Add(leftEdge);

            // --- Bottom Cap ---
            // Generate bottom cap vertices (arc from left to right) about bottomCenter.
            // Let angle vary from π to 2π, skipping the first (duplicate leftEdge) and last (duplicate start).
            for (int i = 1; i < resolution; i++)
            {
                float angle = (float)Math.PI + (float)Math.PI * i / resolution; // from π to 2π.
                float x = bottomCenter.X + radius * (float)Math.Cos(angle);
                float y = bottomCenter.Y + radius * (float)Math.Sin(angle);
                vertices.Add(new Vector2f(x, y));
            }

            vertices.Reverse();
            return vertices;
        }
    }
}