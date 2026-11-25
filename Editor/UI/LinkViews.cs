// SPDX-License-Identifier: GPL-3.0-or-later
//
// The UI kit's pieces for the link (Window > SFX Forge):
//   LinkBars   the LINK page: a line with a thin bar for each value that is sent
//   PortTile   a MIDI port as a value tile
//   PatchTile  a patch the device sent, with PLAY and SAVE
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatSplat.SfxForge.Editor
{
    /// <summary>
    /// The LINK page's panel (draw_link() in the firmware): a yellow title, and under it a line a value – its name, its
    /// number 0..127, a thin bar. There: what the game sends. In the window: what the device's knobs send.
    /// </summary>
    public class LinkBars : VisualElement
    {
        readonly TextElement title;
        readonly TextElement[] names;
        readonly TextElement[] numbers;
        readonly VisualElement[] fills;
        readonly int[] values;

        public LinkBars(string titleText, params string[] rowNames)
        {
            AddToClassList("sfx-bars");
            title = SfxUI.Text(titleText, "sfx-bars__title");
            Add(title);
            int n = rowNames.Length;
            names = new TextElement[n];
            numbers = new TextElement[n];
            fills = new VisualElement[n];
            values = new int[n];
            for (int i = 0; i < n; i++)
            {
                var row = SfxUI.Box("sfx-bars__row");
                names[i] = SfxUI.Text(rowNames[i], "sfx-bars__name");
                numbers[i] = SfxUI.Text("", "sfx-bars__value");
                row.Add(names[i]);
                row.Add(numbers[i]);
                var track = SfxUI.Box("sfx-bars__track");
                fills[i] = SfxUI.Box("sfx-bars__fill");
                track.Add(fills[i]);
                Add(row);
                Add(track);
                values[i] = int.MinValue;
                SetValue(i, -1);
            }
        }

        public int Count => values.Length;
        public string TitleText => title.text;

        /// <summary>What line i reads: "64", or "-" for a value that never came.</summary>
        public string ValueText(int i) => numbers[i].text;

        public int Value(int i) => values[i];

        /// <summary>value: 0..127; below 0: none came yet ("-", an empty bar).</summary>
        public void SetValue(int i, int value)
        {
            if (i < 0 || i >= values.Length || values[i] == value)
            {
                return;
            }

            values[i] = value;
            numbers[i].text = value < 0 ? "-" : Mathf.Min(value, 127).ToString();
            fills[i].style.width = Length.Percent(value < 0 ? 0f : Mathf.Min(value, 127) * 100f / 127f);
        }
    }

    /// <summary>
    /// A MIDI port as one of the display's value tiles: the ring (green all round while the port is open) with IN
    /// or OUT in it, a dim label, the port's name. A click tells clicked – unless Locked (the link is open: the
    /// ports are fixed).
    /// </summary>
    public class PortTile : VisualElement
    {
        readonly Knob knob;
        readonly TextElement label;
        readonly TextElement value;
        bool locked;
        public event Action clicked;

        public PortTile(string ringText)
        {
            AddToClassList("sfx-tile");
            AddToClassList("sfx-tile--panel");
            AddToClassList("sfx-port");
            knob = new Knob { Number = ringText };
            knob.pickingMode = PickingMode.Ignore;
            var texts = SfxUI.Box("sfx-tile__texts");
            texts.pickingMode = PickingMode.Ignore;
            label = SfxUI.Text("", "sfx-tile__label");
            value = SfxUI.Text("", "sfx-tile__value");
            value.AddToClassList("sfx-port__value");
            texts.Add(label);
            texts.Add(value);
            Add(knob);
            Add(texts);
            this.AddManipulator(
                new Clickable(() =>
                {
                    if (!locked)
                    {
                        clicked?.Invoke();
                    }
                })
            );
        }

        public string LabelText => label.text;
        public string ValueText => value.text;

        /// <summary>True: it does not click (and does not light under the mouse).</summary>
        public bool Locked
        {
            get => locked;
            set
            {
                locked = value;
                EnableInClassList("sfx-port--locked", value);
            }
        }

        /// <summary>
        /// open: the port is open now (the ring is green). there: false for a name that is none (dim).
        /// </summary>
        public void Show(string labelText, string portName, bool open, bool there)
        {
            label.text = labelText;
            value.text = portName;
            value.EnableInClassList("sfx-port__value--none", !there);
            knob.Set(open ? 1f : -1f, false, SfxColors.Linked);
        }
    }

    /// <summary>
    /// A patch the device sent, as a tile: the pad's number on the engine's colour, the engine, when it came, the
    /// sound's shape – and two small keys: PLAY, and SAVE (or SHOW once it is an asset).
    /// </summary>
    public class PatchTile : VisualElement
    {
        /// <summary>The picture's sample rate (SfxPicture: the device's own picture of a sound).</summary>
        public const int PictureRate = SfxPicture.Rate;

        readonly TextElement number;
        readonly TextElement engine;
        readonly TextElement time;
        readonly WaveformView wave;
        readonly SoftKey playKey;
        readonly SoftKey saveKey;
        bool saved;
        public event Action play,
            save,
            show;

        public PatchTile()
        {
            AddToClassList("sfx-patch");
            var top = SfxUI.Box("sfx-row");
            number = SfxUI.Text("", "sfx-patch__number");
            engine = SfxUI.Text("", "sfx-patch__engine");
            time = SfxUI.Text("", "sfx-patch__time");
            top.Add(number);
            top.Add(engine);
            top.Add(time);
            wave = new WaveformView { Compact = true };
            var keys = SfxUI.Box("sfx-patch__keys");
            playKey = new SoftKey("PLAY", () => play?.Invoke(), "Plays the patch here, with the device's engine.");
            saveKey = new SoftKey("SAVE", () =>
            {
                if (saved)
                {
                    show?.Invoke();
                }
                else
                {
                    save?.Invoke();
                }
            });
            keys.Add(playKey);
            keys.Add(saveKey);
            Add(top);
            Add(wave);
            Add(keys);
        }

        public string NumberText => number.text;
        public string EngineText => engine.text;
        public string TimeText => time.text;
        public SoftKey PlayKey => playKey;

        /// <summary>SAVE while the patch is no asset yet, SHOW afterwards.</summary>
        public SoftKey SaveKey => saveKey;
        public bool HasPicture => wave.HasPicture;

        public void Set(int pad, SfxEngine patchEngine, string timeText, bool isSaved)
        {
            Color c = SfxColors.Engine(patchEngine);
            number.text = (pad + 1).ToString();
            number.style.backgroundColor = c;
            engine.text = SfxUnits.EngineNames[(int)patchEngine % SfxUnits.EngineNames.Length];
            engine.style.color = c;
            time.text = timeText;
            wave.SetColour(c, true);
            saved = isSaved;
            saveKey.text = isSaved ? "SHOW" : "SAVE";
            saveKey.tooltip = isSaved
                ? "Shows the preset this patch was saved as."
                : "Saves the patch as a preset in the folder.";
        }

        /// <summary>
        /// The sound's shape, from the device's engine. Without the native library: no picture (the tile is lower),
        /// and PLAY is off.
        /// </summary>
        public void SetPicture(SfxPatch patch, bool native)
        {
            float[] mono = native ? Picture(patch, out int count) : null;
            if (mono == null)
            {
                wave.style.display = DisplayStyle.None;
                playKey.Off = !native;
                return;
            }

            wave.style.display = DisplayStyle.Flex;
            playKey.Off = false;
            wave.SetSamples(mono, mono.Length);
        }

        /// <summary>
        /// A patch as mono samples at PictureRate: the dry voice, ending where the sound has faded, 1.5 s at most –
        /// what the device draws (SfxPicture). Null: the engine is not there, or it rendered nothing.
        /// </summary>
        public static float[] Picture(SfxPatch patch, out int count)
        {
            var mono = new float[SfxPicture.Max];
            count = SfxPicture.Render(patch, mono);
            if (count <= 0)
            {
                return null;
            }

            Array.Resize(ref mono, count);
            return mono;
        }
    }
}
