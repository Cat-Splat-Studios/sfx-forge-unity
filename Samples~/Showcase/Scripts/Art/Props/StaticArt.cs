using UnityEngine;

namespace SfxShowcase.Art
{
    /// <summary>
    /// Static: a jagged burr of noise – irregular spikes like a noise waveform, 2 m wide and 1 m tall, Static Red.
    /// Dangerous, so angular. The seed picks the spikes, so two burrs with the same seed share a mesh and a level
    /// rebuilt from the same data looks the same.
    /// </summary>
    public static class StaticArt
    {
        const float HalfWidth = 1f;
        const float Height = 1f;
        const float LowestSpike = 0.45f;
        const float BaseStripHeight = 0.12f;
        const int FewestSpikes = 8;
        const int MostSpikes = 12;

        /// <summary>A Static burr. Origin: the centre of its base.</summary>
        public static GameObject Create(ArtKit kit, int seed)
        {
            Mesh mesh = kit.SharedMesh("Static " + seed, () => BurrMesh(seed));
            return kit.CreatePart("Static", mesh, DepthLayer.Props);
        }

        static Mesh BurrMesh(int seed)
        {
            // System.Random, not UnityEngine.Random: the art must not disturb the game's random sequence.
            var random = new System.Random(seed);
            int spikes = random.Next(FewestSpikes, MostSpikes + 1);
            int tallest = random.Next(spikes);
            float slot = 2f * HalfWidth / spikes;

            var shape = new InkedShape();
            shape.Add(Shapes.Rect(new Vector2(-HalfWidth, 0f), new Vector2(HalfWidth, BaseStripHeight)), Palette.StaticRed);
            for (int i = 0; i < spikes; i++)
            {
                float left = -HalfWidth + i * slot;
                float tipX = left + slot * Mathf.Lerp(0.2f, 0.8f, (float)random.NextDouble());
                float tipY = i == tallest ? Height : Mathf.Lerp(LowestSpike, Height, (float)random.NextDouble());
                shape.Add(new[] { new Vector2(left, 0f), new Vector2(left + slot, 0f), new Vector2(tipX, tipY) }, Palette.StaticRed);
            }
            return shape.ToMesh("Static");
        }
    }
}
