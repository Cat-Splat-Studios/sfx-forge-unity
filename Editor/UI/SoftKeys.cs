// SPDX-License-Identifier: MIT
//
// The UI kit's keys: SoftKey is one of the display's soft keys, SoftKeyRow a row of them that wraps evenly,
// PageTabs the row of page names (OSC TONE FILTER ...).
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatSplat.SfxForge.Editor
{
    /// <summary>
    /// One of the display's soft keys (F1 – F6): a round-ended key with a word on it. Lit: it is on (green, as
    /// PLAY while the song runs). Off: there is nothing for it to do now (dim, as UNDO with nothing to undo) –
    /// it does not click.
    /// </summary>
    public class SoftKey : TextElement
    {
        bool lit;
        bool off;
        public event Action clicked;

        public SoftKey(string word, Action onClick = null, string tip = null)
        {
            AddToClassList("sfx-key");
            text = word;
            tooltip = tip;
            if (onClick != null)
            {
                clicked += onClick;
            }

            this.AddManipulator(
                new Clickable(() =>
                {
                    if (!off)
                    {
                        clicked?.Invoke();
                    }
                })
            );
        }

        public bool Lit
        {
            get => lit;
            set
            {
                lit = value;
                EnableInClassList("sfx-key--lit", value);
            }
        }

        public bool Off
        {
            get => off;
            set
            {
                off = value;
                EnableInClassList("sfx-key--off", value);
            }
        }
    }

    /// <summary>
    /// A row of soft keys. It wraps where the room ends, into rows of the same number of keys: six in a row when wide,
    /// three and three when narrow; four in a row, or two and two – never three and one.
    /// </summary>
    public class SoftKeyRow : VisualElement
    {
        float keyWidth = -1f;
        float laidOutFor = -1f;

        /// <summary>small: the second row under the six – smaller keys with longer words.</summary>
        public SoftKeyRow(bool small = false)
        {
            AddToClassList("sfx-keys");
            if (small)
            {
                AddToClassList("sfx-keys--small");
            }

            RegisterCallback<GeometryChangedEvent>(e => Balance(e.newRect.width));
        }

        /// <summary>
        /// How many keys stand in a row of this width, when n keys of at least keyWidth (and a gap) share it in rows
        /// of equal number.
        /// </summary>
        public static int PerRow(int n, float width, float keyWidth, float gap = 4f)
        {
            if (n <= 0)
            {
                return 1;
            }

            int fit = Mathf.Clamp((int)(width / (keyWidth + gap)), 1, n);
            int rows = (n + fit - 1) / fit;
            return (n + rows - 1) / rows;
        }

        // each key gets the least width that lets exactly PerRow of them into a
        // row (the stylesheet's own least width decides how many)
        void Balance(float width)
        {
            if (childCount == 0 || width <= 0f || Mathf.Approximately(width, laidOutFor))
            {
                return;
            }

            if (keyWidth < 0f)
            {
                keyWidth = Mathf.Max(20f, this[0].resolvedStyle.minWidth.value);
            }

            laidOutFor = width;
            int per = PerRow(childCount, width, keyWidth);
            float least = per >= childCount ? keyWidth : Mathf.Max(keyWidth, Mathf.Floor(width / per) - 5f);
            foreach (var key in Children())
            {
                key.style.minWidth = least;
            }
        }

        public SoftKey Add(string word, Action onClick, string tip = null)
        {
            var key = new SoftKey(word, onClick, tip);
            Add(key);
            return key;
        }

        /// <summary>
        /// A key's place with nothing in it, at the end of a row whose number of keys does not divide (7, 10): the rows
        /// are then of the same number all the same, and the last key is as wide as the others, not as
        /// wide as the page.
        /// </summary>
        public void AddBlank()
        {
            var blank = new VisualElement { pickingMode = PickingMode.Ignore };
            blank.AddToClassList("sfx-key");
            blank.AddToClassList("sfx-key--blank");
            Add(blank);
        }
    }

    /// <summary>
    /// The page tabs, as on the stage: OSC TONE FILTER … The one you are on has the accent colour. A tab can be
    /// unavailable (WAVE for a sound that is not a wavetable): dark, and it does not click.
    /// </summary>
    public class PageTabs : VisualElement
    {
        readonly TextElement[] tabs;
        readonly bool[] none;
        int index;
        Color accent = SfxColors.Ember;
        public event Action<int> changed;

        public PageTabs(params string[] names)
        {
            AddToClassList("sfx-tabs");
            tabs = new TextElement[names.Length];
            none = new bool[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                int at = i;
                var t = new TextElement { text = names[i] };
                t.AddToClassList("sfx-tab");
                t.AddManipulator(
                    new Clickable(() =>
                    {
                        if (!none[at] && at != index)
                        {
                            SetIndex(at);
                            changed?.Invoke(at);
                        }
                    })
                );
                tabs[i] = t;
                Add(t);
            }

            Paint();
        }

        public int Count => tabs.Length;
        public int Index => index;
        public Color Accent
        {
            get => accent;
            set
            {
                accent = value;
                Paint();
            }
        }

        /// <summary>Goes to a tab without telling anyone.</summary>
        public void SetIndex(int i)
        {
            index = Mathf.Clamp(i, 0, tabs.Length - 1);
            Paint();
        }

        public void SetAvailable(int i, bool available)
        {
            none[i] = !available;
            Paint();
        }

        public bool Available(int i) => !none[i];

        void Paint()
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                bool on = i == index;
                tabs[i].EnableInClassList("sfx-tab--on", on);
                tabs[i].EnableInClassList("sfx-tab--none", none[i] && !on);
                tabs[i].style.backgroundColor = on ? accent : new StyleColor(StyleKeyword.Null);
            }
        }
    }
}
