using System;
using UnityEngine;

namespace SfxShowcase.Art
{
    /// <summary>
    /// The backdrop of Oscilla (DESIGN.md, "Background and parallax"): the dusk sky – the only gradient in the game –
    /// with a low sun, and three flat silhouettes that are the three other waveforms: a far sine, a middle square wave
    /// and a near saw-tooth. No detail, no outline, all darker than anything in play.
    /// </summary>
    public static class BackgroundArt
    {
        const float SilhouetteDepth = 60f;
        const float MarginBeyondLevel = 45f;

        /// <summary>The sky and the three parallax layers, following the camera by themselves.</summary>
        public static GameObject Create(ArtKit kit, Camera camera, Rect levelBounds)
        {
            var background = new GameObject("Background");
            Transform root = background.transform;
            Transform sky = kit.CreatePart("Sky", kit.SharedMesh("Sky", SkyMesh), DepthLayer.Sky, 0, root).transform;
            Transform sun = kit.CreatePart("Sun", kit.SharedMesh("Sun", SunMesh), DepthLayer.Sky, 1, root).transform;

            var parallax = background.AddComponent<ParallaxBackground>();
            parallax.Configure(camera, sky, sun, levelBounds.center.y);
            AddLayer(
                kit,
                parallax,
                "Far Wave",
                DepthLayer.FarWave,
                Palette.FarViolet,
                0.1f,
                -1.5f,
                levelBounds,
                2f,
                SineWave
            );
            AddLayer(
                kit,
                parallax,
                "Mid Wave",
                DepthLayer.MidWave,
                Palette.MidViolet,
                0.25f,
                -4.5f,
                levelBounds,
                15f,
                SquareWave
            );
            AddLayer(
                kit,
                parallax,
                "Near Wave",
                DepthLayer.NearWave,
                Palette.NearIndigo,
                0.45f,
                -7.5f,
                levelBounds,
                16f,
                SawWave
            );
            parallax.Arrange();
            return background;
        }

        /// <summary>
        /// One silhouette. A layer with parallax factor f slides past the camera at f times the camera's speed, so over
        /// the whole level the camera sees only f times the level's width of it – that is how wide the mesh is made.
        /// </summary>
        static void AddLayer(
            ArtKit kit,
            ParallaxBackground parallax,
            string name,
            DepthLayer depth,
            Color color,
            float parallaxFactor,
            float baseline,
            Rect levelBounds,
            float step,
            Func<float, float> heightAt
        )
        {
            float start = levelBounds.xMin * parallaxFactor - MarginBeyondLevel;
            float end = levelBounds.xMax * parallaxFactor + MarginBeyondLevel;
            var builder = new MeshBuilder();
            for (float x = Mathf.Floor(start / step) * step; x < end; x += step)
            {
                // Each step is sampled just inside its two ends, so square and saw waves keep their vertical edges.
                float leftHeight = heightAt(x + step * 0.001f);
                float rightHeight = heightAt(x + step * 0.999f);
                builder.AddQuad(
                    new Vector2(x, -SilhouetteDepth),
                    new Vector2(x + step, -SilhouetteDepth),
                    new Vector2(x + step, rightHeight),
                    new Vector2(x, leftHeight),
                    color
                );
            }

            Transform layer = kit.CreatePart(name, builder.ToMesh(name), depth, 0, parallax.transform).transform;
            parallax.AddLayer(layer, parallaxFactor, baseline);
        }

        /// <summary>The far layer: a slow, wide sine.</summary>
        static float SineWave(float x) => 2.5f * Mathf.Sin(x * 2f * Mathf.PI / 70f);

        /// <summary>The middle layer: square-wave mesas, 15 m wide, stepping through a fixed set of heights.</summary>
        static float SquareWave(float x)
        {
            float[] mesaHeights = { 3f, 0f, 4.5f, 1.5f, 3.5f, 0.5f };
            int mesa = Mathf.FloorToInt(x / 15f);
            return mesaHeights[(mesa % mesaHeights.Length + mesaHeights.Length) % mesaHeights.Length];
        }

        /// <summary>The near layer: a saw-tooth ridge that climbs for 16 m and drops.</summary>
        static float SawWave(float x) => 3f * Mathf.Repeat(x / 16f, 1f);

        /// <summary>
        /// A 1 × 1 quad centred on the origin: Horizon Peach at the bottom, Dusk Magenta in the middle,
        /// Night Plum on top.
        /// </summary>
        static Mesh SkyMesh()
        {
            var builder = new MeshBuilder();
            Vector2 bottomLeft = new Vector2(-0.5f, -0.5f);
            Vector2 bottomRight = new Vector2(0.5f, -0.5f);
            Vector2 middleLeft = new Vector2(-0.5f, 0f);
            Vector2 middleRight = new Vector2(0.5f, 0f);
            Vector2 topLeft = new Vector2(-0.5f, 0.5f);
            Vector2 topRight = new Vector2(0.5f, 0.5f);
            builder.AddQuad(
                bottomLeft,
                bottomRight,
                middleRight,
                middleLeft,
                Palette.HorizonPeach,
                Palette.HorizonPeach,
                Palette.DuskMagenta,
                Palette.DuskMagenta
            );
            builder.AddQuad(
                middleLeft,
                middleRight,
                topRight,
                topLeft,
                Palette.DuskMagenta,
                Palette.DuskMagenta,
                Palette.NightPlum,
                Palette.NightPlum
            );
            return builder.ToMesh("Sky");
        }

        static Mesh SunMesh()
        {
            var builder = new MeshBuilder();
            builder.AddPolygon(Shapes.Ellipse(Vector2.zero, 1f, 1f, 48), Palette.Sun);
            return builder.ToMesh("Sun");
        }
    }
}
