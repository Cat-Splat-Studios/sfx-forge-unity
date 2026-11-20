using UnityEngine;

namespace SfxShowcase.Art
{
    /// <summary>
    /// "Signal at Dusk": every colour of the game, by the role it plays (DESIGN.md section 4). This is the only place a
    /// colour is written down; everything else asks for a role. The five saturated accents are SFX FORGE's own engine
    /// colours, and each has exactly one meaning: yellow is you, cyan is "collect", violet is "bounce", red is "hurts",
    /// green is "progress".
    /// </summary>
    public static class Palette
    {
        /// <summary>#161226 – outlines of everything you can touch, eyes, the HUD text shadow. The darkest value.</summary>
        public static readonly Color Ink = Hex(0x161226);
        /// <summary>#2A1E4A – the top of the sky; the HUD panel fill (see <see cref="HudPanel"/>).</summary>
        public static readonly Color NightPlum = Hex(0x2A1E4A);
        /// <summary>#7A2E6B – the middle of the sky.</summary>
        public static readonly Color DuskMagenta = Hex(0x7A2E6B);
        /// <summary>#F2A27C – the bottom of the sky.</summary>
        public static readonly Color HorizonPeach = Hex(0xF2A27C);
        /// <summary>#4A3478 – the farthest background wave (sine).</summary>
        public static readonly Color FarViolet = Hex(0x4A3478);
        /// <summary>#3A2A66 – the middle background wave (square).</summary>
        public static readonly Color MidViolet = Hex(0x3A2A66);
        /// <summary>#2E2256 – the nearest background wave (saw-tooth).</summary>
        public static readonly Color NearIndigo = Hex(0x2E2256);
        /// <summary>#1F5C63 – the body of the terrain.</summary>
        public static readonly Color GroundTeal = Hex(0x1F5C63);
        /// <summary>#17454F – the level strata bands inside the terrain body.</summary>
        public static readonly Color GroundDeep = Hex(0x17454F);
        /// <summary>#F6EBD0 – the terrain's top line, HUD text, Clipper eyes, dust. The brightest neutral.</summary>
        public static readonly Color SignalCream = Hex(0xF6EBD0);
        /// <summary>#FFD23F – Brio's body and trail, and nothing else in the world.</summary>
        public static readonly Color BrioYellow = Hex(0xFFD23F);
        /// <summary>#FF8A3C – Brio's crest, feet and tuck stripe.</summary>
        public static readonly Color EmberOrange = Hex(0xFF8A3C);
        /// <summary>#36C5FF – Chimes and their ring, and nothing else in the world.</summary>
        public static readonly Color ChimeCyan = Hex(0x36C5FF);
        /// <summary>#C48BFF – Woofers.</summary>
        public static readonly Color WooferViolet = Hex(0xC48BFF);
        /// <summary>#7BE07B – lit Tuning Forks, the Resonator, the safe end of the speed meter.</summary>
        public static readonly Color ForkGreen = Hex(0x7BE07B);
        /// <summary>#FF5A36 – Static and Clippers. Red means "this hurts", always and only.</summary>
        public static readonly Color StaticRed = Hex(0xFF5A36);
        /// <summary>#8C86A8 – unlit Tuning Forks, Clipper shards, empty meter bars.</summary>
        public static readonly Color DormantGrey = Hex(0x8C86A8);

        /// <summary>The sun disc: Horizon Peach tinted towards white.</summary>
        public static readonly Color Sun = Color.Lerp(HorizonPeach, Color.white, 0.35f);
        /// <summary>The fill of a HUD panel: Night Plum at 70 % alpha.</summary>
        public static readonly Color HudPanel = WithAlpha(NightPlum, 0.7f);
        /// <summary>An unfilled bar of the speed meter: Dormant Grey at 40 % alpha.</summary>
        public static readonly Color MeterEmpty = WithAlpha(DormantGrey, 0.4f);

        /// <summary>The same colour with another alpha (fades, the trail). Use only with the transparent material.</summary>
        public static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
