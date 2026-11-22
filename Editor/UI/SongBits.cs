// SPDX-License-Identifier: MIT
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatSplat.SfxForge.Editor
{
    /// <summary>
    /// A row of steps, as the stage draws a sound's row of the pattern: a cell a step, a set step filled as high as it is
    /// hard. Fill Level (0 = not set, up to 1 = full height), set Count (the cells: 16, 32 or 64 - the pattern's grid) and
    /// Length (steps beyond it are dark) and call Changed.
    /// </summary>
    public class StepRow : VisualElement
    {
        public const int Steps = 16;
        /// <summary>The most cells a row has: a pattern of 64 steps.</summary>
        public const int MaxSteps = 64;
        /// <summary>The cells drawn: 16, 32 or 64. Level, Hold, Length and Head count in these.</summary>
        public int Count = Steps;
        /// <summary>A step's height, 0..1; 0: the step is not set.</summary>
        public readonly float[] Level = new float[MaxSteps];
        /// <summary>The steps the pattern has (the others: dark).</summary>
        public int Length = Steps;
        /// <summary>The step that plays now, -1: none. (At 32 and 64 every cell of its sixteenth is lit.)</summary>
        public int Head = -1;
        public Color Accent = SfxColors.Ember;
        /// <summary>The sound's layer is silent: its steps are drawn dim.</summary>
        public bool Dimmed;
        /// <summary>How many steps a set step holds (the line after it); 0 or 1: no line.</summary>
        public readonly byte[] Hold = new byte[MaxSteps];
        /// <summary>The row of the sound that is chosen: its empty steps are a little lighter.</summary>
        public bool Selected;

        public StepRow()
        {
            AddToClassList("sfx-steps");
            generateVisualContent += Draw;
        }

        public void Changed() => MarkDirtyRepaint();

        /// <summary>
        /// Where the cells of a row of 32 or 64 stand in a width: whole pixels, a pixel more between two sixteenths.
        /// cell: the pitch; wide: what is drawn of it. False: too narrow to draw.
        /// </summary>
        public static bool Fine(float width, int count, out int cell, out int wide)
        {
            cell = Mathf.Max(2, Mathf.FloorToInt((width - 15f) / count)); wide = cell >= 5 ? cell - 1 : cell;
            return width >= count * 2 + 15;
        }

        void Draw(MeshGenerationContext c)
        {
            if (Count > Steps) { DrawFine(c); return; }
            Rect r = contentRect;
            if (r.width < Steps * 2 || r.height < 4f) return;
            var p = c.painter2D;
            float pitch = r.width / Steps, w = Mathf.Max(1f, pitch - 2f), h = r.height;
            for (int i = 0; i < Steps; i++)
            {
                float x = r.x + i * pitch;
                bool off = i >= Length, head = i == Head;
                if (Level[i] <= 0f)
                {
                    Color empty = head ? SfxColors.Hex(0x5F4134) : off ? SfxColors.Hex(0x1C130F) : (i / 4 & 1) != 0 ? SfxColors.Hex(0x2B1D17) : SfxColors.Hex(0x38261E);
                    SfxUI.RoundRect(p, x, r.y, w, h, 3f, Selected && !head && !off ? SfxColors.Mix(empty, SfxColors.White, 0.08f) : empty);
                    continue;
                }
                Color cell = head && !Dimmed ? SfxColors.White : Dimmed ? SfxColors.Mix(SfxColors.Bg, Accent, 0.35f) : Accent;
                float bar = Mathf.Max(3f, h * Mathf.Clamp01(Level[i]));
                SfxUI.RoundRect(p, x, r.y, w, h, 3f, SfxColors.Mix(SfxColors.Bg, cell, 0.30f));
                SfxUI.RoundRect(p, x, r.y + h - bar, w, bar, 3f, cell);
            }
            for (int i = 0; i < Steps; i++)                  // a step with a length: a line as far as it holds
            {
                if (Level[i] <= 0f || Hold[i] <= 1) continue;
                int n = Mathf.Min(Hold[i], Steps - i);
                SfxUI.Rect(p, r.x + i * pitch + w, r.y + h - 4f, n * pitch - 2f - w, 3f, Dimmed ? SfxColors.Hex(0x7A6055) : SfxColors.White);
            }
        }

        // 32 or 64 cells: plain rectangles on whole pixels (a rounded cell 4 pixels wide is a smear)
        void DrawFine(MeshGenerationContext c)
        {
            Rect r = contentRect;
            int count = Mathf.Min(Count, MaxSteps), div = count / Steps;
            if (!Fine(r.width, count, out int cell, out int wide) || r.height < 4f) return;
            var p = c.painter2D;
            float x0 = Mathf.Round(r.x), h = r.height;
            for (int i = 0; i < count; i++)
            {
                float x = x0 + i * cell + i / div;
                bool off = i >= Length, head = Head >= 0 && i / div == Head / div;
                if (Level[i] <= 0f)
                {
                    Color empty = head ? SfxColors.Hex(0x5F4134) : off ? SfxColors.Hex(0x1C130F) : (i / (4 * div) & 1) != 0 ? SfxColors.Hex(0x2B1D17) : SfxColors.Hex(0x38261E);
                    SfxUI.Rect(p, x, r.y, wide, h, Selected && !head && !off ? SfxColors.Mix(empty, SfxColors.White, 0.08f) : empty);
                    continue;
                }
                Color set = head && !Dimmed ? SfxColors.White : Dimmed ? SfxColors.Mix(SfxColors.Bg, Accent, 0.35f) : Accent;
                float bar = Mathf.Max(3f, Mathf.Round(h * Mathf.Clamp01(Level[i])));
                SfxUI.Rect(p, x, r.y, wide, h, SfxColors.Mix(SfxColors.Bg, set, 0.30f));
                SfxUI.Rect(p, x, r.y + h - bar, wide, bar, set);
            }
            for (int i = 0; i < count; i++)                  // a step with a length: a line from its cell's right edge to the right edge of the last cell it holds
            {
                if (Level[i] <= 0f || Hold[i] <= 1) continue;
                int last = Mathf.Min(i + Hold[i], count) - 1;
                float from = x0 + i * cell + i / div + wide, to = x0 + last * cell + last / div + wide;
                if (to > from) SfxUI.Rect(p, from, r.y + h - 4f, to - from, 3f, Dimmed ? SfxColors.Hex(0x7A6055) : SfxColors.White);
            }
        }
    }

    public enum PatternState { Unused, Used, Selected, Playing }

    /// <summary>
    /// A pattern as the SONG page draws it: its letter, along its foot how many sounds play in each sixteenth, and in its
    /// corner 32 or 64 when its grid is finer than sixteenths.
    /// </summary>
    public class PatternTile : VisualElement
    {
        readonly TextElement letter, grid;
        readonly VisualElement bars;
        readonly int[] counts = new int[StepRow.Steps];
        PatternState state;
        public event Action clicked;

        public PatternTile(char name)
        {
            AddToClassList("sfx-pattern");
            letter = SfxUI.Text(name.ToString(), "sfx-pattern__letter");
            bars = SfxUI.Box("sfx-pattern__bars");
            bars.pickingMode = PickingMode.Ignore;
            bars.generateVisualContent += Draw;
            grid = SfxUI.Text("", "sfx-pattern__grid");
            grid.pickingMode = PickingMode.Ignore;
            Add(letter); Add(bars); Add(grid);
            this.AddManipulator(new Clickable(() => clicked?.Invoke()));
            Set(null, PatternState.Unused);
        }

        /// <summary>
        /// soundsOnStep: for each of the 16 sixteenths, how many sounds are set anywhere in it (null: none).
        /// steps: the pattern's grid - 32 or 64 stands in the tile's corner, 16 shows nothing.
        /// </summary>
        public void Set(int[] soundsOnStep, PatternState newState, int steps = 16)
        {
            for (int i = 0; i < counts.Length; i++) counts[i] = soundsOnStep != null && i < soundsOnStep.Length ? soundsOnStep[i] : 0;
            state = newState;
            style.backgroundColor = Back;
            letter.style.color = Fore;
            grid.text = steps > StepRow.Steps ? steps.ToString() : "";
            grid.style.display = steps > StepRow.Steps ? DisplayStyle.Flex : DisplayStyle.None;
            grid.style.color = SfxColors.Mix(Back, Fore, 0.8f);
            bars.MarkDirtyRepaint();
        }

        /// <summary>What stands in the corner: "32", "64", or nothing for a pattern of 16 steps.</summary>
        public string GridText => grid.text;
        /// <summary>The sounds counted in a column (a sixteenth, 0..15).</summary>
        public int Column(int i) => i >= 0 && i < counts.Length ? counts[i] : 0;

        Color Back => state == PatternState.Playing ? SfxColors.White : state == PatternState.Selected ? SfxColors.Pattern : state == PatternState.Used ? SfxColors.Hex(0x3A2A22) : SfxColors.Hex(0x1A120E);
        Color Fore => state == PatternState.Playing || state == PatternState.Selected ? SfxColors.Bg : state == PatternState.Used ? SfxColors.Text : SfxColors.Off;

        void Draw(MeshGenerationContext c)
        {
            Rect r = bars.contentRect;
            if (r.width < 16f) return;
            var p = c.painter2D;
            Color colour = SfxColors.Mix(Back, Fore, 0.7f);
            float pitch = r.width / counts.Length;
            for (int i = 0; i < counts.Length; i++)
            {
                int n = counts[i];
                if (n <= 0) continue;
                float h = Mathf.Min(r.height, 2f + n * 2f);
                SfxUI.Rect(p, r.x + i * pitch, r.yMax - h, Mathf.Max(1f, pitch - 1f), h, colour);
            }
        }
    }

    /// <summary>
    /// A small fader that only shows, as a tile of the MIX page: a bar filled to the value with a mark where "as designed"
    /// is (SetLevel) – or a line with a tick for a pan (SetPan).
    /// </summary>
    public class SfxFader : VisualElement
    {
        float value, mark = -1f;
        bool pan;
        Color colour = SfxColors.Ember;

        public SfxFader()
        {
            AddToClassList("sfx-fader");
            generateVisualContent += Draw;
        }

        /// <summary>value01: how full. mark01: where the mark stands (below 0: none).</summary>
        public void SetLevel(float value01, float mark01, Color fill) { pan = false; value = Mathf.Clamp01(value01); mark = mark01; colour = fill; MarkDirtyRepaint(); }
        /// <summary>pan01: 0 left .. 0.5 the middle .. 1 right.</summary>
        public void SetPan(float pan01, Color tick) { pan = true; value = Mathf.Clamp01(pan01); colour = tick; MarkDirtyRepaint(); }

        void Draw(MeshGenerationContext c)
        {
            Rect r = contentRect;
            if (r.width < 6f || r.height < 3f) return;
            var p = c.painter2D;
            float mid = r.y + r.height * 0.5f;
            if (pan)
            {
                SfxUI.Rect(p, r.x, mid - 0.5f, r.width, 1f, SfxColors.Line);
                SfxUI.Rect(p, r.x + value * (r.width - 3f), mid - 4.5f, 3f, 9f, colour);
                return;
            }
            SfxUI.Rect(p, r.x, mid - 3.5f, r.width, 7f, SfxColors.Line);
            SfxUI.Rect(p, r.x, mid - 3.5f, r.width * value, 7f, colour);
            if (mark >= 0f) SfxUI.Rect(p, r.x + r.width * Mathf.Clamp01(mark), mid - 6.5f, 1f, 13f, SfxColors.Dim);
        }
    }
}
