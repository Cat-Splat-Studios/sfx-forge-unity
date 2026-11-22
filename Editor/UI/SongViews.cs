// SPDX-License-Identifier: MIT
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatSplat.SfxForge.Editor
{
    /// <summary>
    /// INTENSITY as the stage draws it: a bar filled to the value, and over it a mark for every sound's LAYER – in the
    /// sound's colour, dim while the intensity is below it. Dragging along the bar moves the value (changed).
    /// Set the marks with SetMark, the value with Value.
    /// </summary>
    public class IntensityStrip : VisualElement
    {
        public const int Marks = 16;
        readonly int[] at = new int[Marks];             // 0..127, below 0: no mark
        readonly Color[] colours = new Color[Marks];
        readonly TextElement text;
        int value = 127;
        public event Action<int> changed;
        /// <summary>The bar's colour.</summary>
        public Color Fill = SfxColors.Ember;
        /// <summary>False: it only shows.</summary>
        public bool Interactive = true;

        public IntensityStrip()
        {
            AddToClassList("sfx-intensity");
            for (int i = 0; i < Marks; i++) at[i] = -1;
            text = SfxUI.Text("", "sfx-intensity__text");
            Add(text);
            generateVisualContent += Draw;
            RegisterCallback<PointerDownEvent>(e => { if (!Interactive || e.button != 0) return; this.CapturePointer(e.pointerId); Take(e.localPosition.x); e.StopPropagation(); });
            RegisterCallback<PointerMoveEvent>(e => { if (this.HasPointerCapture(e.pointerId)) Take(e.localPosition.x); });
            RegisterCallback<PointerUpEvent>(e => { if (this.HasPointerCapture(e.pointerId)) this.ReleasePointer(e.pointerId); });
        }

        /// <summary>0..127.</summary>
        public int Value
        {
            get => value;
            set { this.value = Mathf.Clamp(value, 0, 127); MarkDirtyRepaint(); }
        }

        /// <summary>What stands at the bar's right end: "100%", or "FM BASS plays from 28%".</summary>
        public string Text { get => text.text; set => text.text = value; }

        /// <summary>The mark of a sound (0..15): where (0..127), below 0: none.</summary>
        public void SetMark(int sound, int level127, Color colour)
        {
            if (sound < 0 || sound >= Marks) return;
            at[sound] = level127; colours[sound] = colour;
            MarkDirtyRepaint();
        }

        public int Mark(int sound) => sound >= 0 && sound < Marks ? at[sound] : -1;

        void Take(float x)
        {
            float w = contentRect.width - 8f;
            if (w < 8f) return;
            int v = Mathf.Clamp(Mathf.RoundToInt((x - 4f) / w * 127f), 0, 127);
            if (v == value) return;
            Value = v;
            changed?.Invoke(v);
        }

        void Draw(MeshGenerationContext c)
        {
            Rect r = contentRect;
            if (r.width < 24f || r.height < 12f) return;
            var p = c.painter2D;
            float top = r.y + 6f, h = r.height - 6f, w = r.width;
            SfxUI.RoundRect(p, r.x, top, w, h, 5f, SfxColors.Panel);
            SfxUI.RoundRect(p, r.x, top, 6f + (w - 8f) * value / 127f, h, 5f, Fill);
            for (int s = 0; s < Marks; s++)
            {
                if (at[s] < 0) continue;
                SfxUI.Rect(p, r.x + 2f + (w - 8f) * at[s] / 127f, r.y, 3f, 5f, value >= at[s] ? colours[s] : SfxColors.Mix(SfxColors.Bg, colours[s], 0.45f));
            }
        }
    }

    /// <summary>
    /// An automation lane over one pattern, as the stage draws it: a cell a step, as high as the knob stands there. A
    /// point is bright with a white top; where it only holds, the cell is dim. A dot: the sound plays on that step.
    /// Fill Point (-1 = no point, else the value) and Plays, set Count (the cells: 16, 32 or 64 - the pattern's grid),
    /// Hi (the knob's highest value) and Length, then Changed.
    /// </summary>
    public class LaneRow : VisualElement
    {
        public const int Steps = 16;
        /// <summary>The most cells a row has: a pattern of 64 steps.</summary>
        public const int MaxSteps = 64;
        /// <summary>The cells drawn: 16, 32 or 64. Point, Plays and Length count in these.</summary>
        public int Count = Steps;
        public readonly int[] Point = new int[MaxSteps];
        public readonly bool[] Plays = new bool[MaxSteps];
        public int Hi = 126, Length = Steps;
        public Color Accent = SfxColors.Ember;

        public LaneRow()
        {
            AddToClassList("sfx-lane-row");
            for (int i = 0; i < MaxSteps; i++) Point[i] = -1;
            generateVisualContent += Draw;
        }

        public void Changed() => MarkDirtyRepaint();

        void Draw(MeshGenerationContext c)
        {
            if (Count > Steps) { DrawFine(c); return; }
            Rect r = contentRect;
            if (r.width < Steps * 2 || r.height < 6f) return;
            var p = c.painter2D;
            float pitch = r.width / Steps, w = Mathf.Max(1f, pitch - 2f), h = r.height;
            int hi = Mathf.Max(1, Hi), last = -1;
            for (int i = 0; i < Steps; i++)
            {
                float x = r.x + i * pitch;
                bool off = i >= Length;
                SfxUI.RoundRect(p, x, r.y, w, h, 3f, off ? SfxColors.Hex(0x1C130F) : (i / 4 & 1) != 0 ? SfxColors.Hex(0x2B1D17) : SfxColors.Hex(0x38261E));
                if (Point[i] >= 0) last = Point[i];
                if (last >= 0 && !off)
                {
                    float bar = 2f + Mathf.Min(last, hi) * (h - 2f) / hi;
                    SfxUI.Rect(p, x, r.y + h - bar, w, bar, Point[i] >= 0 ? Accent : SfxColors.Mix(SfxColors.Bg, Accent, 0.40f));
                    if (Point[i] >= 0) SfxUI.Rect(p, x, r.y + h - bar, w, 2f, SfxColors.White);
                }
                if (Plays[i]) SfxUI.Rect(p, x + w * 0.5f - 2f, r.y + 1f, 4f, 3f, last >= 0 && !off && last > hi * 3 / 4 ? SfxColors.Bg : SfxColors.Mix(SfxColors.Bg, Accent, 0.7f));
            }
        }

        // 32 or 64 cells: plain rectangles on whole pixels, where a row of steps of that grid has them (StepRow.Fine)
        void DrawFine(MeshGenerationContext c)
        {
            Rect r = contentRect;
            int count = Mathf.Min(Count, MaxSteps), div = count / Steps;
            if (!StepRow.Fine(r.width, count, out int cell, out int wide) || r.height < 6f) return;
            var p = c.painter2D;
            float x0 = Mathf.Round(r.x), h = r.height;
            int hi = Mathf.Max(1, Hi), last = -1;
            for (int i = 0; i < count; i++)
            {
                float x = x0 + i * cell + i / div;
                bool off = i >= Length;
                SfxUI.Rect(p, x, r.y, wide, h, off ? SfxColors.Hex(0x1C130F) : (i / (4 * div) & 1) != 0 ? SfxColors.Hex(0x2B1D17) : SfxColors.Hex(0x38261E));
                if (Point[i] >= 0) last = Point[i];
                if (last >= 0 && !off)
                {
                    float bar = Mathf.Round(2f + Mathf.Min(last, hi) * (h - 2f) / hi);
                    SfxUI.Rect(p, x, r.y + h - bar, wide, bar, Point[i] >= 0 ? Accent : SfxColors.Mix(SfxColors.Bg, Accent, 0.40f));
                    if (Point[i] >= 0) SfxUI.Rect(p, x, r.y + h - bar, wide, 2f, SfxColors.White);
                }
                if (Plays[i]) SfxUI.Rect(p, x + (wide >= 4 ? wide / 2 - 1 : 0), r.y + 1f, wide >= 4 ? 2f : wide, 3f, last >= 0 && !off && last > hi * 3 / 4 ? SfxColors.Bg : SfxColors.Mix(SfxColors.Bg, Accent, 0.7f));
            }
        }
    }

    /// <summary>
    /// A sound on the MIX page: its name, its fader with the mark where "as designed" is, its pan under it – and M while
    /// it is muted (then all of it grey). The chosen one has a white frame. clicked: the mouse took it.
    /// </summary>
    public class MixTile : VisualElement
    {
        readonly TextElement title, mark;
        readonly SfxFader fader = new SfxFader(), pan = new SfxFader();
        public event Action clicked;

        public MixTile()
        {
            AddToClassList("sfx-mix-tile");
            var top = SfxUI.Box("sfx-row");
            top.pickingMode = PickingMode.Ignore;
            title = SfxUI.Text("", "sfx-mix-tile__name");
            mark = SfxUI.Text("", "sfx-mix-tile__mark");
            top.Add(title); top.Add(mark);
            fader.pickingMode = PickingMode.Ignore; pan.pickingMode = PickingMode.Ignore;
            Add(top); Add(fader); Add(pan);
            this.AddManipulator(new Clickable(() => clicked?.Invoke()));
        }

        public string NameText => title.text;
        public bool ShowsMute => mark.text == "M";

        /// <summary>volume, panning: 0..127 as the song keeps them (100 = as designed, 64 = the middle).</summary>
        public void Set(string name, Color colour, int volume, int panning, bool muted, bool selected)
        {
            title.text = name;
            title.style.color = muted ? SfxColors.Off : SfxColors.Text;
            mark.text = muted ? "M" : "";
            fader.SetLevel(volume / 127f, 100f / 127f, muted ? SfxColors.Off : colour);
            pan.SetPan(panning / 127f, muted ? SfxColors.Dim : SfxColors.Text);
            EnableInClassList("sfx-mix-tile--on", selected);
        }
    }

    /// <summary>
    /// The MASTER page's two pictures: the delay as its echoes (the sound, then left, right, left … each feedback times
    /// the one before, as far apart as the delay's time), the reverb as its fading (longer with the room's size, darker
    /// with its damping). Set takes the song's values: time 0..5, the others 0..127.
    /// </summary>
    public class MasterView : VisualElement
    {
        static readonly int[] Sixteenths = { 1, 2, 3, 4, 6, 8 };
        readonly VisualElement delay, reverb;
        int time = 2, feedback = 64, size = 91, damping = 40;

        public MasterView()
        {
            AddToClassList("sfx-master");
            delay = Half("DELAY", SfxColors.Pattern, DrawDelay);
            reverb = Half("REVERB", SfxColors.Engines[(int)SfxEngine.Wavetable], DrawReverb);
            reverb.style.marginLeft = 4;
        }

        VisualElement Half(string name, Color colour, Action<MeshGenerationContext> draw)
        {
            var half = SfxUI.Box("sfx-master__half");
            var title = SfxUI.Text(name, "sfx-master__title");
            title.style.color = colour;
            half.Add(title);
            half.generateVisualContent += draw;
            Add(half);
            return half;
        }

        public void Set(int delayTime, int delayFeedback, int reverbSize, int reverbDamping)
        {
            time = ((delayTime % Sixteenths.Length) + Sixteenths.Length) % Sixteenths.Length;
            feedback = delayFeedback & 127; size = reverbSize & 127; damping = reverbDamping & 127;
            delay.MarkDirtyRepaint(); reverb.MarkDirtyRepaint();
        }

        // (the firmware's pictures are 202 px wide and 84 high: the same shapes, in this element's own measure)
        void DrawDelay(MeshGenerationContext c)
        {
            Rect r = delay.contentRect;
            if (r.width < 40f || r.height < 30f) return;
            var p = c.painter2D;
            float unit = r.width / 202f, baseY = r.yMax - 4f, tall = r.height - 26f, a = 1f, fb = feedback * (0.45f / 64f), gap = 8f * Sixteenths[time] * unit;
            SfxUI.Rect(p, r.x, baseY, r.width, 1f, SfxColors.Line);
            float x = r.x + 2f * unit;
            for (int k = 0; x < r.xMax - 5f && a > 0.02f; k++, x += gap)
            {
                float h = tall * a;
                SfxUI.Rect(p, x, baseY - h, Mathf.Max(3f, 5f * unit), h, k == 0 ? SfxColors.White : (k & 1) != 0 ? SfxColors.Pattern : SfxColors.Game);
                a *= k == 0 ? 0.7f : fb;
            }
        }

        void DrawReverb(MeshGenerationContext c)
        {
            Rect r = reverb.contentRect;
            if (r.width < 40f || r.height < 30f) return;
            var p = c.painter2D;
            float unit = r.width / 202f, baseY = r.yMax - 4f, tall = r.height - 26f, tau = 14f + size * 0.9f, damp = damping / 127f;
            Color far = SfxColors.Hex(0x5B3F86), tone = SfxColors.Mix(SfxColors.Hex(0xE9D6FF), far, damp);
            SfxUI.Rect(p, r.x, baseY, r.width, 1f, SfxColors.Line);
            for (int i = 0; i < 200; i += 4)
            {
                float h = tall * Mathf.Pow(2f, -i / tau);
                if (h < 1f) break;
                SfxUI.Rect(p, r.x + (1f + i) * unit, baseY - h, 4f * unit, h, SfxColors.Mix(tone, far, i / 200f * damp));
            }
        }
    }
}
