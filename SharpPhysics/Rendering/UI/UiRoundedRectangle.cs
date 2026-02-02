using SharpPhysics.Engine.Helpers;
using SFML.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SharpPhysics.Rendering.UI
{
    public class UiRoundedRectangle : UiElement
    {
        public Color OutlineColor { get; set; } = Color.White;
        public Vector2 Size { get; private set; }
        private Vector2[] _points;

        public UiRoundedRectangle(Vector2 size, float radius, int quality)
        {
            this.Size = size;
            _points = GeneratedRoundedRectangleBorderPoints(size, 2f, radius, quality).ToArray();
        }

        private IEnumerable<Vector2> GeneratedRoundedRectangleBorderPoints(Vector2 size, float thickness, float radius, int quality)
        {
            return MergeEveryOther(GeneratedRoundedRectanglePoints(size, radius, quality, thickness), GeneratedRoundedRectanglePoints(size, radius, quality, 0));
        }

        private IEnumerable<Vector2> GeneratedRoundedRectanglePoints(Vector2 size, float radius, int quality, float thickness = 0)
        {
            // Determine arc quality per corner.
            // (Assumes that "quality" is the total number of points, so each quadrant gets quality/4 points.)
            int arcQuality = Math.Max(2, quality / 4);

            // thickness for outline (if any)
            radius += thickness;

            // Compute the centers for the four corner circles:
            Vector2 bottomRightCenter = new Vector2(size.X - radius + thickness , size.Y - radius + thickness);
            Vector2 bottomLeftCenter = new Vector2(radius - thickness , size.Y - radius + thickness);
            Vector2 topLeftCenter = new Vector2(radius - thickness, radius - thickness);
            Vector2 topRightCenter = new Vector2(size.X - radius + thickness, radius - thickness);

            // Generate arc points for each corner:
            List<Vector2> bottomRightArc = GetArcPoints(new Vector2(bottomRightCenter.X, bottomRightCenter.Y), radius, 0f, 90f, arcQuality);
            List<Vector2> bottomLeftArc = GetArcPoints(new Vector2(bottomLeftCenter.X, bottomLeftCenter.Y), radius, 90f, 180f, arcQuality);
            List<Vector2> topLeftArc = GetArcPoints(new Vector2(topLeftCenter.X, topLeftCenter.Y), radius, 180f, 270f, arcQuality);
            List<Vector2> topRightArc = GetArcPoints(new Vector2(topRightCenter.X, topRightCenter.Y), radius, 270f, 360f, arcQuality);

            // Combine the arcs in clockwise order.
            List<Vector2> finalPoints =
            [
                .. bottomRightArc.Select(p => new Vector2(p.X, p.Y)),
                .. bottomLeftArc.Select(p => new Vector2(p.X, p.Y)),
                .. topLeftArc.Select(p => new Vector2(p.X, p.Y)),
                .. topRightArc.Select(p => new Vector2(p.X, p.Y)),
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
        private List<Vector2> GetArcPoints(Vector2 center, float radius, float startAngleDeg, float endAngleDeg, int numPoints)
        {
            List<Vector2> arcPoints = new List<Vector2>();
            // Using numPoints-1 so that both endpoints are included.
            for (int i = 0; i < numPoints; i++)
            {
                float t = i / (float)(numPoints - 1);
                float angleDeg = startAngleDeg + t * (endAngleDeg - startAngleDeg);
                float angleRad = angleDeg * (float)Math.PI / 180f;
                float x = center.X + radius * (float)Math.Cos(angleRad);
                float y = center.Y + radius * (float)Math.Sin(angleRad);
                arcPoints.Add(new Vector2(x, y));
            }
            return arcPoints;
        }

        protected override void DrawSelf(RenderTarget target)
        {
            // Draw the rounded rectangle using a TriangleFan.
            VertexArray triStrip = new VertexArray(PrimitiveType.TriangleStrip);

            // Append all the outline (arc) points.
            int i = 0;
            foreach (var pt in _points)
            {
                if ( i % 2 == 0)
                    triStrip.Append(new Vertex((pt + Position).ToSfml(), Color.White));
                else 
                    triStrip.Append(new Vertex((pt + Position).ToSfml(), this.OutlineColor));
                i++;
            }

            // Close the fan by repeating the first outline point.
            if (_points.Length > 1)
            {
                triStrip.Append(new Vertex((_points[0] + Position).ToSfml(), Color.White));
                triStrip.Append(new Vertex((_points[1] + Position).ToSfml(), this.OutlineColor));
            }

                target.Draw(triStrip);
        }

        public static IEnumerable<Vector2> MergeEveryOther(IEnumerable<Vector2> first, IEnumerable<Vector2> second)
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

        // New clickable behavior.
        public override bool HandleClick(Vector2 clickPos)
        {
            return false;
        }
    }
}
