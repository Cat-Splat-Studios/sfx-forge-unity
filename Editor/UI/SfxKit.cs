// SPDX-License-Identifier: MIT
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatSplat.SfxForge.Editor
{
    /// <summary>The display's colours (sfx-forge/sfx-forge.c). SfxForge.uss has the same ones as variables.</summary>
    public static class SfxColors
    {
        public static readonly Color Bg = Hex(0x180F0B), Panel = Hex(0x2B1C15), Line = Hex(0x4A3026), Dim = Hex(0x907A6F), Text = Hex(0xF4ECE8);
        public static readonly Color Key = Hex(0x36241C), Off = Hex(0x6A5248), Ember = Hex(0xFF5A36), Linked = Hex(0x6EE7A8), Unlinked = Hex(0x48332A);
        public static readonly Color Pattern = Hex(0x36C5FF), Warn = Hex(0xFF8A3C), Game = Hex(0xFFD23F), White = Color.white;
        /// <summary>Impact, UI Blip, Sci-Fi FM, Foley, Wavetable.</summary>
        public static readonly Color[] Engines = { Hex(0xFF5A36), Hex(0xFFD23F), Hex(0x36C5FF), Hex(0x7BE07B), Hex(0xC48BFF) };

        public static Color Engine(SfxEngine e) => Engines[(int)e % Engines.Length];
        public static Color Hex(uint rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
        /// <summary>From a to b, t of the way (the firmware's vg_mix).</summary>
        public static Color Mix(Color a, Color b, float t) => new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, 1f);
    }

    /// <summary>The kit's small helpers: the panel everything sits in, its stylesheet, texts, painter shapes.</summary>
    public static class SfxUI
    {
        const string SheetGuid = "5f0e6a1c9b7d4e3a8c2f1b6d7a9e4c10";          // (SfxForge.uss.meta: found wherever the package lies, whatever it is called)
        /// <summary>Narrower than this, a panel has the class sfx-narrow.</summary>
        public const float NarrowBelow = 400f;
        static StyleSheet sheet;
        static bool sheetMissed;

        /// <summary>For the package's tests and pictures: show what a platform without the native library shows.</summary>
        internal static bool PretendNativeMissing;
        /// <summary>The device's engine is here (SfxNative): there is a picture, a preview, a WAV.</summary>
        public static bool NativeThere => SfxNative.Available && !PretendNativeMissing;

        public static StyleSheet Sheet
        {
            get
            {
                if (sheet == null)
                {
                    string path = AssetDatabase.GUIDToAssetPath(SheetGuid);
                    if (!string.IsNullOrEmpty(path)) sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                    if (sheet == null && !sheetMissed)
                    {
                        sheetMissed = true;                     // (said once, not at every panel)
                        Debug.LogWarning("SFX Forge: SfxForge.uss not found – its inspectors and its window show without their look. Reimport the package.");
                    }
                }
                return sheet;
            }
        }

        /// <summary>The device's dark panel with rounded corners: the root of an inspector or of the window. It carries the stylesheet.</summary>
        public static VisualElement Panel()
        {
            var root = new VisualElement();
            root.AddToClassList("sfx-root");
            if (Sheet != null) root.styleSheets.Add(Sheet);
            root.RegisterCallback<GeometryChangedEvent>(e => root.EnableInClassList("sfx-narrow", e.newRect.width < NarrowBelow));
            return root;
        }

        /// <summary>A text with a class of the stylesheet. Never takes the mouse.</summary>
        public static TextElement Text(string text, string className)
        {
            var t = new TextElement { text = text, pickingMode = PickingMode.Ignore };
            t.AddToClassList(className);
            return t;
        }

        public static VisualElement Box(string className)
        {
            var e = new VisualElement();
            e.AddToClassList(className);
            return e;
        }

        /// <summary>A filled rectangle with round corners, in an element's generateVisualContent.</summary>
        public static void RoundRect(Painter2D p, float x, float y, float w, float h, float r, Color fill)
        {
            if (w <= 0f || h <= 0f) return;
            r = Mathf.Min(r, Mathf.Min(w, h) * 0.5f);
            p.fillColor = fill;
            p.BeginPath();
            p.MoveTo(new Vector2(x + r, y));
            p.LineTo(new Vector2(x + w - r, y));
            if (r > 0f) p.ArcTo(new Vector2(x + w, y), new Vector2(x + w, y + r), r);
            p.LineTo(new Vector2(x + w, y + h - r));
            if (r > 0f) p.ArcTo(new Vector2(x + w, y + h), new Vector2(x + w - r, y + h), r);
            p.LineTo(new Vector2(x + r, y + h));
            if (r > 0f) p.ArcTo(new Vector2(x, y + h), new Vector2(x, y + h - r), r);
            p.LineTo(new Vector2(x, y + r));
            if (r > 0f) p.ArcTo(new Vector2(x, y), new Vector2(x + r, y), r);
            p.ClosePath();
            p.Fill();
        }

        /// <summary>A menu of names under an element; ticked: the one that is chosen now (-1: none). picked tells which was taken.</summary>
        public static void Menu(VisualElement under, string[] names, int ticked, Action<int> picked)
        {
            var menu = new GenericDropdownMenu();
            for (int i = 0; i < names.Length; i++)
            {
                int pick = i;
                menu.AddItem(names[i], i == ticked, () => picked?.Invoke(pick));
            }
#pragma warning disable CS0618                              // (the newer overload is not in every Unity this package supports)
            menu.DropDown(under.worldBound, under, false);
#pragma warning restore CS0618
        }

        public static void Rect(Painter2D p, float x, float y, float w, float h, Color fill) => RoundRect(p, x, y, w, h, 0f, fill);
    }

    /// <summary>
    /// The display's top line: a coloured number tile, the NAME, the ENGINE in its colour – and, right, a dot that is green
    /// while the link is open. The number tile can be clicked (numberClicked).
    /// </summary>
    public class SfxHeader : VisualElement
    {
        readonly TextElement number, title, sub, linkText;
        readonly VisualElement dot;
        public event Action numberClicked;
        /// <summary>The number tile: give it a tooltip, or read its place for a menu.</summary>
        public VisualElement NumberTile => number;
        /// <summary>Where more can go, right of the names and left of the link.</summary>
        public VisualElement Slot { get; }

        public SfxHeader()
        {
            AddToClassList("sfx-header");
            number = SfxUI.Text("", "sfx-number");
            number.pickingMode = PickingMode.Position;
            number.AddManipulator(new Clickable(() => numberClicked?.Invoke()));
            title = SfxUI.Text("", "sfx-name");
            sub = SfxUI.Text("", "sfx-engine");
            Slot = SfxUI.Box("sfx-spacer");
            Slot.AddToClassList("sfx-row");
            dot = SfxUI.Box("sfx-link-dot");
            linkText = SfxUI.Text("LINK", "sfx-link-text");
            Add(number); Add(title); Add(sub); Add(Slot); Add(dot); Add(linkText);
        }

        public void Set(string numberText, string nameText, string subText, Color accent)
        {
            number.text = numberText; title.text = nameText; sub.text = subText;
            number.style.backgroundColor = accent;
            sub.style.color = accent;
        }

        public void SetLink(bool linked, string tip = null)
        {
            dot.EnableInClassList("sfx-link-dot--on", linked);
            linkText.EnableInClassList("sfx-link-text--on", linked);
            dot.tooltip = tip;
        }

        /// <summary>No dot: a header of something the link has nothing to do with.</summary>
        public void HideLink() { dot.style.display = DisplayStyle.None; linkText.style.display = DisplayStyle.None; }
    }

    /// <summary>
    /// A notice across the panel, for what must not be missed (an asset from a newer version): a panel-coloured strip
    /// with an orange edge, one bold orange line, one small dim line. Not shown until Set gives it a text.
    /// </summary>
    public class SfxBanner : VisualElement
    {
        readonly TextElement a, b;

        public SfxBanner()
        {
            AddToClassList("sfx-banner");
            a = SfxUI.Text("", "sfx-banner__a");
            b = SfxUI.Text("", "sfx-banner__b");
            Add(a); Add(b);
            style.display = DisplayStyle.None;
        }

        /// <summary>What its two lines say now, and whether it shows.</summary>
        public string Bold => a.text;
        public string Small => b.text;
        public bool Shown => style.display != DisplayStyle.None;

        /// <summary>bold null or "": the banner is gone.</summary>
        public void Set(string bold, string small = null)
        {
            a.text = bold ?? ""; b.text = small ?? "";
            b.style.display = string.IsNullOrEmpty(small) ? DisplayStyle.None : DisplayStyle.Flex;
            style.display = string.IsNullOrEmpty(bold) ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }

    /// <summary>Two lines, as under the display's column of values: one bold and coloured, one small and dim.</summary>
    public class SfxFooter : VisualElement
    {
        readonly TextElement a, b;

        public SfxFooter()
        {
            AddToClassList("sfx-footer");
            a = SfxUI.Text("", "sfx-footer__a");
            b = SfxUI.Text("", "sfx-footer__b");
            Add(a); Add(b);
        }

        /// <summary>What its two lines say now.</summary>
        public string Bold => a.text;
        public string Small => b.text;

        public void Set(string bold, Color colour, string small)
        {
            a.text = bold; a.style.color = colour; b.text = small;
            a.style.display = string.IsNullOrEmpty(bold) ? DisplayStyle.None : DisplayStyle.Flex;
            b.style.display = string.IsNullOrEmpty(small) ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
