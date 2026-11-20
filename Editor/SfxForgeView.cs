// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatSplat.SfxForge.Editor
{
    /// <summary>
    /// What Window > SFX Forge shows, and what it is asked to do – apart from the link itself, so the view can be built,
    /// shown and tested without a MIDI port. The window fills it from SfxLink and answers the actions; a test or a
    /// picture fills it with anything.
    /// </summary>
    public sealed class SfxForgeModel
    {
        public const int MaxPatches = 32, MaxMessages = 6, Knobs = SfxProtocol.EncoderCount, Pads = SfxProtocol.Slots;
        public const string DefaultFolder = "Assets/SfxForge";

        /// <summary>A patch the device sent: which pad, when, and the preset it was saved as (null: not yet).</summary>
        public sealed class Patch
        {
            public int slot;
            public SfxPatch patch;
            public string time;
            public SfxPreset saved;
        }

        // ---- the link ----
        /// <summary>False where MIDI is not implemented (anything but Windows).</summary>
        public bool midiSupported = true;
        public bool isOpen, hasOutput;
        /// <summary>The ports there are.</summary>
        public string[] inputs = new string[0], outputs = new string[0];
        /// <summary>The ports chosen ("": none), and the ones that are open (null: none).</summary>
        public string inputPort = "", outputPort = "", inputName, outputName;
        /// <summary>
        /// What the device said it is ("VIBE FM, firmware 1.4.0, profile 1"), or NoIdentity once a request went
        /// unanswered for a second. Null: nothing to say (not connected, or asked a moment ago).
        /// </summary>
        public string identity;
        /// <summary>True: identity is what a device said. False: it is that none answered.</summary>
        public bool identityKnown;
        public const string NoIdentity = "no identity: firmware before 1.4, or nothing answers";

        // ---- the device ----
        /// <summary>0: a pad plays. 1: a pad fetches its patch.</summary>
        public int padMode;
        /// <summary>The pad touched last, here or on the device (the number at the top left).</summary>
        public int lastPad;
        /// <summary>The text for the device's top line.</summary>
        public string text = "";
        /// <summary>What the device's knobs sent last on its LINK page (CC 20..25): 0..127, -1 = nothing yet.</summary>
        public readonly int[] knobs = new int[Knobs];
        /// <summary>The pads held now, here or on the device.</summary>
        public readonly bool[] padDown = new bool[Pads];

        // ---- what came ----
        public bool autoSave = true;
        public string folder = DefaultFolder;
        /// <summary>The patches the device sent, the newest first.</summary>
        public readonly List<Patch> received = new List<Patch>();
        /// <summary>The last messages in, the newest first, as text.</summary>
        public readonly List<string> messages = new List<string>();
        /// <summary>A song was fetched in this session (SHOW SONG has something to show).</summary>
        public bool hasSong;

        // ---- what the view asks for ----
        public Action connect, disconnect, refreshPorts, fetchSong, showSong;
        public Action<string> pickInput, pickOutput, sendText, setFolder;
        public Action<bool> setAutoSave;
        public Action<int> playPad, releasePad, fetchPad;
        public Action<Patch> play, save, show;

        public SfxForgeModel() { ForgetKnobs(); }

        public void ForgetKnobs() { for (int i = 0; i < knobs.Length; i++) knobs[i] = -1; }

        /// <summary>A patch came: it goes to the top of the list (which keeps the last 32).</summary>
        public Patch TakePatch(int slot, SfxPatch patch, string time)
        {
            var p = new Patch { slot = slot, patch = patch, time = time };
            received.Insert(0, p);
            if (received.Count > MaxPatches) received.RemoveAt(received.Count - 1);
            lastPad = Mathf.Clamp(slot, 0, Pads - 1);
            return p;
        }

        /// <summary>A message came: its text goes to the top of the list (which keeps the last 6).</summary>
        public void TakeMessage(string line)
        {
            messages.Insert(0, line);
            if (messages.Count > MaxMessages) messages.RemoveAt(messages.Count - 1);
        }

        /// <summary>A knob of the device's LINK page turned: index 0..5, value 0..1.</summary>
        public void TakeKnob(int index, float value01)
        {
            if (index >= 0 && index < knobs.Length) knobs[index] = Mathf.Clamp(Mathf.RoundToInt(value01 * 127f), 0, 127);
        }

        public void TakePad(int pad, bool down)
        {
            if (pad < 0 || pad >= Pads) return;
            padDown[pad] = down;
            if (down) lastPad = pad;
        }

        /// <summary>The newest patch that came from a pad, null if none did.</summary>
        public Patch PatchOfPad(int pad)
        {
            foreach (var p in received) if (p.slot == pad) return p;
            return null;
        }
    }

    /// <summary>
    /// Window > SFX Forge as the device's display: the top line, the link as its LINK page (left what the device's knobs
    /// send, right the two ports and LINKED / NOT CONNECTED), the 16 pads as tiles, the patches that came as tiles in
    /// their engine's colour, the last messages, and a row of soft keys. It shows a SfxForgeModel and tells it what the
    /// mouse did; it never touches a MIDI port. Call Refresh() after the model changed.
    /// </summary>
    public sealed class SfxForgeView : VisualElement
    {
        static readonly string[] KnobNames = { "KNOB 1   CC 20", "KNOB 2   CC 21", "KNOB 3   CC 22", "KNOB 4   CC 23", "KNOB A   CC 24", "KNOB B   CC 25" };
        const float PatchTileWidth = 150f;

        readonly SfxForgeModel model;
        readonly SfxHeader header = new SfxHeader();
        readonly LinkBars bars = new LinkBars("FROM THE DEVICE", KnobNames);
        readonly PortTile inTile = new PortTile("IN"), outTile = new PortTile("OUT");
        readonly SfxFooter status = new SfxFooter();
        readonly PageTabs padTabs = new PageTabs("PADS PLAY", "PADS FETCH THEIR PATCH");
        readonly PadGrid pads = new PadGrid();
        readonly TextField textField = new TextField(), folderField = new TextField { isDelayed = true };
        readonly VisualElement patchGrid = SfxUI.Box("sfx-patches"), log = SfxUI.Box("sfx-log");
        readonly TextElement title, padCap, patchHint, logNone, identity;
        readonly TextElement[] logLines = new TextElement[SfxForgeModel.MaxMessages];
        readonly SoftKey showTextKey, autoKey, connectKey, fetchKey, showSongKey;
        readonly Dictionary<SfxForgeModel.Patch, PatchTile> tiles = new Dictionary<SfxForgeModel.Patch, PatchTile>();
        readonly List<SfxForgeModel.Patch> gone = new List<SfxForgeModel.Patch>();
        readonly bool[] mouseDown = new bool[SfxForgeModel.Pads];
        int columns = 2;

        public SfxForgeView(SfxForgeModel shown)
        {
            model = shown ?? new SfxForgeModel();
            var root = SfxUI.Panel();
            Add(root);
            root.Add(header);
            header.NumberTile.tooltip = "The pad touched last, here or on the device.";
            title = SfxUI.Text("GAME LINK", "sfx-title");
            title.style.color = SfxColors.Pattern;
            root.Add(title);

            // the link, as the device's LINK page: a panel of bars left, tiles and the state right
            var link = SfxUI.Box("sfx-link");
            var box = SfxUI.Box("sfx-link__box");
            bars.tooltip = "On the device's LINK page its knobs send CC 20-25 to the game: what they sent last.";
            box.Add(bars);
            var side = SfxUI.Box("sfx-link__side");
            inTile.tooltip = "The MIDI port the device sends to. A click chooses it (not while connected).";
            outTile.tooltip = "The MIDI port the device listens to. A click chooses it (not while connected).";
            inTile.clicked += () => Ports(inTile, model.inputs, model.inputPort, model.pickInput);
            outTile.clicked += () => Ports(outTile, model.outputs, model.outputPort, model.pickOutput);
            identity = SfxUI.Text("", "sfx-identity");
            identity.pickingMode = PickingMode.Position;    // (for its tooltip)
            identity.tooltip = "What the device answered when it was asked what it is (firmware 1.4.0 on): its hardware, its firmware, its profile. An older firmware does not answer; it works all the same.";
            side.Add(inTile); side.Add(outTile); side.Add(status); side.Add(identity);
            link.Add(box); link.Add(side);
            root.Add(link);

            // the device: its pads, a text for its top line
            padTabs.changed += i => { model.padMode = i; Refresh(); };
            root.Add(padTabs);
            pads.pressed += PadPressed;
            pads.released += PadReleased;
            root.Add(pads);
            padCap = SfxUI.Text("", "sfx-song-cap");
            root.Add(padCap);
            var textRow = SfxUI.Box("sfx-field-row");
            Field(textField, "A text for the device's top line: a level's name, a state (24 characters).");
            textField.maxLength = 24;
            textField.RegisterValueChangedCallback(e => model.text = e.newValue);
            textField.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) SendText(); });
            showTextKey = new SoftKey("SHOW ON DEVICE", SendText, "Shows the text on the device's top line.");
            showTextKey.AddToClassList("sfx-field-key");
            showTextKey.style.marginLeft = 4;
            textRow.Add(textField); textRow.Add(showTextKey);
            root.Add(textRow);

            // what came: the patches
            root.Add(SfxUI.Text("PATCHES FROM THE DEVICE", "sfx-section"));
            var saveRow = SfxUI.Box("sfx-field-row");
            autoKey = new SoftKey("SAVE AS THEY ARRIVE", () => model.setAutoSave?.Invoke(!model.autoSave), "On: a patch becomes a preset as soon as it arrives (EXPORT on the device). Off: SAVE on its tile does it.");
            autoKey.AddToClassList("sfx-field-key");
            Field(folderField, "The folder the presets and songs are saved in (inside Assets).");
            folderField.RegisterValueChangedCallback(e => model.setFolder?.Invoke(e.newValue));
            saveRow.Add(autoKey); saveRow.Add(SfxUI.Text("FOLDER", "sfx-field-label")); saveRow.Add(folderField);
            root.Add(saveRow);
            patchHint = SfxUI.Text("Press EXPORT (F5) on the device, or fetch a pad above.", "sfx-song-cap");
            root.Add(patchHint);
            patchGrid.RegisterCallback<GeometryChangedEvent>(e => SetColumns(e.newRect.width));
            root.Add(patchGrid);

            // what came: everything, as it is
            root.Add(SfxUI.Text("LAST MESSAGES IN", "sfx-section"));
            logNone = SfxUI.Text("nothing yet", "sfx-log__line");
            log.Add(logNone);
            for (int i = 0; i < logLines.Length; i++)
            {
                logLines[i] = SfxUI.Text("", "sfx-log__line");
                if (i == 0) logLines[i].AddToClassList("sfx-log__line--new");
                log.Add(logLines[i]);
            }
            root.Add(log);

            var keys = new SoftKeyRow();
            keys.AddToClassList("sfx-keys--four");
            connectKey = keys.Add("CONNECT", () => { if (model.isOpen) model.disconnect?.Invoke(); else model.connect?.Invoke(); });
            keys.Add("REFRESH PORTS", () => model.refreshPorts?.Invoke(), "Looks again for the MIDI ports there are.");
            fetchKey = keys.Add("FETCH SONG", () => model.fetchSong?.Invoke(), "Asks the device for its 16 sounds and its song; they arrive as a Song asset. SEND on its SONG page does the same.");
            showSongKey = keys.Add("SHOW SONG", () => model.showSong?.Invoke(), "Shows the song that was fetched last.");
            root.Add(keys);

            Refresh();
        }

        public SfxForgeModel Model => model;
        public PadGrid Pads => pads;
        public PageTabs PadTabs => padTabs;
        public LinkBars Bars => bars;
        public PortTile InputTile => inTile;
        public PortTile OutputTile => outTile;
        public TextField TextField => textField;
        public TextField FolderField => folderField;
        /// <summary>The state under the ports: "LINKED | in: … out: …".</summary>
        public string StatusText => status.Bold + " | " + status.Small;
        /// <summary>The line under the state: what the device said it is, or that it did not answer. "" while it is not shown.</summary>
        public string IdentityText => identity.style.display == DisplayStyle.None ? "" : identity.text;
        public string HeaderNumber => ((TextElement)header.NumberTile).text;
        /// <summary>The tiles of the patches shown, the newest first.</summary>
        public int PatchCount => patchGrid.childCount;
        public PatchTile PatchTileAt(int i) => (PatchTile)patchGrid[i][0];
        public bool PatchHintShown => patchHint.style.display != DisplayStyle.None;
        /// <summary>Line i of the last messages, "" below the last one.</summary>
        public string MessageText(int i) => logLines[i].style.display == DisplayStyle.None ? "" : logLines[i].text;
        /// <summary>The soft key (or small key) with this word on it, null if there is none.</summary>
        public SoftKey Key(string word) => this.Query<SoftKey>().Where(k => k.text == word).First();

        static void Field(TextField field, string tip)
        {
            field.AddToClassList("sfx-field");
            field.tooltip = tip;
        }

        // ---- what the mouse did --------------------------------------------------------------------------------------------
        void Ports(PortTile under, string[] names, string current, Action<string> picked)
        {
            var options = new string[names.Length + 1];
            options[0] = "(none)";
            Array.Copy(names, 0, options, 1, names.Length);
            int ticked = Mathf.Max(0, SfxLink.Find(names, current) + 1);
            SfxUI.Menu(under, options, ticked, pick => picked?.Invoke(pick == 0 ? "" : names[pick - 1]));
        }

        void PadPressed(int pad)
        {
            if (!model.hasOutput) return;
            model.lastPad = pad;
            if (model.padMode == 0) { mouseDown[pad] = true; model.playPad?.Invoke(pad); }
            else model.fetchPad?.Invoke(pad);
            Refresh();
        }

        void PadReleased(int pad)
        {
            if (!mouseDown[pad]) return;
            mouseDown[pad] = false;
            model.releasePad?.Invoke(pad);
            Refresh();
        }

        /// <summary>Lets go of every pad the mouse still holds (the window closes, or reloads, while one is down).</summary>
        public void LetGo()
        {
            for (int pad = 0; pad < mouseDown.Length; pad++)
            {
                if (!mouseDown[pad]) continue;
                mouseDown[pad] = false;
                model.releasePad?.Invoke(pad);
            }
        }

        void SendText()
        {
            if (model.hasOutput) model.sendText?.Invoke(model.text ?? "");
        }

        void SetColumns(float width)
        {
            int n = Mathf.Clamp((int)(width / PatchTileWidth), 1, 4);
            if (n == columns) return;
            columns = n;
            foreach (var cell in patchGrid.Children()) cell.style.width = Length.Percent(100f / columns);
        }

        // ---- showing the model -----------------------------------------------------------------------------------------------
        /// <summary>Shows the model as it is now.</summary>
        public void Refresh()
        {
            var m = model;
            int last = Mathf.Clamp(m.lastPad, 0, SfxForgeModel.Pads - 1);
            var lastPatch = m.PatchOfPad(last);
            Color accent = lastPatch != null ? SfxColors.Engine(lastPatch.patch.engine) : SfxColors.Pattern;
            header.Set((last + 1).ToString(), "SFX FORGE", lastPatch != null ? SfxUnits.EngineNames[(int)lastPatch.patch.engine % SfxUnits.EngineNames.Length] : "", accent);
            padTabs.Accent = accent;
            header.SetLink(m.isOpen, m.isOpen ? "Linked" : "Not connected");

            for (int i = 0; i < bars.Count; i++) bars.SetValue(i, i < m.knobs.Length ? m.knobs[i] : -1);
            ShowPort(inTile, "FROM DEVICE", m.inputs, m.inputPort, m.inputName);
            ShowPort(outTile, "TO DEVICE", m.outputs, m.outputPort, m.outputName);
            if (m.isOpen) status.Set("LINKED", SfxColors.Linked, "in: " + (m.inputName ?? "–") + "   out: " + (m.outputName ?? "–"));
            else if (!m.midiSupported) status.Set("NO MIDI HERE", SfxColors.Warn, "MIDI is implemented for Windows only so far. Presets, SfxPlayer and baking do not need it.");
            else if (m.inputs.Length + m.outputs.Length == 0)
                status.Set("NO MIDI PORTS", SfxColors.Warn, "Without the hardware, SFX FORGE runs in the browser or the plugin: install a virtual MIDI cable (e.g. loopMIDI) with two ports, give one to the browser's MIDI out and the other to its MIDI in.");
            else status.Set("NOT CONNECTED", SfxColors.Dim, "Choose the two ports, then CONNECT.");
            bool says = m.isOpen && !string.IsNullOrEmpty(m.identity);
            identity.style.display = says ? DisplayStyle.Flex : DisplayStyle.None;
            identity.text = says ? m.identity : "";
            identity.EnableInClassList("sfx-identity--known", says && m.identityKnown);

            padTabs.SetIndex(m.padMode);
            for (int i = 0; i < SfxForgeModel.Pads; i++)
            {
                var p = m.PatchOfPad(i);
                bool held = m.padDown[i] || mouseDown[i];
                string name = p != null ? SfxUnits.EngineShort[(int)p.patch.engine % SfxUnits.EngineShort.Length] : "";
                Color c = p != null ? SfxColors.Engine(p.patch.engine) : SfxColors.Dim;
                pads.SetPad(i, name, c, !m.hasOutput && !held ? PadState.Empty : held ? PadState.Lit : i == last && p != null ? PadState.Selected : PadState.Dim);
            }
            padCap.text = !m.hasOutput ? "The pads need the port TO DEVICE: connect first."
                : m.padMode == 0 ? "A pad plays its sound on the device for as long as it is held."
                : "A pad asks the device for its patch: it arrives below.";
            showTextKey.Off = !m.hasOutput;
            if (textField.value != (m.text ?? "")) textField.SetValueWithoutNotify(m.text ?? "");

            autoKey.Lit = m.autoSave;
            if (folderField.value != (m.folder ?? "")) folderField.SetValueWithoutNotify(m.folder ?? "");
            ShowPatches();

            int lines = Mathf.Min(m.messages.Count, logLines.Length);
            logNone.style.display = lines == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            for (int i = 0; i < logLines.Length; i++)
            {
                logLines[i].style.display = i < lines ? DisplayStyle.Flex : DisplayStyle.None;
                if (i < lines) logLines[i].text = m.messages[i];
            }

            connectKey.text = m.isOpen ? "DISCONNECT" : "CONNECT";
            connectKey.Lit = m.isOpen;
            connectKey.tooltip = m.isOpen ? "Closes the two MIDI ports." : "Opens the two MIDI ports chosen above.";
            fetchKey.Off = !m.hasOutput;
            showSongKey.Off = !m.hasSong;
        }

        void ShowPort(PortTile tile, string label, string[] names, string chosen, string open)
        {
            bool none = string.IsNullOrEmpty(chosen);
            int at = SfxLink.Find(names, chosen);
            if (open != null) tile.Show(label, open, true, true);
            else if (none) tile.Show(label, "(none)", false, false);
            else if (at < 0) tile.Show(label + "  ·  NOT FOUND", chosen, false, false);
            else tile.Show(label, names[at], false, true);
            tile.Locked = model.isOpen;
        }

        // a tile a patch; a tile is kept for as long as its patch is in the list (its picture is baked once)
        void ShowPatches()
        {
            var list = model.received;
            patchHint.style.display = list.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            gone.Clear();
            foreach (var known in tiles.Keys) if (!list.Contains(known)) gone.Add(known);
            foreach (var p in gone) tiles.Remove(p);
            bool native = SfxUI.NativeThere;
            bool same = patchGrid.childCount == list.Count;
            for (int i = 0; same && i < list.Count; i++) same = tiles.TryGetValue(list[i], out var t) && patchGrid[i][0] == t;
            if (!same) patchGrid.Clear();
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                if (!tiles.TryGetValue(p, out var tile))
                {
                    tile = new PatchTile();
                    tile.play += () => model.play?.Invoke(p);
                    tile.save += () => model.save?.Invoke(p);
                    tile.show += () => model.show?.Invoke(p);
                    tile.SetPicture(p.patch, native);
                    tiles[p] = tile;
                }
                tile.Set(p.slot, p.patch.engine, p.time, p.saved != null);
                if (same) continue;
                tile.RemoveFromHierarchy();
                var cell = SfxUI.Box("sfx-patch-cell");
                cell.style.width = Length.Percent(100f / columns);
                cell.Add(tile);
                patchGrid.Add(cell);
            }
        }
    }
}
