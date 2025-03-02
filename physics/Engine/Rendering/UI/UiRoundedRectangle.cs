using SFML.Graphics;
using SFML.System;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace physics.Engine.Rendering.UI
{
    public class UiRoundedRectangle : aUiElement
    {
        private Vector2f[] _points;

        public UiRoundedRectangle(Vector2f size, float radius, int quality)
        {
            _points = GeneratedRoundedRectangleBorderPoints(size, 2f, radius, quality).ToArray();
        }

        private IEnumerable<Vector2f> GeneratedRoundedRectangleBorderPoints(Vector2f size, float thickness, float radius, int quality)
        {
            return MergeEveryOther(GeneratedRoundedRectanglePoints(size, radius, quality, thickness), GeneratedRoundedRectanglePoints(size, radius, quality, 0));
        }

        private IEnumerable<Vector2f> GeneratedRoundedRectanglePoints(Vector2f size, float radius, int quality, float thickness = 0)
        {
            // Determine arc quality per corner.
            // (Assumes that "quality" is the total number of points, so each quadrant gets quality/4 points.)
            int arcQuality = Math.Max(2, quality / 4);

            // thickness for outline (if any)
            radius += thickness;

            // Compute the centers for the four corner circles:
            Vector2f bottomRightCenter = new Vector2f(size.X - radius + thickness , size.Y - radius + thickness);
            Vector2f bottomLeftCenter = new Vector2f(radius - thickness , size.Y - radius + thickness);
            Vector2f topLeftCenter = new Vector2f(radius - thickness, radius - thickness);
            Vector2f topRightCenter = new Vector2f(size.X - radius + thickness, radius - thickness);

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

            return finalPoints;
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
            VertexArray fan = new VertexArray(PrimitiveType.TriangleStrip);

            // Append all the outline (arc) points.
            foreach (var pt in _points)
            {
                fan.Append(new Vertex(pt + Position, Color.White));
            }

            // Close the fan by repeating the first outline point.
            if (_points.Length > 1)
            {
                fan.Append(new Vertex(_points[0] + Position, Color.White));
                fan.Append(new Vertex(_points[1] + Position, Color.White));
            }

                target.Draw(fan);
        }

        public static IEnumerable<Vector2f> MergeEveryOther(IEnumerable<Vector2f> first, IEnumerable<Vector2f> second)
        {
            using (var enum1 = first.GetEnumerator())
            using (var enum2 = second.GetEnumerator())
            {
                bool hasFirst = enum1.MoveNext();
                bool hasSecond = enum2.MoveNext();

                // Continue until both enumerators are exhausted.
                while (hasFirst || hasSecond)
                {
                    if (hasFirst)
                    {
                        yield return enum1.Current;
                        hasFirst = enum1.MoveNext();
                    }
                    if (hasSecond)
                    {
                        yield return enum2.Current;
                        hasSecond = enum2.MoveNext();
                    }
                }
            }
        }

    }
}
