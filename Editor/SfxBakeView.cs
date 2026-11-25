// SPDX-License-Identifier: GPL-3.0-or-later
//
// Window > SFX Forge Bake, without the window: what it shows and what it looks like.
//   SfxBakeModel  what the window shows and what it is asked to do, as plain data and callbacks
//   SfxBakeView   the picture of it, built from the UI kit (Editor/UI)
// SfxBakeWindow.cs fills the model from the selection; SfxBatchBake.cs does the baking. Read the model first.
using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatSplat.SfxForge.Editor
{
    /// <summary>
    /// What Window > SFX Forge Bake shows, and what it is asked to do - apart from the AssetDatabase, so the view can
    /// be built, shown and tested without a selection. The window fills it from the selection and answers the actions.
    /// </summary>
    public sealed class SfxBakeModel
    {
        public const string DefaultFolder = "Assets/SfxForge/Baked";

        /// <summary>The most report lines the view shows.</summary>
        public const int MaxLines = 8;

        /// <summary>What the selection holds: presets, variation sets, and the files a bake of them writes.</summary>
        public int presets;
        public int sets;
        public int files;

        /// <summary>The device's engine is here: something can be baked.</summary>
        public bool nativeThere = true;

        /// <summary>How the files are rendered. The view changes it and tells optionsChanged.</summary>
        public SfxBakeOptions options = new SfxBakeOptions();
        public SfxBakeTo to = SfxBakeTo.NextToEachAsset;
        public string folder = DefaultFolder;

        /// <summary>The last run; null: none yet.</summary>
        public SfxBakeReport report;

        // ---- what the view asks for ----
        public Action bake;
        public Action bakeSets;
        public Action show;
        public Action optionsChanged;
        public Action<SfxBakeTo> setTo;
        public Action<string> setFolder;
    }

    /// <summary>
    /// The bake window as the device's display: the top line (how many files), the options as value tiles, where the
    /// files go, the last bake's lines, and the keys BAKE, BAKE SETS, SHOW. It shows a SfxBakeModel and tells it what
    /// the mouse did; it never touches the AssetDatabase. Call Refresh() after the model changed.
    /// </summary>
    public sealed class SfxBakeView : VisualElement
    {
        public const int TileRate = 0;
        public const int TileChannels = 1;
        public const int TilePitch = 2;
        public const int TileNote = 3;
        public const int TileVelocity = 4;
        public const int TileTrim = 5;
        public const int TileNormalise = 6;
        public const int Tiles = 7;
        static readonly int[] Rates = { 22050, 24000, 32000, 44100, 48000 };
        static readonly string[] RateNames = { "22050Hz", "24000Hz", "32000Hz", "44100Hz", "48000Hz" };
        static readonly string[] ChannelNames = { "STEREO", "MONO" };
        static readonly string[] TrimNames = { "OFF", "-60dB", "-48dB", "-36dB" };
        static readonly float[] Trims = { 0f, -60f, -48f, -36f };
        const int LowNote = 24;
        const int HighNote = 96;
        const int DeepestNormalise = 24;
        static readonly string[] Labels = { "RATE", "CHANNELS", "PITCH", "NOTE", "VELOCITY", "TRIM", "NORMALISE" };
        static readonly string[] Tips =
        {
            "The files' sample rate. Every sound is rendered at 48000, the device's own rate, and brought to "
                + "this one: the same sound, as long, with nothing above half the rate.",
            "STEREO, or MONO: left and right mixed into one channel.",
            "Transposes every sound, in semitones: -24 to +24.",
            "AS DESIGNED, or every sound played as this note (C1 to C7).",
            "How hard the sounds are played: 1% to 100%.",
            "Cuts the end of a file after its last sample above this level. OFF: as the engine ends it.",
            "Every file's peak is brought to this level: 0dB to -24dB. OFF (below -24dB): the sounds keep "
                + "their levels against each other.",
        };

        readonly SfxBakeModel model;
        readonly SfxHeader header = new SfxHeader();
        readonly ValueTile[] tiles = new ValueTile[Tiles];
        readonly SoftKey nextToKey;
        readonly SoftKey oneFolderKey;
        readonly SoftKey bakeKey;
        readonly SoftKey setsKey;
        readonly SoftKey showKey;
        readonly TextField folderField = new TextField { isDelayed = true };
        readonly VisualElement log = SfxUI.Box("sfx-log");
        readonly TextElement logNone;
        readonly TextElement logMore;
        readonly TextElement[] logLines = new TextElement[SfxBakeModel.MaxLines];
        readonly SfxFooter footer = new SfxFooter();
        readonly VisualElement blank = new VisualElement();
        int valueColumns;

        public SfxBakeView(SfxBakeModel shown)
        {
            model = shown ?? new SfxBakeModel();
            var root = SfxUI.Panel();
            Add(root);
            header.HideLink();
            header.NumberTile.tooltip =
                "How many files BAKE writes: one for a preset, one for each variant of a variation set.";
            root.Add(header);

            var values = SfxUI.Box("sfx-song-values");
            for (int i = 0; i < Tiles; i++)
            {
                int at = i;
                var t = new ValueTile(0, false, ValueTile.Look.Row);
                tiles[i] = t;
                t.tooltip = Tips[i];
                t.StepPixels = i == TileRate || i == TileChannels || i == TileTrim ? 14f : 4f;
                t.nudged += d => Nudge(at, d);
                t.typed += text => Typed(at, text);
                t.resetRequested += () => ResetValue(at);
                t.chosen += pick => Choose(at, pick);
                values.Add(t);
            }

            tiles[TileRate].Choices = RateNames;
            tiles[TileChannels].Choices = ChannelNames;
            tiles[TileTrim].Choices = TrimNames;
            tiles[TileNote].AddToClassList("sfx-tile--words"); // ("AS DESIGNED" is long for a value)
            blank.AddToClassList("sfx-tile");
            blank.AddToClassList("sfx-tile--panel");
            blank.AddToClassList("sfx-tile--empty");
            blank.pickingMode = PickingMode.Ignore;

            // (seven values: an eighth place makes complete rows, four and four or two a row)
            values.Add(blank);
            values.RegisterCallback<GeometryChangedEvent>(e => SetValueColumns(e.newRect.width));
            root.Add(values);

            // where the files go: two keys, the lit one is where (as a page's tab is) - and the one folder's name
            root.Add(SfxUI.Text("THE FILES GO", "sfx-section"));
            var places = new SoftKeyRow(true);
            nextToKey = places.Add(
                "NEXT TO EACH ASSET",
                () => model.setTo?.Invoke(SfxBakeTo.NextToEachAsset),
                "Every file is written next to the asset it is made from."
            );
            oneFolderKey = places.Add(
                "INTO ONE FOLDER",
                () => model.setTo?.Invoke(SfxBakeTo.OneFolder),
                "All files are written into the folder named below."
            );
            root.Add(places);
            var where = SfxUI.Box("sfx-field-row");
            var folderLabel = SfxUI.Text("FOLDER", "sfx-field-label");
            folderLabel.style.marginLeft = 0;
            folderField.AddToClassList("sfx-field");
            folderField.tooltip = "The one folder, inside Assets. It is made if it is not there.";
            folderField.RegisterValueChangedCallback(e => model.setFolder?.Invoke(e.newValue));
            where.Add(folderLabel);
            where.Add(folderField);
            root.Add(where);

            root.Add(SfxUI.Text("LAST BAKE", "sfx-section"));
            logNone = SfxUI.Text("nothing baked yet", "sfx-log__line");
            log.Add(logNone);
            for (int i = 0; i < logLines.Length; i++)
            {
                logLines[i] = SfxUI.Text("", "sfx-log__line");

                // (for its tooltip: the whole line, where the window is too narrow for it)
                logLines[i].pickingMode = PickingMode.Position;
                log.Add(logLines[i]);
            }

            logMore = SfxUI.Text("", "sfx-log__line");
            log.Add(logMore);
            root.Add(log);

            var keys = new SoftKeyRow();
            bakeKey = keys.Add(
                "BAKE",
                () => model.bake?.Invoke(),
                "Writes every selected preset, and every variant of every selected variation set, as a .wav file "
                    + "with the options above."
            );
            setsKey = keys.Add(
                "BAKE SETS",
                () => model.bakeSets?.Invoke(),
                "Every selected variation set bakes its own clips, as BAKE in its Inspector does. The options "
                    + "above are not used."
            );
            showKey = keys.Add("SHOW", () => model.show?.Invoke(), "Shows the file that was written last.");
            root.Add(keys);
            root.Add(footer);

            Refresh();
        }

        public SfxBakeModel Model => model;

        public ValueTile Tile(int i) => tiles[i];

        public TextField FolderField => folderField;

        /// <summary>The key with this word on it, null if there is none.</summary>
        public SoftKey Key(string word) => this.Query<SoftKey>().Where(k => k.text == word).First();

        /// <summary>The two keys that say where the files go: the lit one is where.</summary>
        public SoftKey ToKey(SfxBakeTo to) => to == SfxBakeTo.OneFolder ? oneFolderKey : nextToKey;

        public string HeaderNumber => ((TextElement)header.NumberTile).text;
        public string HeaderSub => header.Q<TextElement>(className: "sfx-engine").text;
        public string HeaderName => header.Q<TextElement>(className: "sfx-name").text;
        public string FooterText => footer.Bold + " | " + footer.Small;

        /// <summary>
        /// How many report lines show, line i of them ("" below the last), and whether it is in the warning colour.
        /// </summary>
        public int LineCount
        {
            get
            {
                int n = 0;
                foreach (var l in logLines)
                {
                    if (l.style.display != DisplayStyle.None)
                    {
                        n++;
                    }
                }

                return n;
            }
        }

        public string LineText(int i) => logLines[i].style.display == DisplayStyle.None ? "" : logLines[i].text;

        public bool LineWarns(int i) => logLines[i].ClassListContains("sfx-log__line--warn");

        /// <summary>
        /// The line under the last one: "... and 12 more: see the Console" ("" while it is not shown).
        /// </summary>
        public string MoreText => logMore.style.display == DisplayStyle.None ? "" : logMore.text;

        static string Plural(int n, string one, string many) =>
            n.ToString(CultureInfo.InvariantCulture) + " " + (n == 1 ? one : many);

        // NORMALISE as a place on its tile: 0 = 0dB ... 24 = -24dB, 25 = OFF
        static int NormalisePlace(float db) =>
            float.IsNaN(db) ? DeepestNormalise + 1 : Mathf.Clamp(Mathf.RoundToInt(-db), 0, DeepestNormalise);

        /// <summary>Shows the model as it is now.</summary>
        public void Refresh()
        {
            var m = model;
            SfxBakeOptions o = m.options ?? (m.options = new SfxBakeOptions());
            Color accent = SfxColors.Pattern;

            // (nothing to bake: the number is not lit)
            header.Set(
                m.files.ToString(CultureInfo.InvariantCulture),
                "BAKE TO WAV",
                Plural(m.presets, "PRESET", "PRESETS") + ", " + Plural(m.sets, "SET", "SETS"),
                m.files > 0 ? accent : SfxColors.Dim
            );

            int rate = Array.IndexOf(Rates, o.sampleRate);
            tiles[TileRate].ChoiceIndex = rate;
            var rateTile = tiles[TileRate];
            rateTile.Show(
                Labels[TileRate],
                o.sampleRate.ToString(CultureInfo.InvariantCulture) + "Hz",
                rate < 0 ? -1f : (rate + 1) / (float)Rates.Length,
                false,
                accent
            );
            tiles[TileChannels].ChoiceIndex = o.mono ? 1 : 0;
            tiles[TileChannels]
                .Show(Labels[TileChannels], ChannelNames[o.mono ? 1 : 0], o.mono ? 0.5f : 1f, false, accent);
            int st = Mathf.RoundToInt(o.semitones);
            var pitchTile = tiles[TilePitch];
            pitchTile.Show(
                Labels[TilePitch],
                (st > 0 ? "+" : "") + st.ToString(CultureInfo.InvariantCulture) + "st",
                (st + 24) / 48f,
                true,
                accent
            );
            var noteTile = tiles[TileNote];
            noteTile.Show(
                Labels[TileNote],
                o.midiNote <= 0 ? "AS DESIGNED" : SfxUnits.NoteText(o.midiNote),
                o.midiNote <= 0 ? -1f : (o.midiNote - LowNote) / (float)(HighNote - LowNote),
                false,
                accent
            );
            int velocity = Mathf.Clamp(Mathf.RoundToInt(o.velocity * 100f), 1, 100);
            var velocityTile = tiles[TileVelocity];
            velocityTile.Show(
                Labels[TileVelocity],
                velocity.ToString(CultureInfo.InvariantCulture) + "%",
                velocity / 100f,
                false,
                accent
            );
            int trim = Mathf.Max(0, Array.IndexOf(Trims, o.trimDb));
            tiles[TileTrim].ChoiceIndex = trim;
            var trimTile = tiles[TileTrim];
            trimTile.Show(
                Labels[TileTrim],
                o.trimDb == 0f ? "OFF" : NormaliseText(o.trimDb),
                trim / (float)(Trims.Length - 1),
                false,
                accent
            );
            var normaliseTile = tiles[TileNormalise];
            normaliseTile.Show(
                Labels[TileNormalise],
                float.IsNaN(o.normaliseDb) ? "OFF" : NormaliseText(o.normaliseDb),
                float.IsNaN(o.normaliseDb) ? -1f : 1f - Mathf.Clamp01(-o.normaliseDb / DeepestNormalise),
                false,
                accent
            );

            bool one = m.to == SfxBakeTo.OneFolder;
            nextToKey.Lit = !one;
            oneFolderKey.Lit = one;
            folderField.EnableInClassList("sfx-field--unused", !one);
            if (folderField.value != (m.folder ?? ""))
            {
                folderField.SetValueWithoutNotify(m.folder ?? "");
            }

            var lines = m.report != null ? m.report.lines : null;
            int count = lines != null ? lines.Count : 0;
            int shown = Mathf.Min(count, logLines.Length);
            int from = count - shown;
            logNone.style.display = count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            for (int i = 0; i < logLines.Length; i++)
            {
                logLines[i].style.display = i < shown ? DisplayStyle.Flex : DisplayStyle.None;
                if (i >= shown)
                {
                    continue;
                }

                var line = lines[from + i];
                logLines[i].text = line.Text;
                logLines[i].tooltip = line.Text;
                logLines[i].EnableInClassList("sfx-log__line--warn", line.Warns);
            }

            logMore.style.display = from > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            logMore.text = from > 0 ? "… and " + from + " more: see the Console" : "";

            bakeKey.Off = m.files == 0 || !m.nativeThere;
            setsKey.Off = m.sets == 0 || !m.nativeThere;
            showKey.Off = m.report == null || m.report.LastFile == null;

            if (!m.nativeThere)
            {
                footer.Set(
                    "NATIVE LIBRARY NOT FOUND",
                    SfxColors.Warn,
                    "sfxforge is here for Windows x64 only: nothing can be baked here."
                );
            }
            else if (m.report != null)
            {
                footer.Set(
                    m.report.Cancelled ? "CANCELLED"
                        : m.report.Failed > 0 ? "BAKED, NOT ALL OF IT"
                        : "BAKED",
                    m.report.Failed > 0 || m.report.Cancelled ? SfxColors.Warn : SfxColors.Linked,
                    m.report.Summary
                );
            }
            else if (m.presets + m.sets == 0)
            {
                footer.Set(
                    "NOTHING TO BAKE",
                    SfxColors.Dim,
                    "Select presets, variation sets or folders in the Project window."
                );
            }
            else
            {
                footer.Set(
                    Plural(m.files, "FILE", "FILES"),
                    SfxColors.Text,
                    "from "
                        + Plural(m.presets, "preset", "presets")
                        + " and "
                        + Plural(m.sets, "variation set", "variation sets")
                        + " in the selection"
                );
            }
        }

        void SetValueColumns(float width)
        {
            int n = width < 560f ? 2 : 4;
            if (n == valueColumns)
            {
                return;
            }

            valueColumns = n;
            foreach (var tile in tiles)
            {
                tile.style.minWidth = Length.Percent(n == 4 ? 23f : 46f);
            }

            blank.style.minWidth = Length.Percent(n == 4 ? 23f : 46f);
        }

        static string NormaliseText(float db)
        {
            double tenths = Math.Round(db * 10.0);
            return (
                    tenths % 10 == 0
                        ? ((int)(tenths / 10)).ToString(CultureInfo.InvariantCulture)
                        : (tenths / 10.0).ToString("0.0", CultureInfo.InvariantCulture)
                ) + "dB";
        }

        // ---- what the mouse did -------------------------------------------------------------------------------------

        void Changed()
        {
            model.optionsChanged?.Invoke();
            Refresh();
        }

        /// <summary>Turns a tile (TileRate .. TileNormalise) by whole steps.</summary>
        public void Nudge(int tile, int d)
        {
            SfxBakeOptions o = model.options;
            if (d == 0)
            {
                return;
            }

            switch (tile)
            {
                case TileRate:
                {
                    int at = Array.IndexOf(Rates, o.sampleRate);
                    o.sampleRate = Rates[Mathf.Clamp((at < 0 ? Rates.Length - 1 : at) + d, 0, Rates.Length - 1)];
                    break;
                }

                case TileChannels:
                    o.mono = d < 0;
                    break;
                case TilePitch:
                    o.semitones = Mathf.Clamp(Mathf.RoundToInt(o.semitones) + d, -24, 24);
                    break;
                case TileNote:
                    o.midiNote =
                        o.midiNote <= 0 ? (d > 0 ? Mathf.Clamp(LowNote + d - 1, LowNote, HighNote) : 0)
                        : o.midiNote + d < LowNote ? 0
                        : Mathf.Min(HighNote, o.midiNote + d);
                    break;
                case TileVelocity:
                    o.velocity = Mathf.Clamp(Mathf.RoundToInt(o.velocity * 100f) + d, 1, 100) / 100f;
                    break;
                case TileTrim:
                {
                    int at = Mathf.Max(0, Array.IndexOf(Trims, o.trimDb));
                    o.trimDb = Trims[Mathf.Clamp(at + d, 0, Trims.Length - 1)];
                    break;
                }

                case TileNormalise:
                {
                    int place = Mathf.Clamp(NormalisePlace(o.normaliseDb) - d, 0, DeepestNormalise + 1);
                    o.normaliseDb = place > DeepestNormalise ? float.NaN : -place;
                    break;
                }
            }

            Changed();
        }

        /// <summary>
        /// A typed value for a tile: "44100", "mono", "+3", "C4" or "as designed", "80%", "-48", "-6dB" or "off". What
        /// is not understood changes nothing.
        /// </summary>
        public void Typed(int tile, string text)
        {
            SfxBakeOptions o = model.options;
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            string t = text.Trim().ToLowerInvariant();
            bool number = SfxUnits.Magnitude(t, out double v, out string unit) && t != "off";
            switch (tile)
            {
                case TileRate:
                    if (!number)
                    {
                        return;
                    }

                    {
                        int best = 0;
                        for (int i = 1; i < Rates.Length; i++)
                        {
                            if (Math.Abs(Rates[i] - v) < Math.Abs(Rates[best] - v))
                            {
                                best = i;
                            }
                        }

                        o.sampleRate = Rates[best];
                    }

                    break;
                case TileChannels:
                    if (t.StartsWith("m") || t == "1")
                    {
                        o.mono = true;
                    }
                    else if (t.StartsWith("s") || t == "2")
                    {
                        o.mono = false;
                    }
                    else
                    {
                        return;
                    }

                    break;
                case TilePitch:
                    if (!number || (unit.Length > 0 && unit != "st"))
                    {
                        return;
                    }

                    o.semitones = Mathf.Clamp(Mathf.RoundToInt((float)v), -24, 24);
                    break;
                case TileNote:
                    if (t == "off" || t == "0" || t.StartsWith("as") || t == "--")
                    {
                        o.midiNote = 0;
                    }
                    else if (SfxUnits.TryParse(text, LowNote, HighNote, SfxUnits.NoteText, out int note))
                    {
                        o.midiNote = note;
                    }
                    else
                    {
                        return;
                    }

                    break;
                case TileVelocity:
                    if (!number || (unit.Length > 0 && unit != "%"))
                    {
                        return;
                    }

                    o.velocity = Mathf.Clamp(Mathf.RoundToInt((float)v), 1, 100) / 100f;
                    break;
                case TileTrim:
                    if (t == "off" || (number && v == 0))
                    {
                        o.trimDb = 0f;
                    }
                    else if (number && (unit.Length == 0 || unit == "db"))
                    {
                        int best = 1;
                        double want = -Math.Abs(v);
                        for (int i = 2; i < Trims.Length; i++)
                        {
                            if (Math.Abs(Trims[i] - want) < Math.Abs(Trims[best] - want))
                            {
                                best = i;
                            }
                        }

                        o.trimDb = Trims[best];
                    }
                    else
                    {
                        return;
                    }

                    break;
                case TileNormalise:
                    if (t == "off")
                    {
                        o.normaliseDb = float.NaN;
                    }
                    else if (SfxUnits.TryParseDb(text, out float db))
                    {
                        // (0 to -24, as the tile turns)
                        o.normaliseDb = Mathf.Clamp(-Mathf.Abs(db), -DeepestNormalise, 0f);
                    }
                    else
                    {
                        return;
                    }

                    break;
            }

            Changed();
        }

        /// <summary>A double-click on a tile's ring: 48000Hz, STEREO, 0st, AS DESIGNED, 100%, OFF, OFF.</summary>
        public void ResetValue(int tile)
        {
            SfxBakeOptions o = model.options;
            switch (tile)
            {
                case TileRate:
                    o.sampleRate = 48000;
                    break;
                case TileChannels:
                    o.mono = false;
                    break;
                case TilePitch:
                    o.semitones = 0f;
                    break;
                case TileNote:
                    o.midiNote = 0;
                    break;
                case TileVelocity:
                    o.velocity = 1f;
                    break;
                case TileTrim:
                    o.trimDb = 0f;
                    break;
                case TileNormalise:
                    o.normaliseDb = float.NaN;
                    break;
            }

            Changed();
        }

        /// <summary>A name picked from a tile's menu (RATE, CHANNELS, TRIM).</summary>
        public void Choose(int tile, int pick)
        {
            SfxBakeOptions o = model.options;
            if (tile == TileRate)
            {
                o.sampleRate = Rates[Mathf.Clamp(pick, 0, Rates.Length - 1)];
            }
            else if (tile == TileChannels)
            {
                o.mono = pick == 1;
            }
            else if (tile == TileTrim)
            {
                o.trimDb = Trims[Mathf.Clamp(pick, 0, Trims.Length - 1)];
            }
            else
            {
                return;
            }

            Changed();
        }
    }
}
