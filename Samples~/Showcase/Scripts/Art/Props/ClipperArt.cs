using UnityEngine;

namespace SfxShowcase.Art
{
    /// <summary>
    /// The Clipper: a square wave on legs – a flat-topped Static Red block, 1.2 m wide, on two stubby legs, with two
    /// square Signal Cream eyes towards the side it faces. Dangerous, so every corner is a right angle.
    /// </summary>
    public static class ClipperArt
    {
        const float HalfWidth = 0.6f;
        const float LegHeight = 0.36f;
        const float BodyBottom = 0.3f;
        const float BodyTop = 1.2f;
        const float LegHalfWidth = 0.14f;
        const float LegOffset = 0.3f;

        /// <summary>A Clipper facing local +x, walking on the spot. Origin: between its feet on the ground.</summary>
        public static GameObject Create(ArtKit kit)
        {
            var clipper = new GameObject("Clipper");
            Mesh legMesh = kit.SharedMesh("Clipper Leg", LegMesh);
            Transform backLeg = kit.CreatePart("Back Leg", legMesh, DepthLayer.Props, -1, clipper.transform).transform;
            Transform frontLeg = kit.CreatePart(
                "Front Leg",
                legMesh,
                DepthLayer.Props,
                -1,
                clipper.transform
            ).transform;
            backLeg.localPosition = new Vector3(-LegOffset, 0f, 0f);
            frontLeg.localPosition = new Vector3(LegOffset, 0f, 0f);
            Transform body = kit.CreatePart(
                "Body",
                kit.SharedMesh("Clipper Body", BodyMesh),
                DepthLayer.Props,
                0,
                clipper.transform
            ).transform;
            clipper.AddComponent<ClipperWalk>().Configure(body, backLeg, frontLeg);
            return clipper;
        }

        static Mesh BodyMesh()
        {
            return new InkedShape()
                .Add(
                    Shapes.Rect(new Vector2(-HalfWidth, BodyBottom), new Vector2(HalfWidth, BodyTop)),
                    Palette.StaticRed
                )
                .AddDetail(Shapes.Rect(new Vector2(0.05f, 0.78f), new Vector2(0.25f, 0.98f)), Palette.SignalCream)
                .AddDetail(Shapes.Rect(new Vector2(0.33f, 0.78f), new Vector2(0.53f, 0.98f)), Palette.SignalCream)
                .AddDetail(Shapes.Rect(new Vector2(0.05f, 0.5f), new Vector2(0.53f, 0.57f)), Palette.Ink)
                .ToMesh("Clipper Body");
        }

        static Mesh LegMesh()
        {
            return new InkedShape()
                .Add(
                    Shapes.Rect(new Vector2(-LegHalfWidth, 0f), new Vector2(LegHalfWidth, LegHeight)),
                    Palette.StaticRed
                )
                .ToMesh("Clipper Leg");
        }
    }
}
