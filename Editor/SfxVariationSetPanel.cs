// SPDX-License-Identifier: MIT
using System;
using System.Globalization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatSplat.SfxForge.Editor
{
    /// <summary>
    /// A variation set as the device would show it: the top line, what state its clips are in, the preset it varies,
    /// its values as tiles (COUNT, AMOUNT, PITCH, VOLUME, NO REPEAT, SEED), which pages of values the variants keep,
    /// the variants themselves to listen to, and the keys - PLAY NEXT as the game would, BAKE, NEW SEED, SHOW CLIPS.
    /// Every edit goes through SerializedObject: Unity's Undo takes it back, several sets are edited together (values
    /// that differ read "-"). What SfxVariationSetEditor shows; made by SfxVariationSetEditor.Build.
    /// </summary>
    public sealed class SfxVariationSetPanel : VisualElement
    {
        public const int TileCount = 0;
        public const int TileAmount = 1;
        public const int TilePitch = 2;
        public const int TileVolume = 3;
        public const int TileNoRepeat = 4;
        public const int TileSeed = 5;
        public const int Tiles = 6;
        const int DefaultCount = 6;
        const int DefaultAmount = 10;
        const int DefaultNoRepeat = 2;
        const int DefaultSeed = 1;
        const int MaxNoRepeat = SfxVariationSet.MaxCount - 1;
        const float DefaultCents = 50f;
        const float DefaultDb = 2f;
        const float MaxCents = 1200f;
        const float MaxDb = 24f;

        /// <summary>What the state banner says in bold.</summary>
        public const string NoPreset = "NO PRESET";
        public const string NotBaked = "NOT BAKED";
        public const string OutOfDate = "OUT OF DATE";

        readonly SerializedObject so;
        readonly SerializedProperty presetProp;
        readonly SerializedProperty seedProp;
        readonly SerializedProperty countProp;
        readonly SerializedProperty amountProp;
        readonly SerializedProperty lockedProp;
        readonly SerializedProperty centsProp;
        readonly SerializedProperty dbProp;
        readonly SerializedProperty noRepeatProp;
        readonly SerializedProperty monoProp;
        readonly SfxHeader header = new SfxHeader();
        readonly SfxBanner newer = new SfxBanner();
        readonly SfxBanner state = new SfxBanner();
        readonly ObjectField presetField = new ObjectField
        {
            objectType = typeof(SfxPreset),
            allowSceneObjects = false,
        };
        readonly ValueTile[] tiles = new ValueTile[Tiles];
        readonly SoftKey[] pageKeys = new SoftKey[SfxUnits.PageCount];
        readonly VisualElement grid = SfxUI.Box("sfx-patches");
        readonly TextElement variantsCap;
        readonly TextElement keepCap;
        readonly VariantTile[] variantTiles = new VariantTile[SfxVariationSet.MaxCount];
        readonly VisualElement[] cells = new VisualElement[SfxVariationSet.MaxCount];
        readonly SfxPatch[] pictured = new SfxPatch[SfxVariationSet.MaxCount];
        readonly bool[] hasPicture = new bool[SfxVariationSet.MaxCount];
        readonly SoftKey playKey;
        readonly SoftKey bakeKey;
        readonly SoftKey seedKey;
        readonly SoftKey showKey;
        readonly SoftKey monoKey;
        readonly SfxFooter footer = new SfxFooter();
        readonly IVisualElementScheduledItem gridLater;

        int dragGroup = -1;
        int gridColumns = 2;
        int valueColumns;
        int shownStamp;
        int saidFor;
        bool gridNative;
        bool shownBaked;
        string undoName;
        string namedFor;
        string said;
        string saidSmall;
        Color saidColour;
        SfxShuffle order;
        int orderCount = -1;
        int orderNoRepeat = -1;

        public SfxVariationSetPanel(SerializedObject serializedObject)
        {
            so = serializedObject;
            presetProp = so.FindProperty("preset");
            seedProp = so.FindProperty("seed");
            countProp = so.FindProperty("count");
            amountProp = so.FindProperty("amount");
            lockedProp = so.FindProperty("locked");
            centsProp = so.FindProperty("pitchCents");
            dbProp = so.FindProperty("volumeDb");
            noRepeatProp = so.FindProperty("noRepeat");
            monoProp = so.FindProperty("mono");

            var root = SfxUI.Panel();
            Add(root);
            header.HideLink();
            header.NumberTile.tooltip = "How many variants the set has, the preset itself included.";
            root.Add(header);
            root.Add(newer);
            root.Add(state);

            var presetRow = SfxUI.Box("sfx-field-row");
            var presetLabel = SfxUI.Text("PRESET", "sfx-field-label");
            presetLabel.style.marginLeft = 0;
            presetField.AddToClassList("sfx-object");
            presetField.tooltip = "The sound this set varies. Variant 1 is this sound itself.";
            presetField.RegisterValueChangedCallback(e =>
                Apply(one => one.FindProperty("preset").objectReferenceValue = e.newValue)
            );
            presetRow.Add(presetLabel);
            presetRow.Add(presetField);
            root.Add(presetRow);

            var values = SfxUI.Box("sfx-song-values");
            string[] tips =
            {
                "How many variants, the preset itself included: 1 to 16. More variants leave the earlier ones as "
                    + "they are.",
                "How far a variant's values move from the preset's. 0%: every variant is the preset (pitch and "
                    + "volume still vary).",
                "A play is tuned up or down by up to this many cents (100 = a semitone).",
                "A play is quieter by up to this many dB.",
                "The last variants played are not chosen again.",
                "Which variants these are: the same preset, seed, amount and kept pages give the same variants "
                    + "again. Type one in hex.",
            };
            for (int i = 0; i < Tiles; i++)
            {
                int at = i;
                var t = new ValueTile(0, false, ValueTile.Look.Row);
                tiles[i] = t;
                t.tooltip = tips[i];
                t.StepPixels = i == TileCount || i == TileNoRepeat ? 8f : 3f;
                t.nudged += d => Nudge(at, d);
                t.typed += text => Typed(at, text);
                t.resetRequested += () => ResetValue(at);
                t.dragStarted += BeginDrag;
                t.dragEnded += EndDrag;
                values.Add(t);
            }

            values.RegisterCallback<GeometryChangedEvent>(e => SetValueColumns(e.newRect.width));
            root.Add(values);

            root.Add(SfxUI.Text("VARIANTS KEEP", "sfx-section"));
            var keep = new SoftKeyRow(true);
            for (int i = 0; i < SfxUnits.PageCount; i++)
            {
                int page = i;
                pageKeys[i] = keep.Add(
                    SfxUnits.PageNames[i],
                    () => TogglePage(page),
                    "Lit: the variants keep this page's values as the preset has them."
                );
            }

            // (seven keys: eight places make rows of four, or of three with FX as wide as the others)
            keep.AddBlank();
            root.Add(keep);
            keepCap = SfxUI.Text("", "sfx-song-cap");
            keepCap.style.whiteSpace = WhiteSpace.Normal;
            root.Add(keepCap);

            root.Add(SfxUI.Text("VARIANTS", "sfx-section"));
            variantsCap = SfxUI.Text("", "sfx-song-cap");
            root.Add(variantsCap);
            for (int i = 0; i < variantTiles.Length; i++)
            {
                int index = i;
                var tile = new VariantTile { ShowKeep = false };
                variantTiles[i] = tile;
                tile.PlayKey.tooltip = "Plays this variant here, with the device's engine: what BAKE writes.";
                tile.play += () => PlayVariant(index);
                cells[i] = SfxUI.Box("sfx-patch-cell");
                cells[i].Add(tile);
                grid.Add(cells[i]);
            }

            grid.RegisterCallback<GeometryChangedEvent>(e => SetGridColumns(e.newRect.width));
            root.Add(grid);

            var keys = new SoftKeyRow();
            keys.AddToClassList("sfx-keys--four");
            playKey = keys.Add(
                "PLAY NEXT",
                () => PlayNext(),
                "Plays as the game would: the next variant of the order, with its pitch and its volume."
            );
            bakeKey = keys.Add(
                "BAKE",
                () => Bake(),
                "Writes the variants as .wav files next to this asset, so the set plays on every platform. Bake "
                    + "again after the preset or the set changed. Not an undo step, and the set's earlier edits can no "
                    + "longer be undone after it."
            );
            seedKey = keys.Add("NEW SEED", NewSeed, "Other variants of the same preset.");
            showKey = keys.Add("SHOW CLIPS", ShowClips, "Shows the folder the baked clips are in.");
            root.Add(keys);
            var monoRow = SfxUI.Box("sfx-toggle-row");
            monoKey = new SoftKey(
                "MONO",
                ToggleMono,
                "Lit: the clips have one channel (left and right mixed). Half the memory; right for a sound that "
                    + "is placed in 3D."
            );
            monoRow.Add(monoKey);
            monoRow.Add(SfxUI.Text("one channel: for a sound placed in 3D", "sfx-song-cap"));
            root.Add(monoRow);
            root.Add(footer);

            gridLater = schedule.Execute(GridPicture).Every(10);
            gridLater.Pause();
            schedule.Execute(Poll).Every(400);
            RegisterCallback<AttachToPanelEvent>(_ => Undo.undoRedoPerformed += Refresh);
            RegisterCallback<DetachFromPanelEvent>(_ => Undo.undoRedoPerformed -= Refresh);
            this.TrackSerializedObjectValue(so, _ => Refresh());

            Refresh();
        }

        bool Alive
        {
            get
            {
                try
                {
                    return so != null && so.targetObject != null;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        SfxVariationSet First => so.targetObject as SfxVariationSet;
        int Many => so.targetObjects.Length;

        public ValueTile Tile(int i) => tiles[i];

        /// <summary>The notice for a set from a newer SFX Forge.</summary>
        public SfxBanner NewerBanner => newer;

        /// <summary>
        /// The notice of the clips' state: NO PRESET, NOT BAKED, OUT OF DATE - not shown while the clips are
        /// baked and current.
        /// </summary>
        public SfxBanner StateBanner => state;
        public ObjectField PresetField => presetField;

        /// <summary>The key with this word on it, null if there is none.</summary>
        public SoftKey Key(string word) => this.Query<SoftKey>().Where(k => k.text == word).First();

        /// <summary>The key of a page (SfxUnits.PageOsc ..): lit while the variants keep its values.</summary>
        public SoftKey PageKey(int page) => pageKeys[page];

        /// <summary>How many variant tiles show, and tile i of them (0: the preset itself).</summary>
        public int VariantCount
        {
            get
            {
                int n = 0;
                foreach (var cell in cells)
                {
                    if (cell.style.display != DisplayStyle.None)
                    {
                        n++;
                    }
                }

                return grid.style.display == DisplayStyle.None ? 0 : n;
            }
        }

        public VariantTile VariantTileAt(int i) => variantTiles[i];

        /// <summary>The line over the variants ("" while it is not shown).</summary>
        public string VariantsCaption => variantsCap.style.display == DisplayStyle.None ? "" : variantsCap.text;

        /// <summary>The line under VARIANTS KEEP: which pages and single values the variants keep.</summary>
        public string KeepCaption => keepCap.text;

        /// <summary>The two lines under the keys, "bold | small".</summary>
        public string FooterText => footer.Bold + " | " + footer.Small;

        /// <summary>The top line's number, name and sub text.</summary>
        public string HeaderNumber => ((TextElement)header.NumberTile).text;

        // ---- what is shown ------------------------------------------------------------------------------------------

        static string Words(int state) =>
            state == 1 ? NoPreset
            : state == 2 ? NotBaked
            : state == 3 ? OutOfDate
            : null;

        static int StateOf(SfxVariationSet set) =>
            set == null ? 0
            : set.preset == null ? 1
            : !set.IsBaked ? 2
            : set.IsStale ? 3
            : 0;

        /// <summary>Reads the set again and shows it.</summary>
        public void Refresh()
        {
            if (!Alive)
            {
                return;
            }

            so.Update();
            int many = Many;
            SfxVariationSet set = First;
            SfxPreset preset = presetProp.hasMultipleDifferentValues
                ? null
                : presetProp.objectReferenceValue as SfxPreset;
            bool native = SfxUI.NativeThere;
            Color accent = preset != null ? SfxColors.Engine(preset.patch.engine) : SfxColors.Dim;
            NameUndo(many);

            header.Set(
                countProp.hasMultipleDifferentValues ? "-" : set.Count.ToString(CultureInfo.InvariantCulture),
                many > 1 ? many + " SETS" : so.targetObject.name.ToUpperInvariant(),
                presetProp.hasMultipleDifferentValues ? "-"
                    : preset != null ? SfxUnits.EngineNames[(int)preset.patch.engine % SfxUnits.EngineNames.Length]
                    : NoPreset,
                accent
            );
            ShowNewer();

            // the state of the clips: of the one set, or of all of them when it is the same
            int what = StateOf(set);
            bool same = true;
            foreach (var target in so.targetObjects)
            {
                same &= StateOf(target as SfxVariationSet) == what;
            }

            if (!same)
            {
                state.Set(
                    "NOT ALL IN THE SAME STATE",
                    "Some of these " + many + " sets are baked, some are not or are out of date: select one to see."
                );
            }
            else if (what == 1)
            {
                state.Set(NoPreset, "Choose the sound this set varies.");
            }
            else if (what == 2)
            {
                state.Set(
                    NotBaked,
                    "It plays where the native library is (Windows x64). BAKE writes its clips so it plays everywhere."
                );
            }
            else if (what == 3)
            {
                state.Set(
                    OutOfDate,
                    "The preset or the set changed since BAKE: the clips are the old sound. BAKE again."
                );
            }
            else
            {
                state.Set(null);
            }

            presetField.showMixedValue = presetProp.hasMultipleDifferentValues;
            if (!presetProp.hasMultipleDifferentValues && presetField.value != presetProp.objectReferenceValue)
            {
                presetField.SetValueWithoutNotify(presetProp.objectReferenceValue);
            }

            Show(
                TileCount,
                "COUNT",
                countProp,
                set.Count.ToString(CultureInfo.InvariantCulture),
                set.Count / (float)SfxVariationSet.MaxCount,
                accent
            );
            Show(TileAmount, "AMOUNT", amountProp, set.Amount + "%", set.Amount / 100f, accent);
            Show(TilePitch, "PITCH", centsProp, SfxUnits.CentsText(set.pitchCents), set.pitchCents / MaxCents, accent);
            Show(
                TileVolume,
                "VOLUME",
                dbProp,
                set.volumeDb < 0.05f ? "OFF" : SfxUnits.DbText(-set.volumeDb),
                set.volumeDb / MaxDb,
                accent
            );
            Show(
                TileNoRepeat,
                "NO REPEAT",
                noRepeatProp,
                set.noRepeat <= 0 ? "OFF" : "LAST " + set.noRepeat,
                Mathf.Clamp01(set.noRepeat / (float)MaxNoRepeat),
                accent
            );
            Show(
                TileSeed,
                "SEED",
                seedProp,
                SfxUnits.SeedText(unchecked((uint)set.seed)),
                (unchecked((uint)set.seed) % 128u) / 127f,
                accent
            );

            // which values the variants keep: whole pages on the keys; single
            // values (MAKE SET brings the sound's locks) in words
            uint locked = unchecked((uint)set.locked) & SfxMutate.All;
            string pagesKept = "";
            string single = "";
            for (int page = 0; page < pageKeys.Length; page++)
            {
                uint mask = SfxUnits.PageMask(page);
                bool whole = mask != 0 && (locked & mask) == mask;
                pageKeys[page].Lit = !lockedProp.hasMultipleDifferentValues && whole;
                if (whole)
                {
                    pagesKept += (pagesKept.Length > 0 ? ", " : "") + SfxUnits.PageNames[page];
                    continue;
                }

                foreach (int k in SfxUnits.PageParams[page])
                {
                    if (k < 0 || (locked & (1u << k)) == 0)
                    {
                        continue;
                    }

                    single +=
                        (single.Length > 0 ? ", " : "")
                        + (
                            preset != null
                                ? SfxUnits.ParamName((SfxParam)k, preset.patch.engine)
                                : ((SfxParam)k).ToString().ToUpperInvariant()
                        );
                }
            }

            if (lockedProp.hasMultipleDifferentValues)
            {
                keepCap.text = "These sets keep different values.";
            }
            else if (locked == 0)
            {
                keepCap.text =
                    "Nothing is kept: every value of a variant may differ from the preset's. A lit page stays as the "
                    + "preset has it.";
            }
            else
            {
                keepCap.text =
                    "Kept as the preset has them: "
                    + (pagesKept.Length > 0 ? "the pages " + pagesKept : "")
                    + (pagesKept.Length > 0 && single.Length > 0 ? ", and " : "")
                    + (
                        single.Length > 0
                            ? "the single values " + single + " (a click on their page keeps all of it)"
                            : ""
                    )
                    + ".";
            }

            // the variants: of one set that has a preset
            bool showGrid = many == 1 && set.preset != null;
            grid.style.display = showGrid ? DisplayStyle.Flex : DisplayStyle.None;
            variantsCap.style.display = showGrid ? DisplayStyle.None : DisplayStyle.Flex;
            variantsCap.text = many > 1 ? many + " SETS: select one to see its variants" : "No preset, no variants.";
            if (showGrid)
            {
                int count = set.Count;
                bool pending = false;
                for (int i = 0; i < variantTiles.Length; i++)
                {
                    cells[i].style.display = i < count ? DisplayStyle.Flex : DisplayStyle.None;
                    if (i >= count)
                    {
                        hasPicture[i] = false;
                        continue;
                    }

                    SfxPatch variant = set.VariantPatch(i);
                    variantTiles[i].Set(i + 1, variant.engine, false, i == 0 ? "THE PRESET" : "VARIANT");
                    if (hasPicture[i] && gridNative == native && pictured[i].Equals(variant))
                    {
                        continue;
                    }

                    pictured[i] = variant;
                    if (!native)
                    {
                        variantTiles[i].SetPicture(variant, false);
                        hasPicture[i] = true;
                        continue;
                    }

                    hasPicture[i] = false;
                    variantTiles[i].ClearPicture(true);
                    pending = true;
                }

                gridNative = native;
                if (pending)
                {
                    gridLater.Resume();
                }
            }

            bool baked = true;
            bool presets = true;
            foreach (var target in so.targetObjects)
            {
                var one = target as SfxVariationSet;
                baked &= one != null && one.IsBaked;
                presets &= one != null && one.preset != null;
            }

            playKey.Off = !native || many != 1 || set.preset == null;
            bakeKey.Off = !native || !presets;
            showKey.Off = many != 1 || !set.IsBaked;
            monoKey.Lit = !monoProp.hasMultipleDifferentValues && set.mono;
            shownStamp = set.Stamp;
            shownBaked = set.IsBaked;

            // (what a key said is taken down once the set is no longer what it was then)
            if (said != null && saidFor != SetNow())
            {
                said = null;
            }

            ShowFooter(set, many, what, same, native);
        }

        // a number for what the selected sets are now: what their clips
        // are made from, how they play, whether they are baked
        int SetNow()
        {
            unchecked
            {
                int h = Many;
                foreach (var target in so.targetObjects)
                {
                    if (target is SfxVariationSet set)
                    {
                        h =
                            (
                                (((h * 31 + set.Stamp) * 31 + set.BakedCount) * 31 + set.pitchCents.GetHashCode()) * 31
                                + set.volumeDb.GetHashCode()
                            ) * 31
                            + set.noRepeat
                            + (set.IsStale ? 7 : 0);
                    }
                }

                return h;
            }
        }

        void Show(int tile, string label, SerializedProperty prop, string text, float bar, Color accent)
        {
            if (prop.hasMultipleDifferentValues)
            {
                tiles[tile].ShowMixed(label, accent);
            }
            else
            {
                tiles[tile].Show(label, text, bar, false, accent);
            }
        }

        void ShowFooter(SfxVariationSet set, int many, int what, bool same, bool native)
        {
            if (said != null)
            {
                footer.Set(said, saidColour, saidSmall);
                return;
            }

            if (!native)
            {
                footer.Set(
                    "NATIVE LIBRARY NOT FOUND",
                    SfxColors.Warn,
                    "sfxforge is here for Windows x64 only: no BAKE here. Clips baked elsewhere still play."
                );
                return;
            }

            if (many > 1)
            {
                footer.Set(
                    many + " SETS",
                    SfxColors.Text,
                    "BAKE bakes every one of them. Their values are edited together."
                );
                return;
            }

            if (what == 1)
            {
                footer.Set(
                    "NOTHING TO PLAY YET",
                    SfxColors.Dim,
                    "Drag a sound into PRESET above, or click the field to pick one."
                );
                return;
            }

            if (what == 2)
            {
                footer.Set(
                    NotBaked,
                    SfxColors.Dim,
                    "BAKE writes "
                        + set.Count
                        + " clips into "
                        + (
                            SfxVariationBaker.FolderOf(set)
                            ?? "a folder next to the asset, once it is saved inside Assets"
                        )
                        + "."
                );
                return;
            }

            long bytes = SfxVariationBaker.BakedBytes(set);
            string size =
                bytes >= 1024 * 1024
                    ? (bytes / (1024f * 1024f)).ToString("0.0", CultureInfo.InvariantCulture) + " MB"
                    : Mathf.Max(1, Mathf.RoundToInt(bytes / 1024f)) + " KB";
            AudioClip clip = set.Clip(0);
            string kind =
                clip != null
                    ? (clip.frequency / 1000f).ToString("0.#", CultureInfo.InvariantCulture)
                        + " kHz "
                        + (clip.channels == 1 ? "mono" : "stereo")
                    : "";
            string folder =
                clip != null
                    ? System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(clip)).Replace('\\', '/')
                    : "";
            int clips = set.BakedCount;
            footer.Set(
                clips + (clips == 1 ? " CLIP BAKED" : " CLIPS BAKED"),
                what == 3 ? SfxColors.Warn : SfxColors.Linked,
                folder
                    + " – "
                    + kind
                    + ", "
                    + size
                    + (
                        what != 3 ? ""
                        : clips != set.Count
                            ? " – the old sound, and "
                                + clips
                                + " variants where the set now has "
                                + set.Count
                                + ": BAKE again"
                        : " – the old sound: BAKE again"
                    )
            );
        }

        void ShowNewer()
        {
            int count = 0;
            int highest = 0;
            foreach (var target in so.targetObjects)
            {
                if (target is SfxVariationSet set && set.IsFromNewerVersion)
                {
                    count++;
                    highest = Mathf.Max(highest, set.Format);
                }
            }

            if (count == 0)
            {
                newer.Set(null);
                return;
            }

            int many = Many;
            string bold =
                many == 1
                    ? "FROM A NEWER SFX FORGE"
                    : (count == 1 ? "ONE OF THESE " + many + " SETS IS" : count + " OF THESE " + many + " SETS ARE")
                        + " FROM A NEWER SFX FORGE";
            newer.Set(bold, (many == 1 ? "This set was " : "Such a set was ") + SfxAssetFormat.NewerText(highest));
        }

        // the preset is another asset: it can change without this one changing
        void Poll()
        {
            if (!Alive || First == null)
            {
                return;
            }

            if (First.Stamp != shownStamp || First.IsBaked != shownBaked)
            {
                Refresh();
            }
        }

        // ---- editing: each selected set on its own, through its SerializedObject ------------------------------------
        void NameUndo(int many)
        {
            string what = many > 1 ? many + " SFX Variation Sets" : "SFX Variation Set " + so.targetObject.name;
            if (what == namedFor)
            {
                return;
            }

            namedFor = what;
            undoName = "Edit " + what;
        }

        void BeginDrag()
        {
            Undo.IncrementCurrentGroup();
            dragGroup = Undo.GetCurrentGroup();
        }

        void EndDrag() => dragGroup = -1;

        // true: something changed. One undo step - a whole drag is one too.
        bool Apply(Action<SerializedObject> change)
        {
            if (!Alive)
            {
                return false;
            }

            bool any = false;
            if (dragGroup < 0)
            {
                Undo.IncrementCurrentGroup();
            }

            foreach (var target in so.targetObjects)
            {
                if (target == null)
                {
                    continue;
                }

                using (var one = new SerializedObject(target))
                {
                    change(one);
                    any |= one.ApplyModifiedProperties();
                }
            }

            if (!any)
            {
                return false;
            }

            if (dragGroup >= 0)
            {
                Undo.CollapseUndoOperations(dragGroup);
            }

            Undo.SetCurrentGroupName(undoName);
            said = null;
            Refresh();
            return true;
        }

        static void Int(SerializedObject one, string field, Func<int, int> change, int low, int high)
        {
            var p = one.FindProperty(field);
            p.intValue = Mathf.Clamp(change(p.intValue), low, high);
        }

        static void Float(
            SerializedObject one,
            string field,
            Func<float, float> change,
            float low,
            float high,
            float grid
        )
        {
            var p = one.FindProperty(field);
            p.floatValue = Mathf.Clamp(Mathf.Round(change(p.floatValue) / grid) * grid, low, high);
        }

        /// <summary>
        /// Turns a tile (TileCount .. TileSeed) by whole steps: what a drag, the wheel and the arrow keys do.
        /// </summary>
        public void Nudge(int tile, int d)
        {
            if (d == 0)
            {
                return;
            }

            Apply(one =>
            {
                switch (tile)
                {
                    case TileCount:
                        Int(one, "count", v => v + d, 1, SfxVariationSet.MaxCount);
                        break;
                    case TileAmount:
                        Int(one, "amount", v => v + d, 0, 100);
                        break;
                    case TilePitch:
                        Float(one, "pitchCents", v => v + d * 5f, 0f, MaxCents, 5f);
                        break;
                    case TileVolume:
                        Float(one, "volumeDb", v => v + d * 0.5f, 0f, MaxDb, 0.5f);
                        break;
                    case TileNoRepeat:
                        Int(one, "noRepeat", v => v + d, 0, MaxNoRepeat);
                        break;
                    case TileSeed:
                    {
                        var p = one.FindProperty("seed");
                        p.intValue = unchecked(p.intValue + d);
                        break;
                    }
                }
            });
        }

        /// <summary>
        /// A typed value for a tile: "8", "25%", "50ct", "-3dB", "off", "last 3", a seed in hex. What is not understood
        /// changes nothing.
        /// </summary>
        public void Typed(int tile, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            string t = text.Trim().ToLowerInvariant();
            if (tile == TileSeed)
            {
                if (SfxUnits.TryParseSeed(t, out uint seed))
                {
                    Apply(one => one.FindProperty("seed").intValue = unchecked((int)seed));
                }

                return;
            }

            if (t.StartsWith("last"))
            {
                t = t.Substring(4).Trim();
            }

            if (t.StartsWith("+-"))
            {
                t = t.Substring(2);
            }

            if (t.StartsWith("±"))
            {
                t = t.Substring(1);
            }

            // ("off" is 0)
            if (!SfxUnits.Magnitude(t, out double v, out string unit))
            {
                return;
            }

            float value = Mathf.Abs((float)v);
            Apply(one =>
            {
                switch (tile)
                {
                    case TileCount:
                        if (unit.Length == 0)
                        {
                            Int(one, "count", _ => Mathf.RoundToInt(value), 1, SfxVariationSet.MaxCount);
                        }

                        break;
                    case TileAmount:
                        if (unit.Length == 0 || unit == "%")
                        {
                            Int(one, "amount", _ => Mathf.RoundToInt(value), 0, 100);
                        }

                        break;
                    case TilePitch:
                        if (unit.Length == 0 || unit == "ct" || unit.StartsWith("cent"))
                        {
                            Float(one, "pitchCents", _ => value, 0f, MaxCents, 1f);
                        }

                        break;
                    case TileVolume:
                        if (unit.Length == 0 || unit == "db")
                        {
                            Float(one, "volumeDb", _ => value, 0f, MaxDb, 0.1f);
                        }

                        break;
                    case TileNoRepeat:
                        if (unit.Length == 0)
                        {
                            Int(one, "noRepeat", _ => Mathf.RoundToInt(value), 0, MaxNoRepeat);
                        }

                        break;
                }
            });
        }

        /// <summary>A double-click on a tile's ring: the value of a new set.</summary>
        public void ResetValue(int tile)
        {
            Apply(one =>
            {
                switch (tile)
                {
                    case TileCount:
                        one.FindProperty("count").intValue = DefaultCount;
                        break;
                    case TileAmount:
                        one.FindProperty("amount").intValue = DefaultAmount;
                        break;
                    case TilePitch:
                        one.FindProperty("pitchCents").floatValue = DefaultCents;
                        break;
                    case TileVolume:
                        one.FindProperty("volumeDb").floatValue = DefaultDb;
                        break;
                    case TileNoRepeat:
                        one.FindProperty("noRepeat").intValue = DefaultNoRepeat;
                        break;
                    case TileSeed:
                        one.FindProperty("seed").intValue = DefaultSeed;
                        break;
                }
            });
        }

        /// <summary>
        /// A key of VARIANTS KEEP: the variants keep this page's values - or no longer, when they did.
        /// </summary>
        public void TogglePage(int page)
        {
            uint mask = SfxUnits.PageMask(page);
            if (mask == 0 || !Alive)
            {
                return;
            }

            bool on = !lockedProp.hasMultipleDifferentValues && (unchecked((uint)First.locked) & mask) == mask;
            Apply(one =>
            {
                var p = one.FindProperty("locked");
                uint now = unchecked((uint)p.intValue);
                p.intValue = unchecked((int)((on ? now & ~mask : now | mask) & SfxMutate.All));
            });
        }

        /// <summary>NEW SEED: other variants of the same preset. One undo step.</summary>
        public void NewSeed() =>
            Apply(one => one.FindProperty("seed").intValue = unchecked((int)SfxPresetPanel.Seeds.Next()));

        public void ToggleMono()
        {
            if (!Alive)
            {
                return;
            }

            bool on = !(!monoProp.hasMultipleDifferentValues && First.mono);
            Apply(one => one.FindProperty("mono").boolValue = on);
        }

        // ---- the keys -----------------------------------------------------------------------------------------------

        void PlayVariant(int index)
        {
            if (!Alive || !SfxUI.NativeThere || First == null || First.preset == null)
            {
                return;
            }

            SfxPatch patch = First.VariantPatch(index);
            SfxForgeEditorLink.Preview(patch, patch.IsSustained ? First.preset.holdSeconds : 0f);
        }

        /// <summary>
        /// PLAY NEXT: as the game would - the next variant of an order, with its pitch and volume. Returns the variant,
        /// -1: nothing played.
        /// </summary>
        public int PlayNext()
        {
            SfxVariationSet set = First;
            if (!Alive || Many != 1 || set == null || set.preset == null || !SfxUI.NativeThere)
            {
                return -1;
            }

            if (order == null || orderCount != set.Count || orderNoRepeat != set.noRepeat)
            {
                // (over the variants as they are now: what BAKE would write)
                order = new SfxShuffle(set.Count, set.noRepeat, SfxPresetPanel.Seeds.Next());
                orderCount = set.Count;
                orderNoRepeat = set.noRepeat;
            }

            int index = set.NextIndex(order, out float pitch, out float volume);
            SfxBaked baked = SfxBake.Render(
                set.VariantPatch(index),
                set.preset.holdSeconds,
                new SfxBakeOptions { mono = set.mono }
            );
            if (baked == null)
            {
                return -1;
            }

            SfxForgeEditorLink.PreviewSamples(baked.samples, baked.sampleRate, baked.channels, pitch, volume);
            float cents = 1200f * Mathf.Log(pitch, 2f);
            Say(
                "VARIANT " + (index + 1),
                SfxColors.Text,
                "pitch "
                    + (cents >= 0f ? "+" : "")
                    + Mathf.RoundToInt(cents)
                    + "ct, volume "
                    + SfxUnits.DbText(20f * Mathf.Log10(volume))
            );
            return index;
        }

        void Say(string bold, Color colour, string small)
        {
            said = bold;
            saidColour = colour;
            saidSmall = small;
            saidFor = SetNow();
            footer.Set(bold, colour, small);
        }

        /// <summary>
        /// BAKE: every selected set writes its clips (SfxVariationBaker.Bake). Not an undo step - and the edits made to
        /// the set before it can no longer be undone (an undo would take the clips out of the set while their files
        /// stay). The report goes to the Console, its gist under the keys.
        /// </summary>
        public SfxBakeReport Bake()
        {
            var all = new SfxBakeReport();
            if (!Alive)
            {
                return all;
            }

            foreach (var target in so.targetObjects)
            {
                if (!(target is SfxVariationSet set))
                {
                    continue;
                }

                SfxBakeReport one = SfxVariationBaker.Bake(set);
                all.lines.AddRange(one.lines);
                all.remarks.AddRange(one.remarks);
                all.Planned += one.Planned;
                all.WriteFailed |= one.WriteFailed;
                if (string.IsNullOrEmpty(all.Where))
                {
                    all.Where = one.Where;
                }
            }

            if (Many > 1)
            {
                all.Where = "";
            }

            all.ToConsole(so.targetObject);
            said = null;
            Refresh();
            if (all.Failed > 0)
            {
                string why = "";
                foreach (var line in all.lines)
                {
                    if (line.failed)
                    {
                        why = line.note;
                        break;
                    }
                }

                Say("NOT BAKED", SfxColors.Warn, why);
            }
            else if (all.remarks.Count > 0)
            {
                Say(all.Written + " CLIPS BAKED, WITH NOTES", SfxColors.Warn, all.remarks[0]);
            }
            else if (all.Warns)
            {
                Say(
                    all.Written + " CLIPS BAKED, WITH NOTES",
                    SfxColors.Warn,
                    "see the Console: a variant clips, is silent or was cut at 12 s"
                );
            }

            return all;
        }

        void ShowClips()
        {
            if (!Alive || First == null || !First.IsBaked)
            {
                return;
            }

            string file = AssetDatabase.GetAssetPath(First.Clip(0));
            if (string.IsNullOrEmpty(file))
            {
                return;
            }

            var folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                System.IO.Path.GetDirectoryName(file).Replace('\\', '/')
            );
            if (folder != null)
            {
                EditorGUIUtility.PingObject(folder);
            }
        }

        // ---- the variants' pictures: one a tick ---------------------------------------------------------------------

        void GridPicture()
        {
            for (int i = 0; i < variantTiles.Length; i++)
            {
                if (hasPicture[i] || cells[i].style.display == DisplayStyle.None)
                {
                    continue;
                }

                variantTiles[i].SetPicture(pictured[i], SfxUI.NativeThere);
                hasPicture[i] = true;
                return;
            }

            gridLater.Pause();
        }

        /// <summary>
        /// Draws the variants' pictures that are still to come, now (the panel does one every 10 ms by itself).
        /// </summary>
        public void FinishPictures()
        {
            if (grid.style.display == DisplayStyle.None)
            {
                return;
            }

            for (int i = 0; i < variantTiles.Length; i++)
            {
                if (hasPicture[i] || cells[i].style.display == DisplayStyle.None)
                {
                    continue;
                }

                variantTiles[i].SetPicture(pictured[i], SfxUI.NativeThere);
                hasPicture[i] = true;
            }
        }

        // six values: three and three when wide, two and two and two when narrow - never four and two
        void SetValueColumns(float width)
        {
            int n = width < 520f ? 2 : 3;
            if (n == valueColumns)
            {
                return;
            }

            valueColumns = n;
            foreach (var tile in tiles)
            {
                tile.style.minWidth = Length.Percent(n == 3 ? 31f : 46f);
            }
        }

        void SetGridColumns(float width)
        {
            int n = width < 560f ? 2 : 4;
            if (n == gridColumns)
            {
                return;
            }

            gridColumns = n;
            foreach (var cell in cells)
            {
                cell.style.width = Length.Percent(100f / n);
            }
        }
    }
}
