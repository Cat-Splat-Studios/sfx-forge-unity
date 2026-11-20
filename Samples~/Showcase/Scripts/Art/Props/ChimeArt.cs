using UnityEngine;

namespace SfxShowcase.Art
{
    /// <summary>
    /// The Chime: a Chime Cyan diamond, 0.5 m tall – a square turned 45° with slightly rounded corners. Friendly, so
    /// round-cornered; touchable, so outlined. Every Chime in the level shares one mesh.
    /// </summary>
    public static class ChimeArt
    {
        const float HalfHeight = 0.25f;
        const float HalfWidth = 0.19f;
        const float Roundness = 0.3f;

        /// <summary>A Chime that spins by itself. Origin: its centre.</summary>
        public static GameObject Create(ArtKit kit)
        {
            var chime = new GameObject("Chime");
            GameObject diamond = kit.CreatePart("Diamond", kit.SharedMesh("Chime", DiamondMesh), DepthLayer.Props, 0, chime.transform);
            chime.AddComponent<ChimeSpin>().Configure(diamond.transform);
            return chime;
        }

        /// <summary>The diamond on its own, for the HUD's Chime icon.</summary>
        public static Mesh DiamondMesh()
        {
            return new InkedShape().Add(Shapes.Diamond(HalfWidth, HalfHeight, Roundness), Palette.ChimeCyan).ToMesh("Chime");
        }
    }
}
