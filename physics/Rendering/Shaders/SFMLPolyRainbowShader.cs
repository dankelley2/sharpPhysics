using SFML.Graphics;
using System.Numerics;
using physics.Engine.Objects;
using physics.Engine.Helpers;
using SFML.System;
using System;

namespace SharpPhysics.Rendering.Shaders
{
    public class SFMLPolyRainbowShader : SFMLShader
    {
        // Stores the polygon's outline points.
        private VertexArray vertexes = new VertexArray(PrimitiveType.TriangleFan);
        // Preallocated VertexArray for drawing contact normals.
        private VertexArray contactLines = new VertexArray(PrimitiveType.Lines);

        public static bool DrawSleepSupports {get; set;} = false;
        
        private Color _color;

        public override void PreDraw(PhysicsObject obj, RenderTarget target)
        {

            // Color based on velocity.
            double particleSpeed = 220 - Math.Min((int)obj.Velocity.Length(), 220);
            double hue = particleSpeed % 360;
            
            if (obj.Sleeping)
            {
                _color = new Color(50,50,50);
            }
            else
            {
                ShaderHelpers.HsvToRgb(hue, 1, 1, out int red, out int green, out int blue);
                _color = new Color((byte)red, (byte)green, (byte)blue);
            }
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
                vertexes.Append(new Vertex(points[i].ToSfml(), _color));
            }

            // Close the loop.
            if (points.Length > 0)
            {
                vertexes.Append(new Vertex(points[0].ToSfml(), _color));
            }

            // 1) Draw the polygon outline.
            target.Draw(vertexes);
        }

        public override void PostDraw(PhysicsObject obj, RenderTarget target)
        {
            if (!SFMLPolyShader.DrawNormals)
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