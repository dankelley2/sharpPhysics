using SFML.Graphics;
using SFML.System;
using System;
using System.Collections.Generic;

namespace physics.Engine.Rendering.UI
{
    public class UiRoundedRectangle : aUiElement
    {
        Vector2f[] _points;
        Vector2f _origin; // center of the rectangle
        Vector2f _size;
        float _cornerRadius;

        public UiRoundedRectangle(Vector2f size, float radius, int quality)
        {
            _size = size;
            _cornerRadius = radius;
            // Compute the rectangle center (for the triangle fan)
            _origin = new Vector2f(size.X / 2, size.Y / 2);

            // Determine arc quality per corner.
            // (Assumes that "quality" is the total number of points, so each quadrant gets quality/4 points.)
            int arcQuality = Math.Max(2, quality / 4);

            // Compute the centers for the four corner circles:
            Vector2f bottomRightCenter = new Vector2f(size.X - radius, size.Y - radius);
            Vector2f bottomLeftCenter = new Vector2f(radius, size.Y - radius);
            Vector2f topLeftCenter = new Vector2f(radius, radius);
            Vector2f topRightCenter = new Vector2f(size.X - radius, radius);

            // Generate arc points for each corner:
            List<Vector2f> bottomRightArc = GetArcPoints(bottomRightCenter, radius, 0f, 90f, arcQuality);
            List<Vector2f> bottomLeftArc = GetArcPoints(bottomLeftCenter, radius, 90f, 180f, arcQuality);
            List<Vector2f> topLeftArc = GetArcPoints(topLeftCenter, radius, 180f, 270f, arcQuality);
            List<Vector2f> topRightArc = GetArcPoints(topRightCenter, radius, 270f, 360f, arcQuality);

            // Combine the arcs in clockwise order.
            List<Vector2f> finalPoints =
            [
                .. bottomRightArc,
                .. bottomLeftArc,
                .. topLeftArc,
                .. topRightArc,
            ];

            _points = finalPoints.ToArray();
        }

        /// <summary>
        /// Generates a list of points along an arc of a circle.
        /// </summary>
        /// <param name="center">The center of the circle.</param>
        /// <param name="radius">The radius of the circle.</param>
        /// <param name="startAngleDeg">Starting angle in degrees.</param>
        /// <param name="endAngleDeg">Ending angle in degrees.</param>
        /// <param name="numPoints">Number of points to generate along the arc.</param>
        /// <returns>A list of points along the arc.</returns>
        private List<Vector2f> GetArcPoints(Vector2f center, float radius, float startAngleDeg, float endAngleDeg, int numPoints)
        {
            List<Vector2f> arcPoints = new List<Vector2f>();
            // Using numPoints-1 so that both endpoints are included.
            for (int i = 0; i < numPoints; i++)
            {
                float t = i / (float)(numPoints - 1);
                float angleDeg = startAngleDeg + t * (endAngleDeg - startAngleDeg);
                float angleRad = angleDeg * (float)Math.PI / 180f;
                float x = center.X + radius * (float)Math.Cos(angleRad);
                float y = center.Y + radius * (float)Math.Sin(angleRad);
                arcPoints.Add(new Vector2f(x, y));
            }
            return arcPoints;
        }

        protected override void DrawSelf(RenderTarget target)
        {
            // Draw the rounded rectangle using a TriangleFan.
            VertexArray fan = new VertexArray(PrimitiveType.TriangleFan);

            // First vertex: center of the rectangle.
            //fan.Append(new Vertex(_origin, Color.White));

            // Append all the outline (arc) points.
            foreach (var pt in _points)
            {
                fan.Append(new Vertex(pt, Color.White));
            }

            // Close the fan by repeating the first outline point.
            if (_points.Length > 0)
                fan.Append(new Vertex(_points[0], Color.White));

            target.Draw(fan);
        }
    }
}
