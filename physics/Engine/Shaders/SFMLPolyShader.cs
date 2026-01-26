using SFML.Graphics;
using System.Numerics;
using physics.Engine.Objects;
using physics.Engine.Helpers;
using SFML.System;

namespace physics.Engine.Shaders
{
    public class SFMLPolyShader : SFMLShader
    {
        // Stores the polygon's outline points.
        private VertexArray vertexes = new VertexArray(PrimitiveType.LineStrip);
        // Preallocated VertexArray for drawing contact normals.
        private VertexArray contactLines = new VertexArray(PrimitiveType.Lines);

        public static bool DrawNormals {get; set;} = false;
        public static bool DrawSleepSupports {get; set;} = false;

        public override void PreDraw(PhysicsObject obj, RenderTarget target)
        {
            // No setup needed here.
        }

        public override void Draw(PhysicsObject obj, RenderTarget target)
        {
            // Clear previous polygon vertices.
            vertexes.Clear();

            // Get transformed vertices.
            Vector2[] points = obj.Shape.GetTransformedVertices(obj.Center, obj.Angle);

            // Add vertices with some color coding.
            for (int i = 0; i < points.Length; i++)
            {
                if (i == 0)
                    vertexes.Append(new Vertex(points[i].ToSfml(), Color.Red));
                else if (i == 1)
                    vertexes.Append(new Vertex(points[i].ToSfml(), Color.Green));
                else 
                    vertexes.Append(new Vertex(points[i].ToSfml(), Color.White));
            }

            // Close the loop.
            if (points.Length > 0)
            {
                vertexes.Append(new Vertex(points[0].ToSfml(), Color.White));
            }
        }

        public override void PostDraw(PhysicsObject obj, RenderTarget target)
        {
            // 1) Draw the polygon outline.
            target.Draw(vertexes);

            // 2) Draw a small red circle at the object's center.
            float radius = 2f;
            CircleShape circle = new CircleShape(radius)
            {
                Origin = new Vector2f(radius, radius),
                Position = obj.Center.ToSfml(),
                OutlineThickness = 1f,
                OutlineColor = obj.Sleeping ? Color.Blue : Color.Red,
                FillColor = Color.Transparent
            };
            target.Draw(circle);

            if (!DrawNormals)
                return;
                
            // 3) Draw a line from each contact point along its normal.
            contactLines.Clear();
            float lineLength = 10f; // Adjust this value to scale the drawn normals.
            foreach (var kv in obj.GetContacts())
            {
                // kv.Key is the other PhysicsObject (unused here), and kv.Value is (contactPoint, normal).
                (Vector2 contactPoint, Vector2 normal) = kv.Value;
                contactLines.Append(new Vertex(contactPoint.ToSfml(), normal.Y > 0 ? Color.Yellow : Color.Cyan));
                contactLines.Append(new Vertex((contactPoint + normal * lineLength).ToSfml(), normal.Y > 0 ? Color.Yellow : Color.Cyan));
            }
            target.Draw(contactLines);
        }
    }
}