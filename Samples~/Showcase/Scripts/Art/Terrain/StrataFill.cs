using System.Collections.Generic;
using UnityEngine;

namespace SfxShowcase.Art
{
    /// <summary>
    /// Fills terrain bodies: Ground Teal with level Ground Deep strata bands. The bands are fixed in WORLD space – band
    /// k lies between y = k × <see cref="ArtStyle.StrataSpacing"/> and that plus <see cref="ArtStyle.StrataThickness"/>
    /// – so they stay level under a slope and line up from one piece of terrain to the next.
    /// </summary>
    public sealed class StrataFill
    {
        readonly MeshBuilder builder;
        readonly List<Vector2> clipInput = new List<Vector2>();
        readonly List<Vector2> clipOutput = new List<Vector2>();

        /// <summary>Creates a filler that writes into a mesh builder.</summary>
        public StrataFill(MeshBuilder builder)
        {
            this.builder = builder;
        }

        /// <summary>The index of the lowest band that reaches above a height.</summary>
        public static int FirstBandAbove(float y) =>
            Mathf.FloorToInt((y - ArtStyle.StrataThickness) / ArtStyle.StrataSpacing) + 1;

        /// <summary>The bottom of band k, in world y.</summary>
        public static float BandBottom(int band) => band * ArtStyle.StrataSpacing;

        /// <summary>A convex piece of terrain body (world space): teal, then every band that crosses it.</summary>
        public void AddBody(IReadOnlyList<Vector2> convexPolygon)
        {
            builder.AddPolygon(convexPolygon, Palette.GroundTeal);
            float lowest = float.MaxValue;
            float highest = float.MinValue;
            for (int i = 0; i < convexPolygon.Count; i++)
            {
                lowest = Mathf.Min(lowest, convexPolygon[i].y);
                highest = Mathf.Max(highest, convexPolygon[i].y);
            }

            for (int band = FirstBandAbove(lowest); BandBottom(band) < highest; band++)
            {
                AddBandInside(convexPolygon, band);
            }
        }

        /// <summary>The part of band k that lies inside a convex polygon, in Ground Deep.</summary>
        public void AddBandInside(IReadOnlyList<Vector2> convexPolygon, int band)
        {
            float bottom = BandBottom(band);
            clipInput.Clear();
            for (int i = 0; i < convexPolygon.Count; i++)
            {
                clipInput.Add(convexPolygon[i]);
            }

            ClipAtHeight(clipInput, clipOutput, bottom, keepAbove: true);
            ClipAtHeight(clipOutput, clipInput, bottom + ArtStyle.StrataThickness, keepAbove: false);
            builder.AddPolygon(clipInput, Palette.GroundDeep);
        }

        /// <summary>Keeps the part of a convex polygon on one side of a horizontal line (Sutherland–Hodgman).</summary>
        static void ClipAtHeight(List<Vector2> input, List<Vector2> output, float y, bool keepAbove)
        {
            output.Clear();
            for (int i = 0; i < input.Count; i++)
            {
                Vector2 current = input[i];
                Vector2 next = input[(i + 1) % input.Count];
                bool currentInside = keepAbove ? current.y >= y : current.y <= y;
                bool nextInside = keepAbove ? next.y >= y : next.y <= y;
                if (currentInside)
                {
                    output.Add(current);
                }

                if (currentInside != nextInside)
                {
                    output.Add(Vector2.Lerp(current, next, (y - current.y) / (next.y - current.y)));
                }
            }
        }
    }
}
