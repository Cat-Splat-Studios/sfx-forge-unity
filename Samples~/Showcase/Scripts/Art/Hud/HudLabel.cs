using UnityEngine;

namespace SfxShowcase.Art
{
    /// <summary>
    /// One line of HUD text: Signal Cream with an Ink drop shadow, in the generated <see cref="StrokeFont"/>. The mesh
    /// is redrawn only when the text or its opacity changes. Sits on an object made by <see cref="ArtKit.CreatePart"/>
    /// with the transparent material (so it can fade).
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    public sealed class HudLabel : MonoBehaviour
    {
        [SerializeField, Tooltip("Cap height of the letters, HUD units.")]
        float height = 0.7f;
        [SerializeField, Tooltip("Which point of the line sits on this object's origin: 0 = its left end, 0.5 = its middle, 1 = its right end.")]
        float anchor;
        [SerializeField, Tooltip("How far down and right the Ink shadow is drawn, HUD units.")]
        float shadowOffset = 0.05f;

        readonly MeshBuilder builder = new MeshBuilder();
        Mesh mesh;
        string shownText;
        float shownOpacity = -1f;

        /// <summary>Sets the size and alignment. Called by <see cref="HudArt"/>.</summary>
        public void Configure(float capHeight, float horizontalAnchor)
        {
            height = capHeight;
            anchor = horizontalAnchor;
        }

        /// <summary>Shows a line of text (upper case, digits, : . / -) at an opacity of 0..1.</summary>
        public void Show(string text, float opacity = 1f)
        {
            if (text == shownText && Mathf.Approximately(opacity, shownOpacity)) return;
            shownText = text;
            shownOpacity = opacity;

            if (mesh == null)
            {
                var filter = GetComponent<MeshFilter>();
                // In play mode this reuses the mesh the scene builder left here; otherwise a fresh one.
                mesh = filter.sharedMesh != null ? filter.sharedMesh : new Mesh { name = "HUD Label" };
                filter.sharedMesh = mesh;
            }

            builder.Clear();
            var origin = new Vector2(-StrokeFont.Width(text, height) * anchor, 0f);
            var shadow = new Vector2(shadowOffset, -shadowOffset);
            StrokeFont.Draw(builder, text, origin + shadow, height, Palette.WithAlpha(Palette.Ink, opacity));
            StrokeFont.Draw(builder, text, origin, height, Palette.WithAlpha(Palette.SignalCream, opacity));
            builder.ApplyTo(mesh);
        }
    }
}
