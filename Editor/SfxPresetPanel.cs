// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatSplat.SfxForge.Editor
{
    /// <summary>
    /// A sound as the device shows it: the top line, the seven pages of four values (OSC TONE FILTER WAVE ENV LFO FX) around
    /// the picture of the sound, and the soft keys. Every edit goes through SerializedObject, so Unity's Undo takes it back
    /// and several selected sounds are edited together (values that differ read "-").
    /// Under the picture: how loud the sound is (peak and RMS, as PLAY and BAKE WAV render it). Under the keys, a drawer of
    /// four tools: NEW (a sound of a kind from nothing), VARY (MUTATE's amount and locks, eight variants to choose from),
    /// LEVEL (the selected sounds matched in loudness), SHARE (the sound as text and as a .syx file).
    /// What SfxPresetEditor shows in the Inspector; made by SfxPresetEditor.Build.
    /// </summary>
    public sealed class SfxPresetPanel : VisualElement
    {
        const int UndoEdit = 0, UndoMutate = 1, UndoRandom = 2, UndoGenerate = 3, UndoKeep = 4, UndoLevel = 5, UndoPaste = 6;
        static readonly string[] UndoWords = { "Edit", "Mutate", "Random", "Generate", "Keep", "Level", "Paste" };
        const float DefaultHold = 1.5f, MinHold = 0.05f;
        /// <summary>The longest hold: what a bake renders at most (SfxNative.Bake).</summary>
        public const float MaxHold = 12f;

        static readonly string[] Fields = BuildFields();
        static readonly System.Random Rng = new System.Random();
        static bool hearChanges = true, sendChanges;
        static int pad, toolTab;

        /// <summary>The tool drawer's tabs.</summary>
        public const int ToolNew = 0, ToolVary = 1, ToolLevel = 2, ToolShare = 3;
        /// <summary>How many variants MORE LIKE THIS shows.</summary>
        public const int GridCount = 8;
        /// <summary>The most sounds whose level is measured together.</summary>
        public const int MaxMeasured = 32;
        const int DefaultAmount = 15;
        const string AmountKey = "SfxForge.MutateAmount", LocksKey = "SfxForge.Locks", SyxFolderKey = "SfxForge.SyxFolder";

        /// <summary>
        /// Where the seeds of NEW, MUTATE and MORE LIKE THIS come from: started from the clock when the domain loads.
        /// A test sets it, and gets the same sounds.
        /// </summary>
        internal static SfxRandom Seeds = new SfxRandom(unchecked((uint)DateTime.Now.Ticks) ^ 0xA5F04C1Du);

        /// <summary>How far MUTATE and the variants move a value, in percent: 1..100, 15 as on the device. Kept for the session.</summary>
        public static int MutateAmount
        {
            get => Mathf.Clamp(SessionState.GetInt(AmountKey, DefaultAmount), 1, 100);
            set => SessionState.SetInt(AmountKey, Mathf.Clamp(value, 1, 100));
        }

        /// <summary>The values MUTATE and the variants leave alone: a bit for each SfxParam (SfxMutate.Bit). Kept for the session.</summary>
        public static uint Locked
        {
            get => unchecked((uint)SessionState.GetInt(LocksKey, 0)) & SfxMutate.All;
            set => SessionState.SetInt(LocksKey, unchecked((int)(value & SfxMutate.All)));
        }

        readonly SerializedObject so;
        readonly SerializedProperty engineProp, holdProp;
        readonly SerializedProperty[] props = new SerializedProperty[SfxPatch.ParamCount];
        readonly SfxHeader header = new SfxHeader();
        readonly SfxBanner newer = new SfxBanner();
        readonly TextElement title;
        readonly PageTabs tabs = new PageTabs(SfxUnits.PageNames);
        readonly ValueTile[] tiles = new ValueTile[4];
        readonly ValueTile holdTile = new ValueTile(0, false, ValueTile.Look.Row);
        readonly VisualElement holdRow;
        readonly SfxFooter holdNote = new SfxFooter(), linkNote = new SfxFooter(), levelNote = new SfxFooter();
        readonly WaveformView wave = new WaveformView();
        readonly SoftKey playKey, undoKey, bakeKey, sendKey, hearKey, deviceKey, sendChangesKey;
        readonly IVisualElementScheduledItem pictureLater, actLater, levelLater, gridLater;

        // the tool drawer
        readonly PageTabs toolTabs = new PageTabs("NEW", "VARY", "LEVEL", "SHARE");
        readonly VisualElement[] toolBoxes = new VisualElement[4];
        readonly SfxFooter newNote = new SfxFooter(), varyNote = new SfxFooter(), matchNote = new SfxFooter(), shareNote = new SfxFooter();
        readonly ValueTile amountTile = new ValueTile(0, false, ValueTile.Look.Row);
        readonly SoftKey[] generateKeys = new SoftKey[SfxGenerator.Count], lockKeys = new SoftKey[4];
        readonly SoftKey lockPageKey, unlockKey, moreKey, againKey, baseKey, makeSetKey, matchRmsKey, matchPeakKey, copyKey, pasteKey, exportKey, importKey;
        readonly VisualElement grid = SfxUI.Box("sfx-patches");
        readonly VariantTile[] variantTiles = new VariantTile[GridCount];
        readonly SfxPatch[] variantPictured = new SfxPatch[GridCount];
        readonly bool[] variantHasPicture = new bool[GridCount];
        readonly TextField shareField = new TextField { isDelayed = true };
        readonly Dictionary<LevelKey, Measured> levels = new Dictionary<LevelKey, Measured>();

        struct LevelKey : IEquatable<LevelKey>
        {
            public SfxPatch patch; public int hold;
            public bool Equals(LevelKey o) => hold == o.hold && patch.Equals(o.patch);
            public override bool Equals(object o) => o is LevelKey k && Equals(k);
            public override int GetHashCode() => patch.GetHashCode() * 31 + hold;
        }
        struct Measured { public SfxLevelReading level; public float seconds; }

        bool hasGrid, gridNative;
        int gridColumns = 2, newCount = -1;
        int newFor, matchFor, shareFor;                     // what the sounds were when a tool's note was written: it is taken down when they are no longer that
        int fitTries;
        IVisualElementScheduledItem fitLater;
        uint gridSeed, lastSeed;
        SfxPatch gridBase;
        SfxCategory lastCategory;
        SfxLevelReading level;
        string matchBold, matchSmall, shareBold, shareSmall, varyBold, varySmall;
        Color shareColour = SfxColors.Text, matchColour = SfxColors.Text, varyColour = SfxColors.Text;
        readonly string[] undoNames = new string[UndoWords.Length];
        readonly float[] mono = new float[SfxPicture.Max];

        int page, dragGroup = -1, monoCount;
        bool pictured, engineMixed;
        SfxPatch shown, picturedPatch;
        string lengthText = "", namedFor;

        static string[] BuildFields()
        {
            var f = new string[SfxPatch.ParamCount];
            for (int i = 0; i < f.Length; i++) { string n = ((SfxParam)i).ToString(); f[i] = char.ToLowerInvariant(n[0]) + n.Substring(1); }
            return f;
        }

        public SfxPresetPanel(SerializedObject serializedObject)
        {
            so = serializedObject;
            var patch = so.FindProperty("patch");
            engineProp = patch.FindPropertyRelative("engine");
            holdProp = so.FindProperty("holdSeconds");
            for (int i = 0; i < props.Length; i++) props[i] = patch.FindPropertyRelative(Fields[i]);

            var root = SfxUI.Panel();
            Add(root);

            header.NumberTile.tooltip = "The pad of the device SEND goes to. Click: another pad.";
            header.numberClicked += PickPad;
            root.Add(header);
            root.Add(newer);
            title = SfxUI.Text("", "sfx-title");
            root.Add(title);
            tabs.changed += i => { page = i; Refresh(); };
            root.Add(tabs);

            var scene = SfxUI.Box("sfx-scene");
            var above = SfxUI.Box("sfx-tile-row");
            var below = SfxUI.Box("sfx-tile-row");
            for (int i = 0; i < 4; i++)
            {
                int at = i;
                var t = tiles[i] = new ValueTile(i + 1, (i & 1) != 0);
                t.nudged += d => Nudge(at, d);
                t.typed += text => Typed(at, text);
                t.resetRequested += () => ResetValue(at);
                t.chosen += pick => Choose(at, pick);
                t.dragStarted += BeginDrag;
                t.dragEnded += EndDrag;
                (i < 2 ? above : below).Add(t);
            }
            scene.Add(above); scene.Add(wave); scene.Add(below);
            root.Add(scene);

            levelNote.AddToClassList("sfx-level");
            levelNote.tooltip = "How loud the sound is when PLAY plays it and BAKE WAV writes it, with its effects: its highest peak, and the RMS of its body. 0dB is full scale; over it a baked file is clamped. The length is the whole sound's, with the tail of its effects - the picture above shows the dry voice only, 1.5 s of it at most.";
            root.Add(levelNote);

            holdRow = SfxUI.Box("sfx-row");
            holdRow.style.marginTop = 6;
            holdTile.style.maxWidth = 150;
            holdTile.StepPixels = 3f;
            holdTile.tooltip = "For a sustained sound: how long a baked clip or a preview holds it before letting go.";
            holdTile.nudged += d => SetHold(h => h + d * 0.05f);
            holdTile.typed += TypedHold;
            holdTile.resetRequested += () => SetHold(_ => DefaultHold);
            holdTile.dragStarted += BeginDrag;
            holdTile.dragEnded += EndDrag;
            holdNote.style.flexGrow = 1; holdNote.style.flexShrink = 1; holdNote.style.flexBasis = 0; holdNote.style.marginLeft = 8;
            holdNote.Set("A HELD SOUND", SfxColors.Text, "a baked clip or a preview holds it this long, then lets go");
            holdRow.Add(holdTile); holdRow.Add(holdNote);
            root.Add(holdRow);

            var keys = new SoftKeyRow();
            playKey = keys.Add("PLAY", Play, "Plays the sound here, with the device's engine.");
            keys.Add("MUTATE", Mutate, "Every value except LEVEL, TABLE and the locked ones moves by up to the AMOUNT – a variation of this sound.");
            undoKey = keys.Add("UNDO", UndoOwn, "Takes back the last change made to this sound here – while it is the last thing done in Unity. Unity's own Undo (Ctrl+Z) does the same.");
            keys.Add("RANDOM", Randomize, "A new one-shot from nothing, inside ranges that stay audible. The engine changes too.");
            bakeKey = keys.Add("BAKE WAV", Bake, "Writes the sound as a .wav next to this asset.");
            sendKey = keys.Add("SEND", Send, "Sends the sound to the chosen pad of the device (the number at the top left).");
            root.Add(keys);
            var more = new SoftKeyRow(true);
            hearKey = more.Add("HEAR CHANGES", () => { hearChanges = !hearChanges; UpdateKeys(); }, "Play when a value changes.");
            deviceKey = more.Add("PLAY ON DEVICE", PlayOnDevice, "Sends the sound to the chosen pad and plays it there.");
            sendChangesKey = more.Add("SEND CHANGES", () => { sendChanges = !sendChanges; UpdateKeys(); }, "Send to that pad when a value changes.");
            root.Add(more);
            root.Add(linkNote);

            // ---- the tool drawer: four tabs, one box shown at a time ----
            root.Add(SfxUI.Text("TOOLS", "sfx-section"));
            toolTabs.changed += i => { toolTab = i; ShowTools(); };
            root.Add(toolTabs);
            for (int i = 0; i < toolBoxes.Length; i++) { toolBoxes[i] = SfxUI.Box("sfx-tools"); root.Add(toolBoxes[i]); }

            var kinds = new SoftKeyRow(true);
            for (int i = 0; i < SfxGenerator.Count; i++)
            {
                var category = (SfxCategory)i;
                generateKeys[i] = kinds.Add(SfxGenerator.Names[i], () => Generate(category), "A new " + SfxGenerator.Names[i] + " from nothing. Every click is another one.");
            }
            kinds.AddBlank(); kinds.AddBlank();             // (ten keys: twelve places make rows of six, four or three)
            toolBoxes[ToolNew].Add(kinds); toolBoxes[ToolNew].Add(newNote);

            var amountRow = SfxUI.Box("sfx-song-values");
            amountTile.StepPixels = 3f;
            amountTile.tooltip = "How far MUTATE and the variants move a value: 15% is the device's MUTATE. Drag, or double-click the value to type one; a double-click on the ring: 15%.";
            amountTile.nudged += d => SetAmount(MutateAmount + d);
            amountTile.typed += TypedAmount;
            amountTile.resetRequested += () => SetAmount(DefaultAmount);
            amountRow.Add(amountTile);
            var locks = new SoftKeyRow(true);
            for (int i = 0; i < 4; i++)
            {
                int at = i;
                lockKeys[i] = locks.Add("-", () => ToggleLock(at), "Lit: MUTATE and the variants leave this value as it is.");
            }
            lockPageKey = locks.Add("LOCK PAGE", TogglePageLock, "Locks the values of the page shown – or unlocks them when all are locked.");
            unlockKey = locks.Add("UNLOCK ALL", UnlockAll, "No value is locked any more, on any page.");
            var vary = new SoftKeyRow(true);
            moreKey = vary.Add("MORE LIKE THIS", MoreLikeThis, "Eight variants of the sound as it is now, to listen to and to choose from. The sound itself stays until you KEEP one.");
            againKey = vary.Add("AGAIN", Again, "Eight other variants of the same base.");
            baseKey = vary.Add("BACK TO BASE", BackToBase, "The sound becomes the base again: what it was when MORE LIKE THIS was pressed. UNDO takes it back.");
            makeSetKey = vary.Add("MAKE SET", () => MakeSet(), "Makes a variation set next to this sound, with this amount and these locks – and, with variants shown, the same eight. A new file: not an undo step.");
            for (int i = 0; i < GridCount; i++)
            {
                int variant = i + 1;
                var tile = variantTiles[i] = new VariantTile();
                tile.play += () => PlayVariant(variant);
                tile.keep += () => Keep(variant);
                var cell = SfxUI.Box("sfx-patch-cell");
                cell.Add(tile);
                grid.Add(cell);
            }
            grid.RegisterCallback<GeometryChangedEvent>(e => SetGridColumns(e.newRect.width));
            toolBoxes[ToolVary].Add(amountRow); toolBoxes[ToolVary].Add(locks); toolBoxes[ToolVary].Add(vary); toolBoxes[ToolVary].Add(grid); toolBoxes[ToolVary].Add(varyNote);

            var match = new SoftKeyRow(true);
            matchRmsKey = match.Add("MATCH RMS", () => MatchLevels(SfxLevelMeasure.Rms), "Writes LEVEL of every selected sound so that all have the same RMS: they sound about as loud as each other.");
            matchPeakKey = match.Add("MATCH PEAK", () => MatchLevels(SfxLevelMeasure.Peak), "Writes LEVEL of every selected sound so that all have the same peak.");
            toolBoxes[ToolLevel].Add(match); toolBoxes[ToolLevel].Add(matchNote);

            var textRow = SfxUI.Box("sfx-field-row");
            shareField.AddToClassList("sfx-field");
            shareField.tooltip = "The sound as text. Select it to copy it; type or paste another sound's text and press Enter to take that sound.";
            shareField.RegisterValueChangedCallback(e => { if (e.newValue != PatchText) PasteText(e.newValue); shareField.SetValueWithoutNotify(PatchText); });
            shareField.RegisterCallback<GeometryChangedEvent>(_ => FitShareText());     // (the whole text shows, however narrow the Inspector)
            textRow.Add(shareField);
            var share = new SoftKeyRow(true);
            copyKey = share.Add("COPY", CopyText, "Copies the sound's text to the clipboard.");
            pasteKey = share.Add("PASTE", () => PasteText(EditorGUIUtility.systemCopyBuffer), "The sound becomes the one whose text is on the clipboard. UNDO takes it back.");
            exportKey = share.Add("EXPORT .SYX", ExportSyxAs, "Writes the sound as a .syx file: the message that loads it into the chosen pad (the number at the top left). Any SysEx tool can send it to the device.");
            importKey = share.Add("IMPORT .SYX", ImportSyxFrom, "The sound becomes the patch in a .syx file: one this exports, or the device's EXPORT. UNDO takes it back.");
            toolBoxes[ToolShare].Add(textRow); toolBoxes[ToolShare].Add(share); toolBoxes[ToolShare].Add(shareNote);

            pictureLater = schedule.Execute(Picture); pictureLater.Pause();
            actLater = schedule.Execute(HearAndSend); actLater.Pause();
            levelLater = schedule.Execute(MeasureSome); levelLater.Pause();
            gridLater = schedule.Execute(GridPicture).Every(10); gridLater.Pause();
            fitLater = schedule.Execute(FitShareText); fitLater.Pause();
            schedule.Execute(UpdateLink).Every(400);
            RegisterCallback<AttachToPanelEvent>(_ => Undo.undoRedoPerformed += Refresh);
            RegisterCallback<DetachFromPanelEvent>(_ => Undo.undoRedoPerformed -= Refresh);
            this.TrackSerializedObjectValue(so, _ => Refresh());   // changed from elsewhere: a reset, another Inspector

            Refresh();
            Picture();                                      // the first picture at once
            if (so.targetObjects.Length == 1) MeasureNow(); // and one sound's level (several: a moment later)
        }

        /// <summary>The page shown: SfxUnits.PageOsc .. PageFx. WAVE only for a wavetable.</summary>
        public int Page
        {
            get => page;
            set { page = Mathf.Clamp(value, 0, SfxUnits.PageCount - 1); Refresh(); Picture(); }
        }

        public WaveformView Wave => wave;
        public ValueTile Tile(int i) => tiles[i];
        public ValueTile HoldTile => holdTile;
        /// <summary>The UNDO key is lit: the last thing done in Unity is a change made to this sound (these sounds) here.</summary>
        public bool CanUndoOwn => !undoKey.Off;
        /// <summary>What the picture's lower caption says: "0.42s long".</summary>
        public string LengthText => lengthText;

        bool Alive { get { try { return so != null && so.targetObject != null; } catch (Exception) { return false; } } }
        static bool NativeThere => SfxUI.NativeThere;
        SfxPreset First => so.targetObject as SfxPreset;
        float FirstHold => First != null ? First.holdSeconds : DefaultHold;
        /// <summary>The notice over the pages: shown for a sound from a newer SFX Forge.</summary>
        public SfxBanner NewerBanner => newer;
        /// <summary>The two lines under the keys: the link's state, or why SEND would be refused.</summary>
        public string LinkText => linkNote.Bold + " | " + linkNote.Small;

        // a sound (or one of several) that a newer SFX Forge made: say so, and what saving it here costs
        void ShowNewer()
        {
            int count = 0, highest = 0;
            foreach (var target in so.targetObjects)
                if (target is SfxPreset preset && preset.IsFromNewerVersion) { count++; highest = Mathf.Max(highest, preset.Format); }
            if (count == 0) { newer.Set(null); return; }
            int many = so.targetObjects.Length;
            string bold = many == 1 ? "FROM A NEWER SFX FORGE" : (count == 1 ? "ONE OF THESE " + many + " SOUNDS IS" : count + " OF THESE " + many + " SOUNDS ARE") + " FROM A NEWER SFX FORGE";
            newer.Set(bold, (many == 1 ? "This sound was " : "Such a sound was ") + SfxAssetFormat.NewerText(highest));
        }

        // ---- what is shown -----------------------------------------------------------------------------------------------
        /// <summary>Reads the sound again and shows it. Cheap: the picture follows a moment later.</summary>
        public void Refresh()
        {
            if (!Alive) return;
            so.Update();
            shown.engine = (SfxEngine)Mathf.Clamp(engineProp.intValue, 0, SfxPatch.EngineCount - 1);
            for (int i = 0; i < props.Length; i++) shown[(SfxParam)i] = (byte)Mathf.Clamp(props[i].intValue, 0, 127);
            engineMixed = engineProp.hasMultipleDifferentValues;
            bool wavetable = engineMixed || shown.engine == SfxEngine.Wavetable;
            Color accent = engineMixed ? SfxColors.Dim : SfxColors.Engine(shown.engine);
            int many = so.targetObjects.Length;
            uint locked = Locked;
            NameUndo(many);

            if (page == SfxUnits.PageWave && !wavetable) page = SfxUnits.PageOsc;
            header.Set((pad + 1).ToString(), many > 1 ? many + " SOUNDS" : so.targetObject.name.ToUpperInvariant(), engineMixed ? "-" : SfxUnits.EngineNames[(int)shown.engine], accent);
            title.text = SfxUnits.PageTitles[page];
            title.style.color = accent;
            tabs.Accent = accent;
            tabs.SetAvailable(SfxUnits.PageWave, wavetable);
            tabs.SetIndex(page);

            for (int i = 0; i < 4; i++)
            {
                int k = SfxUnits.PageParams[page][i];
                var t = tiles[i];
                t.Choices = null; t.ChoiceIndex = -1; t.StepPixels = 2f;
                t.Locked = k >= 0 && (locked & (1u << k)) != 0;
                if (k == SfxUnits.NoKnob) { t.ShowEmpty(); t.Interactive = false; continue; }
                t.Interactive = true;
                if (k == SfxUnits.EngineKnob)
                {
                    t.Choices = SfxUnits.EngineNames; t.ChoiceIndex = engineMixed ? -1 : (int)shown.engine; t.StepPixels = 16f;
                    if (engineMixed) t.ShowMixed("ENGINE", accent);
                    else t.Show("ENGINE", SfxUnits.EngineNames[(int)shown.engine], ((int)shown.engine + 1) / (float)SfxPatch.EngineCount, false, accent);
                    continue;
                }
                var p = (SfxParam)k;
                string name = SfxUnits.ParamName(p, shown.engine) + (t.Locked ? " *" : "");
                int v = shown[p];
                if (p == SfxParam.Table) { t.Choices = SfxUnits.TableNames; t.ChoiceIndex = v % SfxPatch.TableCount; t.StepPixels = 14f; }
                if (props[k].hasMultipleDifferentValues) { t.ChoiceIndex = -1; t.ShowMixed(name, accent); }
                else if (p == SfxParam.Table) t.Show(name, SfxUnits.ParamText(p, v), (v % SfxPatch.TableCount + 1) / (float)SfxPatch.TableCount, false, accent);
                else t.Show(name, SfxUnits.ParamText(p, v), v / 127f, SfxUnits.IsCentred(p), accent);
            }

            bool held = shown.IsSustained || props[(int)SfxParam.Sustain].hasMultipleDifferentValues;
            holdRow.style.display = held ? DisplayStyle.Flex : DisplayStyle.None;
            if (holdProp.hasMultipleDifferentValues) holdTile.ShowMixed("HOLD", accent);
            else holdTile.Show("HOLD", SfxUnits.HoldText(holdProp.floatValue), holdProp.floatValue / 4f, false, accent);

            wave.SetColour(accent, page == SfxUnits.PageOsc || page == SfxUnits.PageFx);
            ShowNewer();
            Line();
            ShowTools();
            UpdateKeys();
            UpdateLink();
            if (!pictured || !shown.Equals(picturedPatch)) pictureLater.ExecuteLater(40);
            if (!LevelIsCurrent()) levelLater.ExecuteLater(150);
            ShowLevel();
        }

        void UpdateKeys()
        {
            bool native = NativeThere, linked = SfxLink.Shared.HasOutput && SfxLink.Refusal(SfxLink.Shared.Device, false) == null;
            int many = so.targetObjects.Length;
            playKey.Off = !native; bakeKey.Off = !native;
            moreKey.Off = many > 1; makeSetKey.Off = many > 1;
            againKey.Off = many > 1 || !hasGrid; baseKey.Off = many > 1 || !hasGrid;
            matchRmsKey.Off = matchPeakKey.Off = many < 2 || many > MaxMeasured || !native;
            copyKey.Off = many > 1 && PatchesDiffer;
            exportKey.Off = many > 1;
            undoKey.Off = !OursOnTop;
            sendKey.Off = !linked; deviceKey.Off = !linked;
            hearKey.Lit = hearChanges;
            sendChangesKey.Lit = sendChanges;
        }

        void UpdateLink()
        {
            bool linked = SfxLink.Shared.HasOutput;
            string refusal = linked ? SfxLink.Refusal(SfxLink.Shared.Device, false) : null;     // (the device said it speaks another patch format)
            header.SetLink(linked, linked ? "Linked: " + SfxLink.Shared.OutputName : "Not connected");
            if (refusal != null) linkNote.Set("ANOTHER PATCH FORMAT", SfxColors.Warn, "SEND is off: " + refusal + ".");
            else if (linked) linkNote.Set("LINKED", SfxColors.Linked, "SEND goes to pad " + (pad + 1) + " of " + SfxLink.Shared.OutputName);
            else linkNote.Set("NOT CONNECTED", SfxColors.Dim, "Choose the MIDI ports in Window > SFX Forge.");
            sendKey.Off = !linked || refusal != null; deviceKey.Off = !linked || refusal != null;
            undoKey.Off = !OursOnTop;                       // (looked at again here: something else may have been done in Unity since)
        }

        // ---- the picture: the sound as the device draws it – its dry voice, 1.5 s at most (SfxPicture) -------------------
        void Picture()
        {
            if (!Alive) return;
            if (!NativeThere)
            {
                pictured = false;
                wave.SetMessage("NATIVE LIBRARY NOT FOUND", "sfxforge is here for Windows x64 only: no picture, no preview, no WAV. The link to the device still works.");
                return;
            }
            if (!pictured || !shown.Equals(picturedPatch))
            {
                monoCount = SfxPicture.Render(shown, mono);
                if (monoCount <= 0)
                {
                    pictured = false;
                    wave.SetMessage("NOTHING TO DRAW", "the engine did not render this sound");
                    return;
                }
                wave.SetSamples(mono, monoCount);
                lengthText = (monoCount / (float)SfxPicture.Rate).ToString("0.00", CultureInfo.InvariantCulture) + "s long";
                picturedPatch = shown; pictured = true;
            }
            Line();
        }

        // over the picture, in white: what this page's knobs shape (scene() in the firmware, in the picture's own measure)
        void Line()
        {
            if (!pictured || !NativeThere) return;
            var xy = wave.Overlay;
            const float px = 1f / 368f;
            string what = "the whole sound";
            int n = 0; bool dim = false;
            float letGo = -1f;
            if (page == SfxUnits.PageEnv)
            {
                float a = shown.attack / 127f, d = shown.ampDecay / 127f, su = shown.sustain / 127f, re = shown.release / 127f, seg = 0.24f;
                bool hold = shown.sustain > 0;
                float xa = 6f * px + a * seg, xd = xa + 10f * px + d * seg, xs = hold ? xd + 0.16f : xd, xr = hold ? xs + 10f * px + re * seg : xd, ys = hold ? 1f - su : 1f;
                xy[0] = new Vector2(0f, 1f); xy[1] = new Vector2(xa, 0f); xy[2] = new Vector2(xd, ys); xy[3] = new Vector2(xs, ys); xy[4] = new Vector2(xr, 1f);
                n = 5;
                if (hold) letGo = xs;                       // (the pad is let go here)
                what = hold ? "held, then released" : "a one-shot";
            }
            else if (page == SfxUnits.PageFilter)
            {
                float cx = 20f * px + shown.filterCutoff / 127f * (1f - 70f * px), level = 0.45f, peak = level - shown.filterRes / 127f * 0.42f;
                if (shown.engine == SfxEngine.Foley) { xy[0] = new Vector2(cx - 26f * px, 1f); xy[1] = new Vector2(cx, peak); xy[2] = new Vector2(cx + 44f * px, 1f); n = 3; what = "the noise's band"; }
                else { xy[0] = new Vector2(0f, level); xy[1] = new Vector2(Mathf.Max(0f, cx - 20f * px), level); xy[2] = new Vector2(cx, peak); xy[3] = new Vector2(cx + 44f * px, 1f); n = 4; what = "the filter"; }
            }
            else if (page == SfxUnits.PageLfo)
            {
                int depth = Mathf.Max(shown.lfoPitch, Mathf.Max(shown.lfoFilter, shown.lfoTimbre));
                float cycles = 1f + shown.lfoRate / 127f * 7f, amp = 0.5f * depth / 127f;
                n = 160;
                for (int i = 0; i < n; i++) { float x = i / (float)(n - 1); xy[i] = new Vector2(x, 0.5f - Mathf.Sin(x * cycles * 2f * Mathf.PI) * amp); }
                dim = depth == 0;
                what = depth > 0 ? "the slow wave" : "LFO: off";
            }
            else if (page == SfxUnits.PageTone || page == SfxUnits.PageWave)
            {
                int from = 48;                              // the wave itself: 10 ms, where the sound is loudest
                float peak = wave.Peak;
                for (int i = 48; i < monoCount - 240; i += 16) if (Mathf.Abs(mono[i]) > peak * 0.7f) { from = i; break; }
                n = Mathf.Min(240, monoCount - from);
                for (int i = 0; i < n; i++) xy[i] = new Vector2(i / 239f, 0.5f - 0.5f * mono[from + i] / peak);
                what = "its wave, 10 ms";
            }
            wave.SetOverlay(n, dim);
            wave.SetMark(letGo);
            wave.SetCaptions(what, lengthText);
        }

        // ---- editing: each selected sound on its own, through its SerializedObject ---------------------------------------
        static SfxPatch Read(SerializedProperty patch)
        {
            var p = new SfxPatch { engine = (SfxEngine)Mathf.Clamp(patch.FindPropertyRelative("engine").intValue, 0, SfxPatch.EngineCount - 1) };
            for (int i = 0; i < Fields.Length; i++) p[(SfxParam)i] = (byte)Mathf.Clamp(patch.FindPropertyRelative(Fields[i]).intValue, 0, 127);
            return p;
        }

        static void Write(SerializedProperty patch, SfxPatch from, SfxPatch to)
        {
            if (from.engine != to.engine) patch.FindPropertyRelative("engine").intValue = (int)to.engine;
            for (int i = 0; i < Fields.Length; i++) if (from[(SfxParam)i] != to[(SfxParam)i]) patch.FindPropertyRelative(Fields[i]).intValue = to[(SfxParam)i];
        }

        void BeginDrag()
        {
            Undo.IncrementCurrentGroup();
            dragGroup = Undo.GetCurrentGroup();
        }

        void EndDrag() => dragGroup = -1;

        // true: something changed. One undo step – a whole drag is one too.
        bool Apply(int undoKind, Action<SerializedObject> change)
        {
            if (!Alive) return false;
            bool any = false;
            if (dragGroup < 0) Undo.IncrementCurrentGroup();    // (a step of its own, whatever came before)
            foreach (var target in so.targetObjects)
            {
                if (target == null) continue;
                using (var one = new SerializedObject(target))
                {
                    change(one);
                    any |= one.ApplyModifiedProperties();
                }
            }
            if (!any) return false;
            if (dragGroup >= 0) Undo.CollapseUndoOperations(dragGroup);
            Undo.SetCurrentGroupName(undoNames[undoKind]);
            Refresh();
            actLater.ExecuteLater(120);
            return true;
        }

        bool Edit(int undoKind, Func<SfxPatch, SfxPatch> change) => Apply(undoKind, one =>
        {
            var patch = one.FindProperty("patch");
            SfxPatch before = Read(patch);
            Write(patch, before, change(before));
        });

        static int Wrap(int v, int d, int n) => ((v + d) % n + n) % n;

        /// <summary>Turns the knob of a tile (0-3) of the page by whole steps – what a drag, the wheel and the arrow keys do.</summary>
        public void Nudge(int tile, int d)
        {
            int k = SfxUnits.PageParams[page][tile];
            if (k == SfxUnits.NoKnob || d == 0) return;
            Edit(UndoEdit, p =>
            {
                if (k == SfxUnits.EngineKnob) p.engine = (SfxEngine)Wrap((int)p.engine, d, SfxPatch.EngineCount);
                else if (k == (int)SfxParam.Table) p.table = (byte)Wrap(p.table % SfxPatch.TableCount, d, SfxPatch.TableCount);
                else p[(SfxParam)k] = (byte)Mathf.Clamp(p[(SfxParam)k] + d, 0, 127);
                return p;
            });
        }

        /// <summary>A typed value for a tile, in the display's words: "200Hz", "1.2s", "40%", "+12", "OFF".</summary>
        public void Typed(int tile, string text)
        {
            int k = SfxUnits.PageParams[page][tile];
            if (k < 0 || !SfxUnits.TryParse((SfxParam)k, text, out int v)) return;
            Edit(UndoEdit, p => { p[(SfxParam)k] = (byte)v; return p; });
        }

        void Choose(int tile, int pick)
        {
            int k = SfxUnits.PageParams[page][tile];
            if (k == SfxUnits.EngineKnob) Edit(UndoEdit, p => { p.engine = (SfxEngine)Mathf.Clamp(pick, 0, SfxPatch.EngineCount - 1); return p; });
            else if (k >= 0) Edit(UndoEdit, p => { p[(SfxParam)k] = (byte)Mathf.Clamp(pick, 0, 127); return p; });
        }

        // a double-click on the dial: the value of a plain patch of that engine
        public void ResetValue(int tile)
        {
            int k = SfxUnits.PageParams[page][tile];
            if (k >= 0) Edit(UndoEdit, p => { p[(SfxParam)k] = SfxPatch.Create(p.engine)[(SfxParam)k]; return p; });
        }

        /// <summary>The hold of a sustained sound, in seconds: 0.05 .. 12 (MaxHold: no bake is longer).</summary>
        public void SetHold(Func<float, float> change) => Apply(UndoEdit, one =>
        {
            var hold = one.FindProperty("holdSeconds");
            hold.floatValue = Mathf.Clamp(Mathf.Round(change(hold.floatValue) * 100f) / 100f, MinHold, MaxHold);
        });

        /// <summary>A typed hold: "2", "2s", "2 sec" are seconds, "200ms" a fifth of one. What is not a time changes nothing.</summary>
        public void TypedHold(string text)
        {
            if (SfxUnits.TryParseSeconds(text, out float seconds)) SetHold(_ => seconds);
        }

        /// <summary>
        /// MUTATE: every value of every selected sound, except LEVEL, TABLE and the locked ones, moves by up to the AMOUNT
        /// (MutateAmount, Locked). With 15 % and nothing locked it moves values as the device's MUTATE does.
        /// </summary>
        public void Mutate() => Edit(UndoMutate, p =>
        {
            var rng = new SfxRandom(Seeds.Next());              // (a seed for each sound)
            p.Mutate(ref rng, SfxPatch.StepOf(MutateAmount), Locked);
            return p;
        });
        /// <summary>RANDOM: every selected sound becomes a new one-shot.</summary>
        public void Randomize() => Edit(UndoRandom, _ => RandomPatch(Rng));

        /// <summary>A new one-shot from nothing, inside ranges that stay audible (sfx_patch_random in core/sfx_core.h).</summary>
        public static SfxPatch RandomPatch(System.Random rng)
        {
            byte[] lo = { 30, 20, 20, 0, 0, 0, 50, 0, 30, 0, 0, 0, 20, 44, 20, 20, 0, 0, 0, 0, 0, 0, 0, 100, 0 };
            byte[] hi = { 100, 110, 100, 127, 127, 127, 127, 100, 105, 60, 12, 0, 60, 100, 90, 90, 20, 40, 50, 60, 40, 40, 50, 100, SfxPatch.TableCount - 1 };
            var p = new SfxPatch { engine = (SfxEngine)rng.Next(SfxPatch.EngineCount) };
            for (int i = 0; i < SfxPatch.ParamCount; i++) p[(SfxParam)i] = (byte)(lo[i] + rng.Next(hi[i] - lo[i] + 1));
            if (rng.Next(2) == 1) p.bitcrush = 0;
            if (rng.Next(2) == 1) p.lfoPitch = 0;
            return p;
        }

        // The names of this panel's undo steps carry what it shows ("Edit SFX Preset Jump", "Mutate 3 SFX Presets"): so the
        // UNDO key can ask Unity what its next undo is, and stays dark while that is anything else – a move in the scene,
        // an edit of another asset, a change made to another sound in another Inspector.
        void NameUndo(int many)
        {
            string what = many > 1 ? many + " SFX Presets" : "SFX Preset " + so.targetObject.name;
            if (what == namedFor) return;
            namedFor = what;
            for (int i = 0; i < undoNames.Length; i++) undoNames[i] = UndoWords[i] + " " + what;
        }

        bool OursOnTop
        {
            get
            {
                string top = Undo.GetCurrentGroupName();
                foreach (string ours in undoNames) if (top == ours) return true;
                return false;
            }
        }

        /// <summary>UNDO: Unity's Undo – only while its next step is a change made to this sound (these sounds) here.</summary>
        public void UndoOwn()
        {
            if (Alive && OursOnTop) Undo.PerformUndo();
        }

        // ---- the level line: how loud the sound is, as PLAY and BAKE WAV render it -------------------------------------
        /// <summary>The level of the sound shown (the first selected), as PLAY and BAKE WAV render it. Empty until it is measured, and without the native library.</summary>
        public SfxLevelReading Level => level;
        /// <summary>The level line's two texts, "bold | small".</summary>
        public string LevelText => levelNote.Bold.Replace(Nbsp, ' ') + " | " + levelNote.Small;

        // a reading and its name stay on one line: a narrow Inspector breaks the line between PEAK and RMS, not inside one
        const char Nbsp = '\u00A0';
        void SayLevel(string bold, Color colour, string small)
            => levelNote.Set(bold.Replace("PEAK ", "PEAK" + Nbsp).Replace("RMS ", "RMS" + Nbsp).Replace(" .. ", Nbsp + ".." + Nbsp).Replace(" SOUNDS", Nbsp + "SOUNDS"), colour, small);

        static LevelKey KeyOf(SfxPreset preset) => new LevelKey { patch = preset.patch, hold = Mathf.RoundToInt(preset.holdSeconds * 100f) };

        // a sound's reading: baked once for a patch and a hold, then kept (seconds below 0: the engine did not render it)
        Measured Measure(SfxPreset preset)
        {
            LevelKey key = KeyOf(preset);
            if (levels.TryGetValue(key, out Measured m)) return m;
            SfxBaked baked = SfxBake.Render(preset.patch, preset.holdSeconds);
            m = baked == null ? new Measured { seconds = -1f } : new Measured { level = baked.level, seconds = baked.Seconds };
            if (levels.Count >= 128) Forget();
            levels[key] = m;
            return m;
        }

        // the readings are full: those of the sounds selected now stay (they would only be measured again), the others go
        void Forget()
        {
            var keep = new Dictionary<LevelKey, Measured>();
            foreach (var target in so.targetObjects)
                if (target is SfxPreset preset && levels.TryGetValue(KeyOf(preset), out Measured m)) keep[KeyOf(preset)] = m;
            levels.Clear();
            if (keep.Count < 96) foreach (var pair in keep) levels[pair.Key] = pair.Value;
        }

        bool LevelIsCurrent()
        {
            if (!NativeThere || so.targetObjects.Length > MaxMeasured) return true;     // (nothing to measure)
            foreach (var target in so.targetObjects) if (target is SfxPreset preset && !levels.ContainsKey(KeyOf(preset))) return false;
            return true;
        }

        /// <summary>
        /// Measures the selected sounds now and shows it. The panel does this by itself a moment after a change - a few
        /// sounds at a time, so that many long sounds do not hold the Inspector up.
        /// </summary>
        public void MeasureNow()
        {
            if (!Alive) return;
            if (NativeThere && so.targetObjects.Length <= MaxMeasured)
                foreach (var target in so.targetObjects) if (target is SfxPreset preset) Measure(preset);
            ShowLevel();
        }

        // what the schedule does: the sounds not measured yet, for about a fortieth of a second - then the rest a moment later
        void MeasureSome()
        {
            if (!Alive) return;
            if (NativeThere && so.targetObjects.Length <= MaxMeasured)
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                foreach (var target in so.targetObjects)
                {
                    if (!(target is SfxPreset preset) || levels.ContainsKey(KeyOf(preset))) continue;
                    if (watch.ElapsedMilliseconds > 25) { levelLater.ExecuteLater(10); return; }
                    Measure(preset);
                }
            }
            ShowLevel();
        }

        static string Range(float low, float high)
        {
            string a = SfxUnits.DbNumber(low), b = SfxUnits.DbNumber(high);
            return a == b ? b + "dB" : a + " .. " + b + "dB";
        }

        void ShowLevel()
        {
            int many = so.targetObjects.Length;
            if (!NativeThere) { level = default; levelNote.Set("NO LEVEL", SfxColors.Dim, "the native library is not here"); return; }
            if (many > MaxMeasured) { level = default; levelNote.Set(many + " SOUNDS", SfxColors.Dim, "select " + MaxMeasured + " or fewer to measure them"); return; }
            if (!LevelIsCurrent())                                  // (measured a moment later: until then it says what it said)
            {
                if (string.IsNullOrEmpty(levelNote.Bold)) levelNote.Set(many > 1 ? many + " SOUNDS" : "LEVEL", SfxColors.Dim, "measuring…");
                return;
            }
            if (many == 1)
            {
                Measured m = First != null ? Measure(First) : new Measured { seconds = -1f };
                level = m.level;
                if (m.seconds < 0f) levelNote.Set("NO LEVEL", SfxColors.Dim, "the engine did not render this sound");
                else if (level.Silent) levelNote.Set("SILENT", SfxColors.Dim, "nothing above -120dB: LEVEL, CUTOFF or DECAY may be at 0");
                else if (level.Clips) SayLevel("CLIPS   PEAK " + SfxUnits.DbText(level.PeakDb) + "   RMS " + SfxUnits.DbText(level.RmsDb), SfxColors.Warn, "over full scale: a baked file is clamped. Lower LEVEL (FX page).");
                else SayLevel("PEAK " + SfxUnits.DbText(level.PeakDb) + "   RMS " + SfxUnits.DbText(level.RmsDb), SfxColors.Text,
                                   "as PLAY and BAKE WAV render it: " + m.seconds.ToString("0.00", CultureInfo.InvariantCulture) + "s with its effects' tail, velocity 100%");
                return;
            }
            float peakLow = 999f, peakHigh = -999f, rmsLow = 999f, rmsHigh = -999f;
            int silent = 0, heard = 0; bool clips = false, first = true;
            foreach (var target in so.targetObjects)
            {
                if (!(target is SfxPreset preset)) continue;
                Measured m = Measure(preset);
                if (first) { level = m.level; first = false; }
                if (m.seconds < 0f || m.level.Silent) { silent++; continue; }
                heard++; clips |= m.level.Clips;
                peakLow = Mathf.Min(peakLow, m.level.PeakDb); peakHigh = Mathf.Max(peakHigh, m.level.PeakDb);
                rmsLow = Mathf.Min(rmsLow, m.level.RmsDb); rmsHigh = Mathf.Max(rmsHigh, m.level.RmsDb);
            }
            if (heard == 0) { levelNote.Set(many + " SOUNDS   SILENT", SfxColors.Dim, "nothing above -120dB in any of them"); return; }
            string bold = (clips ? "CLIPS   " : "") + many + " SOUNDS   PEAK " + Range(peakLow, peakHigh) + "   RMS " + Range(rmsLow, rmsHigh);
            string small = clips ? "one is over full scale: its baked file is clamped. Lower its LEVEL (FX page), or MATCH PEAK (LEVEL tab)." : "as PLAY and BAKE WAV render them, the quietest to the loudest";
            if (silent > 0) small += " – " + silent + (silent == 1 ? " is silent" : " are silent");
            SayLevel(bold, clips ? SfxColors.Warn : SfxColors.Text, small);
        }

        // ---- the tool drawer ---------------------------------------------------------------------------------------------
        /// <summary>The tool shown: ToolNew, ToolVary, ToolLevel, ToolShare. One for all Inspectors, as HEAR CHANGES is.</summary>
        public int ToolTab
        {
            get => toolTab;
            set { toolTab = Mathf.Clamp(value, 0, toolBoxes.Length - 1); ShowTools(); }
        }
        public PageTabs ToolTabs => toolTabs;
        /// <summary>A tool's box is the one shown.</summary>
        public bool ToolShown(int tab) => toolBoxes[tab].style.display != DisplayStyle.None;
        /// <summary>The key with this word on it, null if there is none.</summary>
        public SoftKey Key(string word) => this.Query<SoftKey>().Where(k => k.text == word).First();
        /// <summary>The four lock keys of the VARY tab, one for each value of the page.</summary>
        public SoftKey LockKey(int tile) => lockKeys[tile];
        public ValueTile AmountTile => amountTile;
        /// <summary>The tile of variant 1..GridCount.</summary>
        public VariantTile VariantTileAt(int variant) => variantTiles[variant - 1];
        public bool GridShown => grid.style.display != DisplayStyle.None;
        public TextField ShareField => shareField;
        /// <summary>The notes of the four tools, "bold | small".</summary>
        public string NewText => newNote.Bold + " | " + newNote.Small;
        public string VaryText => varyNote.Bold + " | " + varyNote.Small;
        public string MatchText => matchNote.Bold + " | " + matchNote.Small;
        public string ShareText => shareNote.Bold + " | " + shareNote.Small;

        bool PatchesDiffer
        {
            get
            {
                if (engineProp.hasMultipleDifferentValues) return true;
                foreach (var prop in props) if (prop.hasMultipleDifferentValues) return true;
                return false;
            }
        }

        static int Bits(uint v) { int n = 0; while (v != 0) { n += (int)(v & 1); v >>= 1; } return n; }

        // a number for what the selected sounds are now: their patches, and which sounds they are
        int SoundsNow()
        {
            unchecked
            {
                int h = so.targetObjects.Length;
                foreach (var target in so.targetObjects)
                    if (target is SfxPreset preset) h = (h * 31 + preset.patch.GetHashCode()) * 31 + preset.GetInstanceID();
                return h;
            }
        }

        void ShowTools()
        {
            int many = so.targetObjects.Length;
            bool native = NativeThere;
            // a note says what a key just did: once the sounds are no longer what it left (an edit, an undo), it is taken down
            int sounds = SoundsNow();
            if (newCount >= 0 && sounds != newFor) newCount = -1;
            if (matchBold != null && sounds != matchFor) matchBold = null;
            if (shareBold != null && sounds != shareFor) shareBold = null;
            Color accent = engineMixed ? SfxColors.Dim : SfxColors.Engine(shown.engine);
            toolTabs.Accent = accent;
            toolTabs.SetIndex(toolTab);
            for (int i = 0; i < toolBoxes.Length; i++) toolBoxes[i].style.display = i == toolTab ? DisplayStyle.Flex : DisplayStyle.None;

            // NEW
            if (newCount < 0) newNote.Set("A NEW SOUND OF A KIND", SfxColors.Text, "Replaces this sound. UNDO takes it back.");
            else
            {
                string seed = SfxUnits.SeedText(lastSeed), call = "SfxGenerator.Make(SfxCategory." + lastCategory + ", 0x" + seed + ")";
                newNote.Set(SfxGenerator.Names[(int)lastCategory], SfxColors.Text,
                            newCount > 1 ? "seed " + seed + " and " + (newCount - 1) + " more – " + call + " gives the first again" : "seed " + seed + " – " + call + " gives it again");
            }

            // VARY
            int amount = MutateAmount;
            uint locked = Locked, mask = SfxUnits.PageMask(page);
            amountTile.Show("AMOUNT", amount.ToString(CultureInfo.InvariantCulture) + "%", amount / 100f, false, accent);
            for (int i = 0; i < 4; i++)
            {
                int k = SfxUnits.PageParams[page][i];
                var key = lockKeys[i];
                if (k < 0)                                      // (no value here - or the engine, which MUTATE never changes)
                {
                    key.text = k == SfxUnits.EngineKnob ? "ENGINE STAYS" : "-"; key.Off = true; key.Lit = false;
                    key.tooltip = k == SfxUnits.EngineKnob ? "MUTATE and the variants never change the engine: there is nothing to lock." : "";
                    continue;
                }
                key.tooltip = "Lit: MUTATE and the variants leave this value as it is.";
                key.text = "LOCK " + SfxUnits.ParamName((SfxParam)k, shown.engine);
                key.Off = false;
                key.Lit = (locked & (1u << k)) != 0;
            }
            lockPageKey.Lit = mask != 0 && (locked & mask) == mask;
            unlockKey.Off = locked == 0;
            bool showGrid = hasGrid && many == 1;
            grid.style.display = showGrid ? DisplayStyle.Flex : DisplayStyle.None;
            if (showGrid)
            {
                bool pending = false;
                for (int i = 0; i < GridCount; i++)
                {
                    SfxPatch variant = GridVariant(i + 1);
                    variantTiles[i].Set(i + 1, variant.engine, shown.Equals(variant), Moved(gridBase, variant));
                    if (variantHasPicture[i] && gridNative == native && variantPictured[i].Equals(variant)) continue;
                    variantPictured[i] = variant;
                    if (!native) { variantTiles[i].SetPicture(variant, false); variantHasPicture[i] = true; continue; }
                    variantHasPicture[i] = false;                   // (drawn a moment later, one a tick)
                    variantTiles[i].ClearPicture(true);
                    pending = true;
                }
                gridNative = native;
                if (pending) gridLater.Resume();
            }
            if (many > 1) varyNote.Set("MORE LIKE THIS WORKS ON ONE SOUND", SfxColors.Text, many + " are selected. MUTATE changes all of them.");
            else if (varyBold != null) varyNote.Set(varyBold, varyColour, varySmall);
            else if (hasGrid)
            {
                int held = Bits(locked);
                varyNote.Set(GridCount + " VARIANTS OF " + so.targetObject.name.ToUpperInvariant(), SfxColors.Text,
                             "amount " + amount + "%, " + (held == 0 ? "no value locked" : held == 1 ? "1 value locked" : held + " values locked") + ", seed " + SfxUnits.SeedText(gridSeed) + " – all from the same base");
            }
            else varyNote.Set("VARIANTS OF THIS SOUND", SfxColors.Text, "MORE LIKE THIS shows eight to choose from. AMOUNT and the locks steer MUTATE too.");

            // LEVEL
            if (matchBold != null) matchNote.Set(matchBold, matchColour, matchSmall);
            else matchNote.Set("MATCH LEVEL ACROSS THE SELECTION", SfxColors.Text, "Writes LEVEL (FX page) so the selected sounds read the same. Nothing else changes.");

            // SHARE
            string text = PatchText;
            if (shareField.value != text) shareField.SetValueWithoutNotify(text);
            if (toolTab == ToolShare) FitShareText();
            if (shareBold != null) shareNote.Set(shareBold, shareColour, shareSmall);
            else if (many > 1 && PatchesDiffer) shareNote.Set("THESE SOUNDS DIFFER", SfxColors.Text, "The text is the first one's. A paste or an import writes the same sound to all " + many + ".");
            else shareNote.Set("THIS SOUND AS TEXT", SfxColors.Text, SfxPatchText.Length + " characters: paste it in a chat, a commit, a comment.");
        }

        // a variant's caption: how it differs from the base - the value that moved most, and by how much
        static string Moved(SfxPatch from, SfxPatch to)
        {
            int moved = 0, most = -1, far = 0;
            for (int i = 0; i < SfxPatch.ParamCount; i++)
            {
                int d = to[(SfxParam)i] - from[(SfxParam)i];
                if (d == 0) continue;
                moved++;
                if (Mathf.Abs(d) > Mathf.Abs(far)) { far = d; most = i; }
            }
            if (moved == 0) return "THE BASE";
            return SfxUnits.ParamName((SfxParam)most, to.engine) + " " + (far > 0 ? "+" : "") + far.ToString(CultureInfo.InvariantCulture);
        }

        // the sound's text, whole: the letters are made smaller until its 62 fit the field (never under 6 px), and larger
        // again when there is room. Text does not grow evenly with its size, so it looks again a moment after a change.
        void FitShareText()
        {
            TextElement letters = shareField.Q<TextElement>();
            if (letters == null || letters.panel == null) return;
            float room = letters.contentRect.width;
            if (float.IsNaN(room) || room < 20f) return;
            const float Largest = 11f, Smallest = 6f;
            float now = letters.resolvedStyle.fontSize;
            if (now <= 0f) return;
            float wide = letters.MeasureTextSize(shareField.value, 0, MeasureMode.Undefined, 0, MeasureMode.Undefined).x;
            if (wide <= 0f) return;
            float fits = Mathf.Floor(now * (room - 2f) / wide * 2f) / 2f, want = now;      // (in half pixels, a little room to spare)
            if (wide > room - 1f) want = Mathf.Min(now - 0.5f, fits);
            else if (fits >= now + 1f) want = fits;
            want = Mathf.Clamp(want, Smallest, Largest);
            if (Mathf.Abs(want - now) < 0.01f) { fitTries = 0; return; }
            letters.style.fontSize = want;
            if (fitTries++ < 8) fitLater.ExecuteLater(1);
        }

        /// <summary>The size of the letters of the SHARE field's text, in pixels: smaller in a narrow Inspector, so that the whole text shows.</summary>
        public float ShareFontSize { get { TextElement letters = shareField.Q<TextElement>(); return letters != null ? letters.resolvedStyle.fontSize : 0f; } }

        // ---- NEW: a sound of a kind, from nothing ------------------------------------------------------------------------
        /// <summary>The seed of the sound NEW made last (the first one's, with several selected). SfxGenerator.Make(category, seed) gives it again.</summary>
        public uint LastSeed => lastSeed;

        /// <summary>A key of the NEW tab: every selected sound becomes a new one of this kind, each from its own seed. One undo step.</summary>
        public void Generate(SfxCategory category)
        {
            if (!Alive) return;
            uint first = 0; int made = 0;
            Edit(UndoGenerate, _ =>
            {
                uint seed = Seeds.Next();
                if (made++ == 0) first = seed;
                return SfxGenerator.Make(category, seed);
            });
            lastSeed = first; lastCategory = category; newCount = made; newFor = SoundsNow();
            ShowTools();
        }

        // ---- VARY: MUTATE's amount and locks, and eight variants of one base ---------------------------------------------
        void Vary()                                                 // (after any change on the VARY tab: its note says the state again)
        {
            varyBold = null;
            Refresh();
        }

        /// <summary>AMOUNT: 1..100 percent.</summary>
        public void SetAmount(int percent)
        {
            MutateAmount = percent;
            Vary();
        }

        /// <summary>A typed amount: "20", "20%". Anything else changes nothing.</summary>
        public void TypedAmount(string text)
        {
            if (SfxUnits.Magnitude(text, out double v, out string unit) && (unit.Length == 0 || unit == "%")) SetAmount((int)Math.Round(v));
        }

        /// <summary>Locks or unlocks the value of a tile (0-3) of the page shown.</summary>
        public void ToggleLock(int tile)
        {
            int k = SfxUnits.PageParams[page][tile];
            if (k < 0) return;
            Locked ^= 1u << k;
            Vary();
        }

        /// <summary>Locks every value of the page shown - or unlocks them when all are locked.</summary>
        public void TogglePageLock()
        {
            uint mask = SfxUnits.PageMask(page), locked = Locked;
            Locked = (locked & mask) == mask ? locked & ~mask : locked | mask;
            Vary();
        }

        public void UnlockAll()
        {
            Locked = 0;
            Vary();
        }

        /// <summary>The base of the variants shown: the sound as it was when MORE LIKE THIS was pressed.</summary>
        public SfxPatch GridBase => gridBase;
        public uint GridSeed => gridSeed;
        public bool HasGrid => hasGrid;
        /// <summary>Variant 1..GridCount of the base (0: the base): SfxMutate.Variant with the seed, the amount and the locks as they are now.</summary>
        public SfxPatch GridVariant(int variant) => SfxMutate.Variant(gridBase, gridSeed, variant, MutateAmount, Locked);

        /// <summary>MORE LIKE THIS: the sound as it is now becomes the base, and eight variants of it are shown. One sound only.</summary>
        public void MoreLikeThis()
        {
            if (!Alive || so.targetObjects.Length != 1) return;
            Refresh();
            gridBase = shown; gridSeed = Seeds.Next(); hasGrid = true;
            Vary();
        }

        /// <summary>AGAIN: eight other variants of the same base.</summary>
        public void Again()
        {
            if (!Alive || !hasGrid || so.targetObjects.Length != 1) return;
            gridSeed = Seeds.Next();
            Vary();
        }

        /// <summary>KEEP on a tile: the sound becomes variant 1..GridCount. The base stays the base.</summary>
        public void Keep(int variant)
        {
            if (!Alive || !hasGrid || so.targetObjects.Length != 1 || variant < 1 || variant > GridCount) return;
            SfxPatch kept = GridVariant(variant);
            varyBold = null;
            Edit(UndoKeep, _ => kept);
        }

        /// <summary>BASE: the sound becomes the base again.</summary>
        public void BackToBase()
        {
            if (!Alive || !hasGrid || so.targetObjects.Length != 1) return;
            SfxPatch back = gridBase;
            varyBold = null;
            Edit(UndoKeep, _ => back);
        }

        void PlayVariant(int variant)
        {
            if (!Alive || !hasGrid || !NativeThere) return;
            SfxPatch patch = GridVariant(variant);
            SfxForgeEditorLink.Preview(patch, patch.IsSustained ? FirstHold : 0f);
        }

        // one picture a tick, so eight of them do not hold the Inspector up
        void GridPicture()
        {
            for (int i = 0; i < GridCount; i++)
            {
                if (variantHasPicture[i]) continue;
                variantTiles[i].SetPicture(variantPictured[i], NativeThere);
                variantHasPicture[i] = true;
                return;
            }
            gridLater.Pause();
        }

        /// <summary>Draws the variants' pictures that are still to come, now (the panel does one every 10 ms by itself).</summary>
        public void FinishPictures()
        {
            if (!hasGrid) return;
            for (int i = 0; i < GridCount; i++)
            {
                if (variantHasPicture[i]) continue;
                variantTiles[i].SetPicture(variantPictured[i], NativeThere);
                variantHasPicture[i] = true;
            }
        }

        void SetGridColumns(float width)
        {
            int n = width < 560f ? 2 : 4;
            if (n == gridColumns) return;
            gridColumns = n;
            foreach (var cell in grid.Children()) cell.style.width = Length.Percent(100f / n);
        }

        /// <summary>
        /// MAKE SET: a variation set next to this sound, with this amount and these locks. With variants shown it has their
        /// seed and nine variants - the sound and the eight tiles, tile n being the set's variant n while the sound is the
        /// base. Not baked. It is a new file, not an undo step (as with Unity's own Create menu): delete it in the Project
        /// window if it is not wanted. Null: several sounds, or the sound is no asset inside Assets.
        /// </summary>
        public SfxVariationSet MakeSet()
        {
            if (!Alive || so.targetObjects.Length != 1 || First == null) return null;
            string path = AssetDatabase.GetAssetPath(First);
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/"))
            {
                varyBold = "NO SET MADE"; varyColour = SfxColors.Warn; varySmall = "This sound is not an asset inside Assets: a set is saved next to its sound.";
                ShowTools();
                return null;
            }
            var set = ScriptableObject.CreateInstance<SfxVariationSet>();
            set.preset = First;
            set.amount = MutateAmount;
            set.locked = unchecked((int)Locked);
            set.seed = unchecked((int)(hasGrid ? gridSeed : Seeds.Next()));
            set.count = hasGrid ? GridCount + 1 : 6;
            string folder = Path.GetDirectoryName(path).Replace('\\', '/');
            string at = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + First.name + " Variations.asset");
            AssetDatabase.CreateAsset(set, at);                 // (no undo step: undoing the making of an asset leaves its file behind, empty)
            AssetDatabase.SaveAssetIfDirty(set);
            EditorGUIUtility.PingObject(set);
            varyBold = "SET MADE"; varyColour = SfxColors.Linked; varySmall = at + " – " + set.count + " variants, not baked yet: BAKE in its Inspector.";
            ShowTools();
            return set;
        }

        // ---- LEVEL: the selected sounds at the same loudness -------------------------------------------------------------
        bool NoMatch(string bold, string small)
        {
            matchBold = bold; matchSmall = small; matchColour = SfxColors.Text; matchFor = SoundsNow();
            ShowTools();
            return false;
        }

        /// <summary>
        /// MATCH RMS / MATCH PEAK: measures every selected sound and writes its LEVEL so that all read the same
        /// (SfxLevels.Match). Nothing but LEVEL changes; one undo step. False: nothing was changed, and the note says why.
        /// </summary>
        public bool MatchLevels(SfxLevelMeasure by)
        {
            if (!Alive) return false;
            var presets = new List<SfxPreset>();
            foreach (var selected in so.targetObjects) if (selected is SfxPreset preset) presets.Add(preset);
            int n = presets.Count;
            if (!NativeThere) return NoMatch("NOTHING TO MATCH", "the native library is not here: no sound can be measured");
            if (n > MaxMeasured) return NoMatch("NOTHING TO MATCH", "select " + MaxMeasured + " or fewer sounds");
            var now = new int[n]; var next = new int[n]; var reads = new SfxLevelReading[n];
            int counted = 0, left = 0; string firstLeft = null;
            for (int i = 0; i < n; i++)
            {
                reads[i] = Measure(presets[i]).level;
                now[i] = Mathf.Clamp(presets[i].patch.level, 0, 127);
                if (now[i] > 0 && !reads[i].Silent) { counted++; continue; }
                if (left++ == 0) firstLeft = presets[i].name;
            }
            string tail = left == 0 ? "" : left == 1 ? " – '" + firstLeft + "' is silent: left as it is" : " – '" + firstLeft + "' and " + (left - 1) + " more are silent: left as they are";
            if (counted < 2) return NoMatch("NOTHING TO MATCH", "two sounds that are not silent are needed");
            float target = SfxLevels.Match(now, reads, by, next);
            bool any = false;
            for (int i = 0; i < n; i++) any |= next[i] != now[i];
            if (!any) return NoMatch("ALREADY MATCHED", "nothing was changed" + tail);

            Apply(UndoLevel, one =>
            {
                int i = presets.IndexOf(one.targetObject as SfxPreset);
                if (i >= 0 && next[i] != now[i]) one.FindProperty("patch").FindPropertyRelative("level").intValue = next[i];
            });
            MeasureNow();
            string names = ""; int named = 0, more = 0;
            for (int i = 0; i < n; i++)
            {
                if (now[i] <= 0 || reads[i].Silent) continue;
                if (named >= 6) { more++; continue; }
                names += (named++ > 0 ? ", " : "") + presets[i].name + " " + now[i] + " > " + next[i];
            }
            // what they read now, measured: LEVEL is a whole number, so quiet sounds do not land on one figure
            float low = 999f, high = -999f;
            for (int i = 0; i < n; i++)
            {
                if (now[i] <= 0 || reads[i].Silent) continue;
                SfxLevelReading after = Measure(presets[i]).level;
                float reading = by == SfxLevelMeasure.Rms ? after.RmsDb : after.PeakDb;
                low = Mathf.Min(low, reading); high = Mathf.Max(high, reading);
            }
            if (low > high) low = high = target;
            matchBold = counted + " SOUNDS AT " + (by == SfxLevelMeasure.Rms ? "RMS " : "PEAK ") + (high - low < 0.25f ? SfxUnits.DbText(target) : Range(low, high));
            matchSmall = "LEVEL: " + names + (more > 0 ? ", and " + more + " more" : "") + tail;
            matchColour = SfxColors.Text; matchFor = SoundsNow();
            ShowTools();
            return true;
        }

        // ---- SHARE: the sound as text, and as a .syx file ----------------------------------------------------------------
        /// <summary>The sound shown as text: SfxPatchText.ToText - "SFXF1:" and 56 hex digits.</summary>
        public string PatchText => SfxPatchText.ToText(shown);

        void Shared(string bold, Color colour, string small)
        {
            shareBold = bold; shareColour = colour; shareSmall = small; shareFor = SoundsNow();
            ShowTools();
        }

        string ToAll => so.targetObjects.Length > 1 ? ", to all " + so.targetObjects.Length + " sounds" : "";

        /// <summary>COPY: the sound's text to the clipboard.</summary>
        public void CopyText()
        {
            if (!Alive) return;
            EditorGUIUtility.systemCopyBuffer = PatchText;
            Shared("COPIED", SfxColors.Text, SfxPatchText.Length + " characters are on the clipboard.");
        }

        /// <summary>
        /// PASTE: every selected sound becomes the patch a text holds - one undo step; the hold is not part of the text and
        /// stays. A text that is refused changes nothing, adds no undo step, and the note says why (SfxPatchText.Message).
        /// </summary>
        public SfxTextResult PasteText(string text)
        {
            SfxTextResult result = SfxPatchText.Parse(text, out SfxPatch patch);
            if (!Alive) return result;
            if (result != SfxTextResult.Ok) { Shared("NOT PASTED", SfxColors.Warn, SfxPatchText.Message(result)); return result; }
            Edit(UndoPaste, _ => patch);
            Shared("PASTED", SfxColors.Text, SfxUnits.EngineNames[(int)patch.engine] + ToAll);
            return result;
        }

        /// <summary>IMPORT .SYX: every selected sound becomes the patch in the file. As PasteText; a file that cannot be read is refused too.</summary>
        public SfxTextResult ImportSyx(string path)
        {
            SfxTextResult result = SfxForgeEditorLink.ImportSyx(path, out SfxPatch patch, out int from);
            if (!Alive) return result;
            if (result != SfxTextResult.Ok) { Shared("NOT IMPORTED", SfxColors.Warn, SfxPatchText.Message(result)); return result; }
            Edit(UndoPaste, _ => patch);
            Shared("IMPORTED PAD " + (from + 1), SfxColors.Text, SfxUnits.EngineNames[(int)patch.engine] + ToAll);
            return result;
        }

        void ExportSyxAs()
        {
            if (!Alive || so.targetObjects.Length != 1) return;
            string path = EditorUtility.SaveFilePanel("Export patch", SessionState.GetString(SyxFolderKey, ""), so.targetObject.name + ".syx", "syx");
            if (string.IsNullOrEmpty(path)) return;
            SessionState.SetString(SyxFolderKey, Path.GetDirectoryName(path));
            try
            {
                SfxForgeEditorLink.ExportSyx(shown, pad, path);
                Shared("EXPORTED FOR PAD " + (pad + 1), SfxColors.Text, Path.GetFileName(path) + " – a SysEx tool that sends it to the device loads that pad.");
            }
            catch (Exception e) { Shared("NOT EXPORTED", SfxColors.Warn, e.Message); }
        }

        void ImportSyxFrom()
        {
            if (!Alive) return;
            string path = EditorUtility.OpenFilePanel("Import patch", SessionState.GetString(SyxFolderKey, ""), "syx");
            if (string.IsNullOrEmpty(path)) return;
            SessionState.SetString(SyxFolderKey, Path.GetDirectoryName(path));
            ImportSyx(path);
        }

        // ---- the keys ----------------------------------------------------------------------------------------------------
        void Play()
        {
            if (!Alive || !NativeThere) return;
            Refresh();
            SfxForgeEditorLink.Preview(shown, shown.IsSustained ? FirstHold : 0f);
        }

        void Bake()
        {
            if (!Alive || !NativeThere) return;
            AudioClip clip = null;
            foreach (var target in so.targetObjects) if (target is SfxPreset preset) clip = SfxForgeEditorLink.BakeWav(preset) ?? clip;
            if (clip != null) EditorGUIUtility.PingObject(clip);
        }

        void Send()
        {
            if (Alive && SfxLink.Shared.HasOutput) SfxLink.Shared.SendPatch(pad, shown);
        }

        void PlayOnDevice()
        {
            if (!Alive || !SfxLink.Shared.HasOutput) return;
            SfxLink.Shared.SendPatch(pad, shown); SfxLink.Shared.PlayPad(pad); SfxLink.Shared.ReleasePad(pad);
        }

        void PickPad()
        {
            var names = new string[SfxProtocol.Slots];
            for (int i = 0; i < names.Length; i++) names[i] = "Pad " + (i + 1);
            SfxUI.Menu(header.NumberTile, names, pad, pick => { pad = pick; Refresh(); });
        }

        // a moment after the last change: play it, send it (while a knob is dragged: not at every step)
        void HearAndSend()
        {
            if (!Alive) return;
            if (hearChanges && !Application.isPlaying && NativeThere) SfxForgeEditorLink.Preview(shown, shown.IsSustained ? Mathf.Min(FirstHold, 0.8f) : 0f);
            if (sendChanges && SfxLink.Shared.HasOutput) SfxLink.Shared.SendPatch(pad, shown);
        }
    }
}
