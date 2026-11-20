using System.Collections.Generic;
using UnityEngine;

namespace SfxShowcase.Art
{
    /// <summary>
    /// The game's lettering: a generated stroke font, because the project has no text component and nothing in the
    /// showcase may be an imported asset. Upper case A–Z, 0–9 and : . / - only (the HUD is upper case by design).
    /// Each glyph is a few polylines on a 4 × 6 grid, drawn with square-ended strokes – oscilloscope lettering.
    /// </summary>
    public static class StrokeFont
    {
        const float CellHeight = 6f;
        const float CellWidth = 4f;
        /// <summary>Grid units from the start of one glyph to the start of the next.</summary>
        const float Advance = 5.5f;
        /// <summary>Stroke thickness in grid units.</summary>
        const float StrokeThickness = 0.85f;

        /// <summary>
        /// The glyphs. A glyph is strokes separated by spaces; a stroke is a run of points, each written as two
        /// digits: column (0–4, left to right) then row (0–6, bottom to top). "0006" is a line up the left edge.
        /// A stroke of one point repeated ("2020") is a dot.
        /// </summary>
        static readonly Dictionary<char, string> Glyphs = new Dictionary<char, string>
        {
            { 'A', "0004264440 0242" },
            { 'B', "00063645443303 3342413000" },
            { 'C', "4536160501103041" },
            { 'D', "00062644422000" },
            { 'E', "46060040 0333" },
            { 'F', "460600 0333" },
            { 'G', "45361605011030414323" },
            { 'H', "0006 4046 0343" },
            { 'I', "1636 2620 1030" },
            { 'J', "4641301001" },
            { 'K', "0006 460340" },
            { 'L', "060040" },
            { 'M', "0006234640" },
            { 'N', "00064046" },
            { 'O', "163645413010010516" },
            { 'P', "00063645443303" },
            { 'Q', "163645413010010516 2240" },
            { 'R', "00063645443303 2340" },
            { 'S', "453616050413334241301001" },
            { 'T', "0646 2620" },
            { 'U', "060110304146" },
            { 'V', "062046" },
            { 'W', "0610233046" },
            { 'X', "0046 0640" },
            { 'Y', "062346 2320" },
            { 'Z', "06460040" },
            { '0', "163645413010010516 1234" },
            { '1', "152620 1030" },
            { '2', "05163645440040" },
            { '3', "0516364544334241301001 2333" },
            { '4', "30360242" },
            { '5', "460603334241301001" },
            { '6', "36160501103041423303" },
            { '7', "064620" },
            { '8', "13040516364544331302011030414233" },
            { '9', "10304145361605041343" },
            { ':', "2222 2424" },
            { '.', "2020" },
            { '/', "0046" },
            { '-', "0343" },
        };

        /// <summary>The width of a line of text drawn at a cap height, in the same units as the height.</summary>
        public static float Width(string text, float height)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            return ((text.Length - 1) * Advance + CellWidth) * (height / CellHeight);
        }

        /// <summary>
        /// Draws a line of text. origin is the bottom-left corner of the first glyph; height is the cap height.
        /// Characters the font does not have (and spaces) leave a gap.
        /// </summary>
        public static void Draw(MeshBuilder builder, string text, Vector2 origin, float height, Color color)
        {
            float unit = height / CellHeight;
            for (int i = 0; i < text.Length; i++)
            {
                if (!Glyphs.TryGetValue(text[i], out string glyph)) continue;
                Vector2 glyphOrigin = origin + new Vector2(i * Advance * unit, 0f);
                DrawGlyph(builder, glyph, glyphOrigin, unit, color);
            }
        }

        static void DrawGlyph(MeshBuilder builder, string glyph, Vector2 origin, float unit, Color color)
        {
            bool strokeStarted = false;
            Vector2 previous = Vector2.zero;
            for (int i = 0; i < glyph.Length; i++)
            {
                if (glyph[i] == ' ')
                {
                    strokeStarted = false;
                    continue;
                }
                Vector2 point = origin + new Vector2(glyph[i] - '0', glyph[i + 1] - '0') * unit;
                i++;
                if (strokeStarted) builder.AddLine(previous, point, StrokeThickness * unit, color);
                previous = point;
                strokeStarted = true;
            }
        }
    }
}
