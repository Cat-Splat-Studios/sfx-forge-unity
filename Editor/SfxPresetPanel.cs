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
    /// A sound as the device shows it: the top line, the seven pages of four values (OSC TONE FILTER WAVE ENV LFO FX) around
    /// the picture of the sound, and the soft keys. Every edit goes through SerializedObject, so Unity's Undo takes it back
    /// and several selected sounds are edited together (values that differ read "-").
    /// What SfxPresetEditor shows in the Inspector; made by SfxPresetEditor.Build.
    /// </summary>
    public sealed class SfxPresetPanel : VisualElement
    {
        const int UndoEdit = 0, UndoMutate = 1, UndoRandom = 2;
        static readonly string[] UndoWords = { "Edit", "Mutate", "Random" };
        const float DefaultHold = 1.5f, MinHold = 0.05f;
        /// <summary>The longest hold: what a bake renders at most (SfxNative.Bake).</summary>
        public const float MaxHold = 12f;

        static readonly string[] Fields = BuildFields();
        static readonly System.Random Rng = new System.Random();
        static bool hearChanges = true, sendChanges;
        static int pad;

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
        readonly SfxFooter holdNote = new SfxFooter(), linkNote = new SfxFooter();
        readonly WaveformView wave = new WaveformView();
        readonly SoftKey playKey, undoKey, bakeKey, sendKey, hearKey, deviceKey, sendChangesKey;
        readonly IVisualElementScheduledItem pictureLater, actLater;
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
            keys.Add("MUTATE", Mutate, "Every value except LEVEL and TABLE moves by up to 15 % – a variation of this sound.");
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

            pictureLater = schedule.Execute(Picture); pictureLater.Pause();
            actLater = schedule.Execute(HearAndSend); actLater.Pause();
            schedule.Execute(UpdateLink).Every(400);
            RegisterCallback<AttachToPanelEvent>(_ => Undo.undoRedoPerformed += Refresh);
            RegisterCallback<DetachFromPanelEvent>(_ => Undo.undoRedoPerformed -= Refresh);
            this.TrackSerializedObjectValue(so, _ => Refresh());   // changed from elsewhere: a reset, another Inspector

            Refresh();
            Picture();                                      // the first picture at once
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
                string name = SfxUnits.ParamName(p, shown.engine);
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
            UpdateKeys();
            UpdateLink();
            if (!pictured || !shown.Equals(picturedPatch)) pictureLater.ExecuteLater(40);
        }

        void UpdateKeys()
        {
            bool native = NativeThere, linked = SfxLink.Shared.HasOutput && SfxLink.Refusal(SfxLink.Shared.Device, false) == null;
            playKey.Off = !native; bakeKey.Off = !native;
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

        /// <summary>MUTATE: every value of every selected sound, except LEVEL and TABLE, moves by up to 15 %.</summary>
        public void Mutate() => Edit(UndoMutate, p => { p.Mutate(Rng); return p; });
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
                return top == undoNames[UndoEdit] || top == undoNames[UndoMutate] || top == undoNames[UndoRandom];
            }
        }

        /// <summary>UNDO: Unity's Undo – only while its next step is a change made to this sound (these sounds) here.</summary>
        public void UndoOwn()
        {
            if (Alive && OursOnTop) Undo.PerformUndo();
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
