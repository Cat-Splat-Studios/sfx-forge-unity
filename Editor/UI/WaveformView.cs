// SPDX-License-Identifier: MIT
//
// The picture of a sound: SfxPicture renders the samples the device draws a sound from, WaveformView draws them
// and, over them, a line for what the page's knobs shape.
using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatSplat.SfxForge.Editor
{
    /// <summary>
    /// The samples the device draws a sound from (preview_render in the firmware): one voice, dry – no chorus, delay or
    /// reverb –, mono, at 24 kHz, never more than 1.5 s, always the same noise; a sustained sound is held 0.8 s,
    /// then let go; it ends where the sound has faded. So a picture here is the picture there, and "N s long" is
    /// the same number.
    /// </summary>
    public static class SfxPicture
    {
        public const int Rate = 24000;

        /// <summary>The most samples a picture has: 1.5 s, in blocks of 64.</summary>
        public const int Max = 35968;

        /// <summary>A sustained sound is held this long, then let go.</summary>
        public const float Hold = 0.8f;
        const int Block = 64;
        static readonly float[] block = new float[Block];
        static byte[] voice;

        /// <summary>
        /// Renders the patch into <paramref name="into"/> (at least Max long; what the sound does not fill is silence)
        /// and returns how many samples the picture has. 0: no native library, or not a patch.
        /// </summary>
        public static int Render(SfxPatch patch, float[] into)
        {
            if (into == null || into.Length < Max || !SfxNative.Available)
            {
                return 0;
            }

            if (voice == null)
            {
                voice = new byte[SfxNative.VoiceSize];
            }

            Array.Clear(voice, 0, voice.Length);
            Array.Clear(into, 0, Max);
            int n = 0;
            int last = 0;
            var pin = GCHandle.Alloc(voice, GCHandleType.Pinned);
            try
            {
                IntPtr v = pin.AddrOfPinnedObject();
                if (!SfxNative.Trigger(v, patch.Pack(), 1f, 0f, Rate, 12345u))
                {
                    return 0;
                }

                var none = SfxMods.None;
                bool hold = patch.IsSustained;
                int letGo = (int)(Hold * Rate);
                while (n < Max && SfxNative.Active(v))
                {
                    if (hold && n >= letGo)
                    {
                        SfxNative.Release(v);
                    }

                    Array.Clear(block, 0, Block); // (the voice adds to what is there)
                    SfxNative.Render(v, ref none, block, Block);
                    Array.Copy(block, 0, into, n, Block);
                    n += Block;
                }
            }
            finally
            {
                pin.Free();
            }

            float peak = 0.001f;
            for (int i = 0; i < n; i++)
            {
                float a = into[i] < 0f ? -into[i] : into[i];
                if (a > peak)
                {
                    peak = a;
                }
            }

            for (int i = 0; i < n; i++)
            {
                float a = into[i] < 0f ? -into[i] : into[i];
                if (a > peak * 0.03f)
                {
                    last = i;
                }
            }

            return last + 240 < 1200 ? 1200
                : last + 240 > n ? n
                : last + 240;
        }
    }

    /// <summary>
    /// The picture of a sound page: the sound's whole shape from start to end in its engine's colour – and over it, in
    /// white, a line for what the page's knobs shape (scene() in the firmware). Give it samples (SetSamples), a line
    /// (Overlay + SetOverlay), a thin upright mark (SetMark), a caption above and below; or a message instead of a
    /// picture (SetMessage).
    /// </summary>
    public class WaveformView : VisualElement
    {
        public const int Columns = 384;
        public const int OverlayMax = 256;

        readonly float[] lo = new float[Columns];
        readonly float[] hi = new float[Columns];
        readonly TextElement top;
        readonly TextElement bottom;
        readonly TextElement message;
        readonly TextElement messageSmall;
        int columns;
        int overlayCount;
        float peak = 1f;
        float mark = -1f;
        bool overlayDim;
        bool bright = true;
        Color accent = SfxColors.Ember;

        /// <summary>
        /// The white line's points, in the picture's own measure: x 0..1 across the middle four fifths, y 0 = the top
        /// of the wave's room, 1 = its bottom. Fill it, then SetOverlay(count).
        /// </summary>
        public readonly Vector2[] Overlay = new Vector2[OverlayMax];

        public WaveformView()
        {
            AddToClassList("sfx-wave");
            top = SfxUI.Text("", "sfx-caption");
            top.AddToClassList("sfx-caption--top");
            bottom = SfxUI.Text("", "sfx-caption");
            bottom.AddToClassList("sfx-caption--bottom");
            message = SfxUI.Text("", "sfx-wave__message");
            messageSmall = SfxUI.Text("", "sfx-wave__message-small");
            message.style.display = DisplayStyle.None;
            messageSmall.style.display = DisplayStyle.None;
            Add(top);
            Add(bottom);
            Add(message);
            Add(messageSmall);
            generateVisualContent += Draw;
        }

        /// <summary>
        /// A small picture (a tile of a patch): the wave fills the height, no room is kept for captions.
        /// </summary>
        public bool Compact;

        /// <summary>The loudest sample of what is shown.</summary>
        public float Peak => peak;
        public bool HasPicture => columns > 0;

        /// <summary>
        /// The sound: count mono samples. It is drawn across the whole width, as loud as the room allows.
        /// </summary>
        public void SetSamples(float[] mono, int count)
        {
            columns = 0;
            peak = 0.001f;
            if (mono != null && count > 0)
            {
                columns = Columns;
                for (int c = 0; c < Columns; c++)
                {
                    int a = (int)((long)c * count / Columns);
                    int b = (int)((long)(c + 1) * count / Columns);
                    float l = 0f;
                    float h = 0f;
                    for (int i = a; i < b; i++)
                    {
                        float s = mono[i];
                        if (s < l)
                        {
                            l = s;
                        }

                        if (s > h)
                        {
                            h = s;
                        }
                    }

                    lo[c] = l;
                    hi[c] = h;
                    if (h > peak)
                    {
                        peak = h;
                    }

                    if (-l > peak)
                    {
                        peak = -l;
                    }
                }
            }

            message.style.display = DisplayStyle.None;
            messageSmall.style.display = DisplayStyle.None;
            MarkDirtyRepaint();
        }

        /// <summary>
        /// accent: the engine's colour. own: the page is about the whole sound (bright), not about a
        /// line over it (dim).
        /// </summary>
        public void SetColour(Color colour, bool own)
        {
            if (colour == accent && own == bright)
            {
                return;
            }

            accent = colour;
            bright = own;
            MarkDirtyRepaint();
        }

        /// <summary>
        /// The first count points of Overlay are the line; 0: none. dim: the line of something that is off.
        /// </summary>
        public void SetOverlay(int count, bool dim = false)
        {
            overlayCount = Mathf.Clamp(count, 0, OverlayMax);
            overlayDim = dim;
            MarkDirtyRepaint();
        }

        /// <summary>
        /// A thin upright line from the top of the wave's room to its bottom, under the white line: x in Overlay's
        /// measure, below 0: none. (Where a held sound is let go.)
        /// </summary>
        public void SetMark(float x)
        {
            if (Mathf.Approximately(x, mark))
            {
                return;
            }

            mark = x;
            MarkDirtyRepaint();
        }

        /// <summary>Where the mark stands, below 0: there is none.</summary>
        public float Mark => mark;

        public void SetCaptions(string above, string below)
        {
            top.text = above;
            bottom.text = below;
        }

        /// <summary>No picture: these words instead (the native library is missing).</summary>
        public void SetMessage(string big, string small)
        {
            columns = 0;
            overlayCount = 0;
            mark = -1f;
            message.text = big;
            messageSmall.text = small;
            message.style.display = DisplayStyle.Flex;
            messageSmall.style.display = DisplayStyle.Flex;
            top.text = "";
            bottom.text = "";
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext c)
        {
            Rect r = contentRect;
            if (r.width < 16f || r.height < 16f)
            {
                return;
            }

            float mid = r.y + r.height * 0.5f;
            float amp = Compact ? Mathf.Max(4f, r.height * 0.5f - 1f) : Mathf.Max(4f, r.height * 0.5f - 19f);
            var p = c.painter2D;

            // (a message instead of a picture: nothing runs through its words)
            if (columns == 0)
            {
                return;
            }

            p.strokeColor = SfxColors.Line;
            p.lineWidth = 1f;
            p.lineCap = LineCap.Butt;
            p.BeginPath();
            p.MoveTo(new Vector2(r.x, mid));
            p.LineTo(new Vector2(r.xMax, mid));
            p.Stroke();
            float gain = amp / peak;
            Strip(c, r, mid, gain, SfxColors.Mix(SfxColors.Panel, accent, bright ? 0.42f : 0.18f));
            if (bright)
            {
                Strip(c, r, mid, gain * 0.6f, accent);
            }

            if (mark >= 0f)
            {
                float x = Mathf.Round(r.x + r.width * 0.1f + mark * r.width * 0.8f) + 0.5f;
                p.strokeColor = SfxColors.Line;
                p.lineWidth = 1f;
                p.lineCap = LineCap.Butt;
                p.BeginPath();
                p.MoveTo(new Vector2(x, mid - amp));
                p.LineTo(new Vector2(x, mid + amp));
                p.Stroke();
            }

            if (overlayCount > 1)
            {
                float x0 = r.x + r.width * 0.1f;
                float pw = r.width * 0.8f;
                float y0 = mid - amp;
                float ph = amp * 2f;
                p.strokeColor = overlayDim ? SfxColors.Dim : SfxColors.White;
                p.lineWidth = 2f;
                p.lineJoin = LineJoin.Round;
                p.lineCap = LineCap.Round;
                p.BeginPath();
                p.MoveTo(new Vector2(x0 + Overlay[0].x * pw, y0 + Overlay[0].y * ph));
                for (int i = 1; i < overlayCount; i++)
                {
                    p.LineTo(new Vector2(x0 + Overlay[i].x * pw, y0 + Overlay[i].y * ph));
                }

                p.Stroke();
            }
        }

        // the columns as one band of triangles: no path, nothing to tessellate
        void Strip(MeshGenerationContext c, Rect r, float mid, float gain, Color colour)
        {
            var mesh = c.Allocate(columns * 2, (columns - 1) * 6);
            float dx = r.width / (columns - 1);
            Color32 tint = colour;
            for (int i = 0; i < columns; i++)
            {
                float x = r.x + i * dx;
                mesh.SetNextVertex(
                    new Vertex { position = new Vector3(x, mid - hi[i] * gain - 0.5f, Vertex.nearZ), tint = tint }
                );
                mesh.SetNextVertex(
                    new Vertex { position = new Vector3(x, mid - lo[i] * gain + 0.5f, Vertex.nearZ), tint = tint }
                );
            }

            for (int i = 0; i < columns - 1; i++)
            {
                int a = i * 2;
                mesh.SetNextIndex((ushort)a);
                mesh.SetNextIndex((ushort)(a + 2));
                mesh.SetNextIndex((ushort)(a + 3));
                mesh.SetNextIndex((ushort)a);
                mesh.SetNextIndex((ushort)(a + 3));
                mesh.SetNextIndex((ushort)(a + 1));
            }
        }
    }
}
