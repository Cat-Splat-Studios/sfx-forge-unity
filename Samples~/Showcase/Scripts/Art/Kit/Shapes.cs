using System.Collections.Generic;
using UnityEngine;

namespace SfxShowcase.Art
{
    /// <summary>
    /// The outlines of the few convex shapes everything is made of, as corner lists in counter-clockwise order.
    /// Friendly things are built from the round ones, dangerous things from the angular ones (DESIGN.md, "Shape
    /// language"); anything more complicated is several of these put together in an <see cref="InkedShape"/>.
    /// </summary>
    public static class Shapes
    {
        const float LongestMiter = 3f;

        /// <summary>An ellipse (a circle when both radii are equal).</summary>
        public static List<Vector2> Ellipse(
            Vector2 center,
            float radiusX,
            float radiusY,
            int segments = ArtStyle.CircleSegments
        )
        {
            var points = new List<Vector2>(segments);
            for (int i = 0; i < segments; i++)
            {
                Vector2 direction = MeshBuilder.Direction(360f * i / segments);
                points.Add(center + new Vector2(direction.x * radiusX, direction.y * radiusY));
            }

            return points;
        }

        /// <summary>An axis-aligned rectangle.</summary>
        public static List<Vector2> Rect(Vector2 min, Vector2 max)
        {
            return new List<Vector2> { min, new Vector2(max.x, min.y), max, new Vector2(min.x, max.y) };
        }

        /// <summary>An axis-aligned rectangle with rounded corners.</summary>
        public static List<Vector2> RoundedRect(Vector2 min, Vector2 max, float cornerRadius, int cornerSegments = 4)
        {
            var points = new List<Vector2>(4 * (cornerSegments + 1));
            AddCorner(
                points,
                new Vector2(min.x + cornerRadius, min.y + cornerRadius),
                cornerRadius,
                180f,
                cornerSegments
            );
            AddCorner(
                points,
                new Vector2(max.x - cornerRadius, min.y + cornerRadius),
                cornerRadius,
                270f,
                cornerSegments
            );
            AddCorner(
                points,
                new Vector2(max.x - cornerRadius, max.y - cornerRadius),
                cornerRadius,
                0f,
                cornerSegments
            );
            AddCorner(
                points,
                new Vector2(min.x + cornerRadius, max.y - cornerRadius),
                cornerRadius,
                90f,
                cornerSegments
            );
            return points;
        }

        /// <summary>
        /// A diamond centred on the origin: a square turned 45° with slightly rounded corners, stretched to the given
        /// half width and half height. roundness is the corner radius as a fraction of the square's half side (0..1).
        /// </summary>
        public static List<Vector2> Diamond(float halfWidth, float halfHeight, float roundness)
        {
            List<Vector2> points = RoundedRect(-Vector2.one, Vector2.one, roundness);

            // A unit square's corner is sqrt(2) from the centre; rounding pulls it in by radius * (sqrt(2) - 1).
            float reach = Mathf.Sqrt(2f) - roundness * (Mathf.Sqrt(2f) - 1f);
            Vector2 diagonal = MeshBuilder.Direction(45f);
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 point = points[i];
                var turned = new Vector2(
                    point.x * diagonal.x - point.y * diagonal.y,
                    point.x * diagonal.y + point.y * diagonal.x
                );
                points[i] = new Vector2(turned.x / reach * halfWidth, turned.y / reach * halfHeight);
            }

            return points;
        }

        /// <summary>
        /// A teardrop / leaf: a round end of the given radius centred on the origin, drawn out to a point at
        /// (0, length). length must be greater than radius.
        /// </summary>
        public static List<Vector2> Teardrop(float length, float radius, int segments = ArtStyle.CircleSegments / 2)
        {
            // The straight sides touch the round end where the line from the tip is tangent to the circle.
            float tangent = Mathf.Acos(Mathf.Clamp01(radius / length)) * Mathf.Rad2Deg;
            float start = 90f + tangent;
            float end = 450f - tangent;
            var points = new List<Vector2>(segments + 2);
            for (int i = 0; i <= segments; i++)
            {
                points.Add(MeshBuilder.Direction(Mathf.Lerp(start, end, i / (float)segments)) * radius);
            }

            points.Add(new Vector2(0f, length));
            return points;
        }

        /// <summary>
        /// The same convex polygon grown outwards by a distance on every side (the Ink copy behind an outlined shape).
        /// Works for either winding.
        /// </summary>
        public static List<Vector2> Outset(IReadOnlyList<Vector2> convexPolygon, float distance)
        {
            int count = convexPolygon.Count;
            float outwards = SignedArea(convexPolygon) >= 0f ? 1f : -1f;
            var grown = new List<Vector2>(count);
            for (int i = 0; i < count; i++)
            {
                Vector2 previous = convexPolygon[(i + count - 1) % count];
                Vector2 current = convexPolygon[i];
                Vector2 next = convexPolygon[(i + 1) % count];
                Vector2 normalBefore = RightNormal(previous, current) * outwards;
                Vector2 normalAfter = RightNormal(current, next) * outwards;
                grown.Add(current + Miter(normalBefore, normalAfter, distance));
            }

            return grown;
        }

        /// <summary>
        /// The unit normal on the right-hand side of the direction a→b (outwards for a counter-clockwise polygon,
        /// into the ground for a surface drawn left to right); zero when the two points coincide.
        /// </summary>
        public static Vector2 RightNormal(Vector2 a, Vector2 b)
        {
            Vector2 along = b - a;
            if (along.sqrMagnitude < 1e-10f)
            {
                return Vector2.zero;
            }

            along.Normalize();
            return new Vector2(along.y, -along.x);
        }

        /// <summary>
        /// How far to move a corner so that both edges meeting there move by the given distance along their unit
        /// normals. Very sharp corners are cut short at three times the distance so spikes do not grow needles.
        /// </summary>
        public static Vector2 Miter(Vector2 normalBefore, Vector2 normalAfter, float distance)
        {
            if (normalBefore == Vector2.zero)
            {
                normalBefore = normalAfter;
            }

            if (normalAfter == Vector2.zero)
            {
                normalAfter = normalBefore;
            }

            float denominator = 1f + Vector2.Dot(normalBefore, normalAfter);
            if (denominator < 1e-4f)
            {
                return normalBefore * distance;
            }

            Vector2 miter = (normalBefore + normalAfter) * (distance / denominator);
            return Vector2.ClampMagnitude(miter, distance * LongestMiter);
        }

        static void AddCorner(List<Vector2> points, Vector2 center, float radius, float startDegrees, int segments)
        {
            for (int i = 0; i <= segments; i++)
            {
                points.Add(center + MeshBuilder.Direction(startDegrees + 90f * i / segments) * radius);
            }
        }

        static float SignedArea(IReadOnlyList<Vector2> polygon)
        {
            float twiceArea = 0f;
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % polygon.Count];
                twiceArea += a.x * b.y - b.x * a.y;
            }

            return twiceArea * 0.5f;
        }
    }
}
