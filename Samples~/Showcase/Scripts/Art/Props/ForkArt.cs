using SfxShowcase.Core;
using UnityEngine;

namespace SfxShowcase.Art
{
    /// <summary>
    /// "Structures are forks": the Tuning Fork checkpoint (a stem with a U of two prongs) and the Resonator goal (the
    /// same shape, four times the size, standing on its prongs so it makes an arch to run through). Dormant Grey until
    /// lit, Fork Green once lit – green means "progress".
    /// </summary>
    public static class ForkArt
    {
        const int BendSegments = 12;

        /// <summary>A Tuning Fork, 3 m tall, unlit. Origin: the foot of its stem.</summary>
        public static PropVisual CreateTuningFork(ArtKit kit)
        {
            var fork = new GameObject("Tuning Fork");
            Mesh unlitMesh = kit.SharedMesh("Tuning Fork Unlit", () => ForkMesh(3f, 0.36f, 0.18f, 1.5f, false, Palette.DormantGrey, "Tuning Fork Unlit"));
            Mesh litMesh = kit.SharedMesh("Tuning Fork Lit", () => ForkMesh(3f, 0.36f, 0.18f, 1.5f, false, Palette.ForkGreen, "Tuning Fork Lit"));
            GameObject unlit = kit.CreatePart("Unlit", unlitMesh, DepthLayer.Props, 0, fork.transform);
            GameObject lit = kit.CreatePart("Lit", litMesh, DepthLayer.Props, 0, fork.transform);
            var visual = fork.AddComponent<ForkVisual>();
            visual.Configure(unlit, lit);
            return visual;
        }

        /// <summary>The Resonator arch, 12 m tall, always Fork Green. Origin: the ground between its prongs.</summary>
        public static PropVisual CreateResonator(ArtKit kit)
        {
            var resonator = new GameObject("Resonator");
            Mesh mesh = kit.SharedMesh("Resonator", () => ForkMesh(12f, 2.5f, 0.7f, 2f, true, Palette.ForkGreen, "Resonator"));
            GameObject arch = kit.CreatePart("Arch", mesh, DepthLayer.Props, 0, resonator.transform);
            var visual = resonator.AddComponent<ForkVisual>();
            visual.Configure(null, arch);
            return visual;
        }

        /// <summary>
        /// A fork of the given total height: a stem from the ground, a half-round bend, and two prongs up to the top.
        /// prongSpacing is the distance from the middle to the centre line of a prong. upsideDown stands it on its
        /// prongs instead (the Resonator).
        /// </summary>
        static Mesh ForkMesh(float height, float prongSpacing, float barThickness, float stemLength, bool upsideDown, Color color, string name)
        {
            float halfBar = barThickness * 0.5f;
            var bendCenter = new Vector2(0f, stemLength + prongSpacing);
            var shape = new InkedShape();

            AddBar(shape, new Vector2(-halfBar, 0f), new Vector2(halfBar, stemLength), height, upsideDown, color);
            AddBar(shape, new Vector2(-prongSpacing - halfBar, bendCenter.y), new Vector2(-prongSpacing + halfBar, height), height, upsideDown, color);
            AddBar(shape, new Vector2(prongSpacing - halfBar, bendCenter.y), new Vector2(prongSpacing + halfBar, height), height, upsideDown, color);

            Vector2 previous = MeshBuilder.Direction(180f);
            for (int i = 1; i <= BendSegments; i++)
            {
                Vector2 next = MeshBuilder.Direction(180f + 180f * i / BendSegments);
                shape.Add(new[]
                {
                    Place(bendCenter + previous * (prongSpacing - halfBar), height, upsideDown),
                    Place(bendCenter + previous * (prongSpacing + halfBar), height, upsideDown),
                    Place(bendCenter + next * (prongSpacing + halfBar), height, upsideDown),
                    Place(bendCenter + next * (prongSpacing - halfBar), height, upsideDown),
                }, color);
                previous = next;
            }
            return shape.ToMesh(name);
        }

        static void AddBar(InkedShape shape, Vector2 min, Vector2 max, float height, bool upsideDown, Color color)
        {
            shape.Add(new[]
            {
                Place(min, height, upsideDown),
                Place(new Vector2(max.x, min.y), height, upsideDown),
                Place(max, height, upsideDown),
                Place(new Vector2(min.x, max.y), height, upsideDown),
            }, color);
        }

        /// <summary>Where a point of the upright fork ends up: unchanged, or mirrored top to bottom.</summary>
        static Vector2 Place(Vector2 point, float height, bool upsideDown) =>
            upsideDown ? new Vector2(point.x, height - point.y) : point;
    }
}
