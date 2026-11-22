using System.Collections.Generic;
using UnityEngine;

namespace SfxShowcase.Art
{
    /// <summary>
    /// Terrain: ground strips, floating platforms and loops. One rule for all three – "the ground is read by its top
    /// line": a Signal Cream line follows the walkable surface exactly, and under it the body is Ground Teal with level
    /// Ground Deep strata (<see cref="StrataFill"/>). Terrain has no Ink outline.
    /// </summary>
    public static class TerrainArt
    {
        const int LoopSegmentsPerHalf = 32;
        const float NarrowestColumn = 1e-4f;

        /// <summary>A ground strip: the surface polyline (world space, left to right) filled down to bottomY.</summary>
        public static GameObject CreateGround(ArtKit kit, IReadOnlyList<Vector2> surface, float bottomY)
        {
            var builder = new MeshBuilder();
            var fill = new StrataFill(builder);
            float highest = float.MinValue;
            for (int i = 0; i < surface.Count; i++)
            {
                highest = Mathf.Max(highest, surface[i].y);
            }

            for (int i = 0; i < surface.Count - 1; i++)
            {
                if (IsColumn(surface[i], surface[i + 1]))
                {
                    builder.AddPolygon(Column(surface[i], surface[i + 1], bottomY), Palette.GroundTeal);
                }
            }

            for (int band = StrataFill.FirstBandAbove(bottomY); StrataFill.BandBottom(band) < highest; band++)
            {
                AddGroundBand(builder, fill, surface, bottomY, band);
            }

            AddSurfaceLine(builder, surface);

            return kit.CreatePart("Ground", builder.ToMesh("Ground"), DepthLayer.Terrain);
        }

        /// <summary>
        /// A platform: the surface polyline (world space, left to right) with a body of the given thickness under it.
        /// </summary>
        public static GameObject CreatePlatform(ArtKit kit, IReadOnlyList<Vector2> surface, float thickness)
        {
            var builder = new MeshBuilder();
            var fill = new StrataFill(builder);
            Vector2 down = Vector2.down * thickness;
            for (int i = 0; i < surface.Count - 1; i++)
            {
                Vector2 left = surface[i];
                Vector2 right = surface[i + 1];
                if (IsColumn(left, right))
                {
                    fill.AddBody(new[] { left + down, right + down, right, left });
                }
            }

            AddSurfaceLine(builder, surface);
            return kit.CreatePart("Platform", builder.ToMesh("Platform"), DepthLayer.Terrain);
        }

        /// <summary>
        /// A loop: a ring whose inner face has the given radius, with a wall of the given thickness outside it. The
        /// right half (ridden first) is drawn behind the player and the left half in front, in the same colours.
        /// </summary>
        public static GameObject CreateLoop(ArtKit kit, Vector2 center, float radius, float thickness)
        {
            var loop = new GameObject("Loop");
            Mesh back = LoopHalf(center, radius, thickness, -90f, 90f, "Loop Back");
            Mesh front = LoopHalf(center, radius, thickness, 90f, 270f, "Loop Front");
            kit.CreatePart("Back Half", back, DepthLayer.LoopBack, 0, loop.transform);
            kit.CreatePart("Front Half", front, DepthLayer.LoopFront, 0, loop.transform);
            return loop;
        }

        static Mesh LoopHalf(
            Vector2 center,
            float radius,
            float thickness,
            float startDegrees,
            float endDegrees,
            string name
        )
        {
            var builder = new MeshBuilder();
            var fill = new StrataFill(builder);
            float outer = radius + thickness;
            Vector2 previous = MeshBuilder.Direction(startDegrees);
            for (int i = 1; i <= LoopSegmentsPerHalf; i++)
            {
                Vector2 next = MeshBuilder.Direction(
                    Mathf.Lerp(startDegrees, endDegrees, i / (float)LoopSegmentsPerHalf)
                );
                fill.AddBody(
                    new[]
                    {
                        center + previous * radius,
                        center + previous * outer,
                        center + next * outer,
                        center + next * radius,
                    }
                );
                previous = next;
            }

            builder.AddArc(
                center,
                radius,
                radius + ArtStyle.TopLineThickness,
                startDegrees,
                endDegrees,
                LoopSegmentsPerHalf,
                Palette.SignalCream
            );
            return builder.ToMesh(name);
        }

        /// <summary>
        /// One strata band across a whole ground strip. Where the band is entirely under the surface it is one long
        /// rectangle (so a kilometre of ground stays a small mesh); only where the surface cuts through the band is
        /// it clipped column by column.
        /// </summary>
        static void AddGroundBand(
            MeshBuilder builder,
            StrataFill fill,
            IReadOnlyList<Vector2> surface,
            float bottomY,
            int band
        )
        {
            float bandBottom = Mathf.Max(StrataFill.BandBottom(band), bottomY);
            float bandTop = StrataFill.BandBottom(band) + ArtStyle.StrataThickness;
            bool inRun = false;
            float runStart = 0f;
            float runEnd = 0f;
            for (int i = 0; i < surface.Count - 1; i++)
            {
                Vector2 left = surface[i];
                Vector2 right = surface[i + 1];
                if (!IsColumn(left, right))
                {
                    continue;
                }

                if (bandTop <= Mathf.Min(left.y, right.y))
                {
                    if (!inRun)
                    {
                        runStart = left.x;
                    }

                    inRun = true;
                    runEnd = right.x;
                    continue;
                }

                if (inRun)
                {
                    builder.AddRect(
                        new Vector2(runStart, bandBottom),
                        new Vector2(runEnd, bandTop),
                        Palette.GroundDeep
                    );
                }

                inRun = false;
                if (bandBottom < Mathf.Max(left.y, right.y))
                {
                    fill.AddBandInside(Column(left, right, bottomY), band);
                }
            }

            if (inRun)
            {
                builder.AddRect(new Vector2(runStart, bandBottom), new Vector2(runEnd, bandTop), Palette.GroundDeep);
            }
        }

        /// <summary>
        /// The cream line: a strip of constant thickness along the surface, on the body's side of it.
        /// </summary>
        static void AddSurfaceLine(MeshBuilder builder, IReadOnlyList<Vector2> surface)
        {
            int count = surface.Count;
            var inner = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                Vector2 normalBefore = i > 0 ? Shapes.RightNormal(surface[i - 1], surface[i]) : Vector2.zero;
                Vector2 normalAfter = i < count - 1 ? Shapes.RightNormal(surface[i], surface[i + 1]) : Vector2.zero;
                inner[i] = surface[i] + Shapes.Miter(normalBefore, normalAfter, ArtStyle.TopLineThickness);
            }

            for (int i = 0; i < count - 1; i++)
            {
                builder.AddQuad(surface[i], surface[i + 1], inner[i + 1], inner[i], Palette.SignalCream);
            }
        }

        /// <summary>Whether a surface segment has ground under it (a vertical step has none of its own).</summary>
        static bool IsColumn(Vector2 left, Vector2 right) => right.x - left.x > NarrowestColumn;

        static Vector2[] Column(Vector2 left, Vector2 right, float bottomY)
        {
            return new[] { new Vector2(left.x, bottomY), new Vector2(right.x, bottomY), right, left };
        }
    }
}
