using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SfxShowcase.Art
{
    /// <summary>
    /// Collects flat, vertex-coloured triangles in the xy plane and turns them into a mesh. Shapes are drawn in the
    /// order they are added (painter's order: the materials write no depth), so add what is behind first.
    /// Reusable: <see cref="Clear"/> it and fill it again for meshes that change every frame.
    /// </summary>
    public sealed class MeshBuilder
    {
        const int MaxVerticesFor16BitIndices = 65535;

        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> triangles = new List<int>();

        /// <summary>Forgets everything added so far.</summary>
        public void Clear()
        {
            vertices.Clear();
            colors.Clear();
            triangles.Clear();
        }

        /// <summary>One triangle of one colour.</summary>
        public void AddTriangle(Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            int first = vertices.Count;
            AddVertex(a, color);
            AddVertex(b, color);
            AddVertex(c, color);
            triangles.Add(first);
            triangles.Add(first + 1);
            triangles.Add(first + 2);
        }

        /// <summary>A quad a-b-c-d (corners in order round the edge) of one colour.</summary>
        public void AddQuad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color) =>
            AddQuad(a, b, c, d, color, color, color, color);

        /// <summary>A quad a-b-c-d with a colour per corner (the sky gradient and the trail's fade – nothing else).</summary>
        public void AddQuad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color colorA, Color colorB, Color colorC, Color colorD)
        {
            int first = vertices.Count;
            AddVertex(a, colorA);
            AddVertex(b, colorB);
            AddVertex(c, colorC);
            AddVertex(d, colorD);
            triangles.Add(first);
            triangles.Add(first + 1);
            triangles.Add(first + 2);
            triangles.Add(first);
            triangles.Add(first + 2);
            triangles.Add(first + 3);
        }

        /// <summary>An axis-aligned rectangle.</summary>
        public void AddRect(Vector2 min, Vector2 max, Color color) =>
            AddQuad(min, new Vector2(max.x, min.y), max, new Vector2(min.x, max.y), color);

        /// <summary>A convex polygon (corners in order round the edge), drawn as a fan.</summary>
        public void AddPolygon(IReadOnlyList<Vector2> points, Color color)
        {
            if (points.Count < 3) return;
            int first = vertices.Count;
            for (int i = 0; i < points.Count; i++) AddVertex(points[i], color);
            for (int i = 1; i < points.Count - 1; i++)
            {
                triangles.Add(first);
                triangles.Add(first + i);
                triangles.Add(first + i + 1);
            }
        }

        /// <summary>
        /// A straight stroke from a to b with square caps (it is longer than a-b by half its thickness at each end, so
        /// strokes that meet at a point join cleanly). a = b gives a square dot.
        /// </summary>
        public void AddLine(Vector2 a, Vector2 b, float thickness, Color color)
        {
            Vector2 along = b - a;
            along = along.sqrMagnitude > 1e-10f ? along.normalized : Vector2.right;
            Vector2 half = along * (thickness * 0.5f);
            Vector2 side = new Vector2(-half.y, half.x);
            AddQuad(a - half - side, b + half - side, b + half + side, a - half + side, color);
        }

        /// <summary>
        /// A piece of a ring round a centre, between two radii and two angles (degrees, counter-clockwise from +x).
        /// </summary>
        public void AddArc(Vector2 center, float innerRadius, float outerRadius, float startDegrees, float endDegrees, int segments, Color color)
        {
            Vector2 previous = Direction(startDegrees);
            for (int i = 1; i <= segments; i++)
            {
                Vector2 next = Direction(Mathf.Lerp(startDegrees, endDegrees, i / (float)segments));
                AddQuad(center + previous * innerRadius, center + previous * outerRadius,
                        center + next * outerRadius, center + next * innerRadius, color);
                previous = next;
            }
        }

        /// <summary>A new mesh holding everything added so far.</summary>
        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            ApplyTo(mesh);
            return mesh;
        }

        /// <summary>Replaces the content of an existing mesh with everything added so far.</summary>
        public void ApplyTo(Mesh mesh)
        {
            mesh.Clear();
            mesh.indexFormat = vertices.Count > MaxVerticesFor16BitIndices ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }

        /// <summary>The unit vector at an angle in degrees, counter-clockwise from +x.</summary>
        public static Vector2 Direction(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        void AddVertex(Vector2 position, Color color)
        {
            vertices.Add(new Vector3(position.x, position.y, 0f));
            // Vertex colours reach the shader untouched, so a linear-space project needs them converted here.
            colors.Add(QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color);
        }
    }
}
