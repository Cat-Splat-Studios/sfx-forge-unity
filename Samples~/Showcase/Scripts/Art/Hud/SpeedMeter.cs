using UnityEngine;

namespace SfxShowcase.Art
{
    /// <summary>
    /// The speed meter: a row of twelve upright bars like a level meter, filling left to right with speed – all twelve
    /// lit is the speed cap. Bars 1–7 are Fork Green, 8–10 Brio Yellow, 11–12 Static Red; unlit bars are faint grey.
    /// Redrawn only when the number of lit bars changes. Its origin is the bottom-left corner of the first bar.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    public sealed class SpeedMeter : MonoBehaviour
    {
        /// <summary>How many bars the meter has.</summary>
        public const int BarCount = 12;
        const int LastGreenBar = 7;
        const int LastYellowBar = 10;

        [SerializeField, Tooltip("Width of one bar, HUD units.")]
        float barWidth = 0.26f;
        [SerializeField, Tooltip("Gap between bars, HUD units.")]
        float barGap = 0.12f;
        [SerializeField, Tooltip("Height of the bars, HUD units.")]
        float barHeight = 1f;

        readonly MeshBuilder builder = new MeshBuilder();
        Mesh mesh;
        int shownBars = -1;

        /// <summary>The width of the whole row, HUD units.</summary>
        public float Width => BarCount * barWidth + (BarCount - 1) * barGap;

        /// <summary>Shows a speed, 0..1 of the speed cap.</summary>
        public void Show(float speed01)
        {
            int litBars = Mathf.RoundToInt(Mathf.Clamp01(speed01) * BarCount);
            if (litBars == shownBars) return;
            shownBars = litBars;

            if (mesh == null)
            {
                var filter = GetComponent<MeshFilter>();
                mesh = filter.sharedMesh != null ? filter.sharedMesh : new Mesh { name = "Speed Meter" };
                filter.sharedMesh = mesh;
            }

            builder.Clear();
            for (int bar = 1; bar <= BarCount; bar++)
            {
                float left = (bar - 1) * (barWidth + barGap);
                Color color = bar > litBars ? Palette.MeterEmpty : LitColor(bar);
                builder.AddRect(new Vector2(left, 0f), new Vector2(left + barWidth, barHeight), color);
            }
            builder.ApplyTo(mesh);
        }

        static Color LitColor(int bar)
        {
            if (bar <= LastGreenBar) return Palette.ForkGreen;
            return bar <= LastYellowBar ? Palette.BrioYellow : Palette.StaticRed;
        }
    }
}
