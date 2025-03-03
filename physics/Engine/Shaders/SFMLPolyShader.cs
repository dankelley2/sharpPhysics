using SFML.Graphics;
using SFML.System;
using physics.Engine.Classes;
using physics.Engine.Objects;
using physics.Engine.Shapes;
using System;
using System.Collections.Generic;

namespace physics.Engine.Shaders
{
    public class SFMLPolyShader : SFMLShader
    {
        // Stores the polygon's outline points
        private VertexArray vertexes = new VertexArray(PrimitiveType.LineStrip);

        public override void PreDraw(PhysicsObject obj, RenderTarget target)
        {
            // No setup needed here
        }

        public override void Draw(PhysicsObject obj, RenderTarget target)
        {
            // Clear any previous points
            vertexes.Clear();

            // Convert local polygon vertices into world space
            Vector2f[] points = obj.Shape.GetTransformedVertices(obj.Center, obj.Angle);

            // Add each vertex to our VertexArray
            for (int i = 0; i < points.Length; i++)
            {
                if (i == 0)
                    vertexes.Append(new Vertex(points[i], Color.Red));
                else if (i == 1)
                    vertexes.Append(new Vertex(points[i], Color.Green));
                else 
                    vertexes.Append(new Vertex(points[i], Color.White));
            }

            // Close the loop (connect last back to first)
            if (points.Length > 0)
            {
                vertexes.Append(new Vertex(points[0], Color.White));
            }
            
        }

        public override void PostDraw(PhysicsObject obj, RenderTarget target)
        {
            // 1) Draw the polygon outline
            target.Draw(vertexes);

            // 2) Draw a small red circle around the object's center
            float radius = 5f;
            CircleShape circle = new CircleShape(radius);
            circle.Origin = new Vector2f(radius, radius); // So it rotates/positions around its center
            circle.Position = obj.Center;                 // Center of the polygon / PhysicsObject
            circle.OutlineThickness = 1f;
            circle.OutlineColor = Color.Red;
            circle.FillColor = Color.Transparent;         

            target.Draw(circle);
        }
    }
}
