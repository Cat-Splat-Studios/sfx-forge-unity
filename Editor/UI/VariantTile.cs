// SPDX-License-Identifier: MIT
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatSplat.SfxForge.Editor
{
    /// <summary>
    /// A variant of a sound as a tile: its number on the engine's colour, a word beside it, the sound's shape, and two
    /// small keys - PLAY, and KEEP (lit while the sound is this variant). ShowKeep = false leaves PLAY alone: a tile
    /// that is only listened to (a variation set's). It is a PatchTile's look (the sfx-patch classes).
    /// </summary>
    public class VariantTile : VisualElement
    {
        readonly TextElement number;
        readonly TextElement word;
        readonly WaveformView wave;
        readonly VisualElement keys;
        readonly SoftKey playKey;
        readonly SoftKey keepKey;
        bool showKeep = true;
        public event Action play,
            keep;

        public VariantTile()
        {
            AddToClassList("sfx-patch");
            var top = SfxUI.Box("sfx-row");
            number = SfxUI.Text("", "sfx-patch__number");
            word = SfxUI.Text("", "sfx-patch__engine");
            top.Add(number);
            top.Add(word);
            wave = new WaveformView { Compact = true };
            keys = SfxUI.Box("sfx-patch__keys");
            playKey = new SoftKey(
                "PLAY",
                () => play?.Invoke(),
                "Plays this variant here, with the device's engine. The sound itself is not changed."
            );
            keepKey = new SoftKey("KEEP", () => keep?.Invoke(), "The sound becomes this variant. UNDO takes it back.");
            keys.Add(playKey);
            keys.Add(keepKey);
            Add(top);
            Add(wave);
            Add(keys);
        }

        public string NumberText => number.text;

        /// <summary>The word beside the number: the engine, unless Word was set.</summary>
        public string WordText => word.text;
        public SoftKey PlayKey => playKey;
        public SoftKey KeepKey => keepKey;
        public bool HasPicture => wave.HasPicture && wave.style.display != DisplayStyle.None;

        /// <summary>False: no KEEP key - a variant that is listened to, not taken.</summary>
        public bool ShowKeep
        {
            get => showKeep;
            set
            {
                showKeep = value;
                keepKey.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
                keys.EnableInClassList("sfx-variant__keys--one", !value);
            }
        }

        /// <summary>
        /// number: what stands on the colour (1, 2 ...). kept: the sound is this variant now (KEEP is lit).
        /// </summary>
        public void Set(int variantNumber, SfxEngine engine, bool kept, string wordText = null)
        {
            Color c = SfxColors.Engine(engine);
            number.text = variantNumber.ToString();
            number.style.backgroundColor = c;
            word.text = wordText ?? SfxUnits.EngineNames[(int)engine % SfxUnits.EngineNames.Length];
            word.style.color = c;
            wave.SetColour(c, true);
            keepKey.Lit = kept;
        }

        /// <summary>
        /// The variant's shape, from the device's engine. Without the native library: no picture (the tile is lower),
        /// and PLAY is off.
        /// </summary>
        public void SetPicture(SfxPatch patch, bool native)
        {
            float[] mono = native ? PatchTile.Picture(patch, out int count) : null;
            playKey.Off = !native;
            if (mono == null)
            {
                wave.style.display = DisplayStyle.None;
                return;
            }

            wave.style.display = DisplayStyle.Flex;
            wave.SetSamples(mono, mono.Length);
        }

        /// <summary>No picture yet (it is rendered a moment later): the room for it stays.</summary>
        public void ClearPicture(bool native)
        {
            playKey.Off = !native;
            wave.style.display = native ? DisplayStyle.Flex : DisplayStyle.None;
            if (native)
            {
                wave.SetSamples(Empty, 0);
            }
        }

        static readonly float[] Empty = new float[0];
    }
}
