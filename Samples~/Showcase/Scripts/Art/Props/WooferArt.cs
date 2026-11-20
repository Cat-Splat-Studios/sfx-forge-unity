using SfxShowcase.Core;
using UnityEngine;

namespace SfxShowcase.Art
{
    /// <summary>
    /// The Woofer: a speaker cone set in the ground – an ellipse cone on a rounded base, all Woofer Violet. Violet
    /// means "bounce" and nothing else.
    /// </summary>
    public static class WooferArt
    {
        const float BaseHalfWidth = 0.8f;
        const float BaseHeight = 0.35f;
        const float ConeHeight = 0.42f;

        /// <summary>A Woofer firing along its local +y. Origin: the centre of its base on the ground.</summary>
        public static PropVisual Create(ArtKit kit)
        {
            var woofer = new GameObject("Woofer");
            kit.CreatePart("Base", kit.SharedMesh("Woofer Base", BaseMesh), DepthLayer.Props, 0, woofer.transform);
            GameObject cone = kit.CreatePart("Cone", kit.SharedMesh("Woofer Cone", ConeMesh), DepthLayer.Props, 1, woofer.transform);
            cone.transform.localPosition = new Vector3(0f, ConeHeight, 0f);
            var visual = woofer.AddComponent<WooferVisual>();
            visual.Configure(cone.transform);
            return visual;
        }

        static Mesh BaseMesh()
        {
            var body = Shapes.RoundedRect(new Vector2(-BaseHalfWidth, 0f), new Vector2(BaseHalfWidth, BaseHeight), 0.15f);
            return new InkedShape().Add(body, Palette.WooferViolet).ToMesh("Woofer Base");
        }

        /// <summary>The cone seen from slightly above: rim, dark throat, centre cap. Its origin is its centre.</summary>
        static Mesh ConeMesh()
        {
            return new InkedShape()
                .Add(Shapes.Ellipse(Vector2.zero, 0.7f, 0.22f), Palette.WooferViolet)
                .AddDetail(Shapes.Ellipse(Vector2.zero, 0.5f, 0.13f), Palette.Ink)
                .AddDetail(Shapes.Ellipse(Vector2.zero, 0.2f, 0.07f), Palette.WooferViolet)
                .ToMesh("Woofer Cone");
        }
    }
}
