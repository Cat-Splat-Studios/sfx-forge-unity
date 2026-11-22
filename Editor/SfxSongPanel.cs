// SPDX-License-Identifier: MIT
using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatSplat.SfxForge.Editor
{
    /// <summary>
    /// A song as the device shows it: the top line, tempo / steps / swing / chain, the patterns A – H with what is in
    /// them, the arrangement's letters – and four pages: STEPS (a row a sound, under them INTENSITY with every sound's
    /// LAYER mark), MIX (a tile a sound: fader, pan), MASTER (the delay and the reverb) and AUTO (the automation
    /// lanes). Under the pages: the chosen sound – its LAYER, VOLUME and PAN – and the soft keys.
    ///
    /// It is a viewer. What can be changed here are five numbers of the song's head, through SerializedObject (so Undo
    /// takes them back): TEMPO, SWING, and the chosen sound's LAYER, VOLUME and PAN. Steps, notes, patterns, the chain,
    /// the arrangement, mutes, the master and the lanes only show: they are made on the device.
    /// What SfxSongEditor shows in the Inspector; made by SfxSongEditor.Build.
    /// </summary>
    public sealed class SfxSongPanel : VisualElement
    {
        public const int PageSteps = 0;
        public const int PageMix = 1;
        public const int PageMaster = 2;
        public const int PageAuto = 3;
        public const int TileTempo = 0;
        public const int TileSteps = 1;
        public const int TileSwing = 2;
        public const int TileChain = 3;
        public const int TileLayer = 0;
        public const int TileVolume = 1;
        public const int TilePan = 2;
        static readonly string[] PageNames = { "STEPS", "MIX", "MASTER", "AUTO" };

        // … and as the display titles them (the pattern stands beside SONG and AUTOMATION only)
        static readonly string[] PageTitles = { "SONG", "MIXER", "MASTER EFFECTS", "AUTOMATION" };
        const string UndoEdit = "Edit SFX Song";

        // where the numbers that can be changed stand in the packed song (core/sfx_song.h, sfx_song_pack). The same
        // numbers as SfxPackedSong.HeadBpm, HeadSwing, HeadLayers, HeadVolumes and HeadPans, which are internal to the
        // runtime assembly: change them together.
        const int AtBpm = 2;
        const int AtSwing = 4;
        const int AtLayer = 7;
        const int AtVolume = 7 + SfxProtocol.Slots;
        const int AtPan = 7 + 2 * SfxProtocol.Slots;
        const int Sounds = SfxProtocol.Slots;
        const int Patterns = SfxProtocol.Patterns;
        const int Steps = SfxSong.MaxSteps;

        // what is looked at – not a part of the song, and kept from one selection to the next
        static int page;
        static int sound;
        static int intensity = 127;

        readonly SerializedObject so;
        readonly SerializedProperty dataProp;
        bool builtValid;
        int pattern;
        int dragGroup = -1;

        SfxHeader header;
        TextElement titlePage;
        TextElement titlePattern;
        TextElement titleScale;
        TextElement arrCap;
        TextElement autoNone;
        TextElement viewOnly;
        PageTabs tabs;
        readonly ValueTile[] songTiles = new ValueTile[4];
        readonly ValueTile[] soundTiles = new ValueTile[3];
        readonly ValueTile[] masterTiles = new ValueTile[5];
        readonly PatternTile[] patternTiles = new PatternTile[Patterns];
        readonly TextElement[] bars = new TextElement[SfxSong.MaxBars];
        readonly VisualElement[] pages = new VisualElement[4];
        readonly TextElement[] names = new TextElement[Sounds];
        readonly StepRow[] rows = new StepRow[Sounds];
        readonly MixTile[] mixTiles = new MixTile[Sounds];
        readonly Lane[] lanes = new Lane[SfxSong.Lanes];
        readonly int[] counts = new int[Steps];
        readonly TextElement[] noteTexts = new TextElement[Steps];
        VisualElement noteLayer;
        IntensityStrip heat;
        IntensityStrip layer;
        MasterView master;
        SfxFooter soundNote;
        SfxFooter linkNote;
        SfxBanner newer;
        SoftKey playKey;
        SoftKey barKey;
        SoftKey sendKey;

        sealed class Lane
        {
            public VisualElement root;
            public TextElement name;
            public TextElement what;
            public TextElement says;
            public LaneRow row;
        }

        public SfxSongPanel(SerializedObject serializedObject)
        {
            so = serializedObject;
            dataProp = so.FindProperty("data");
            Build();
            schedule.Execute(UpdateLink).Every(400);
            RegisterCallback<AttachToPanelEvent>(_ => Undo.undoRedoPerformed += Refresh);
            RegisterCallback<DetachFromPanelEvent>(_ => Undo.undoRedoPerformed -= Refresh);

            // changed from elsewhere: a song fetched into this asset
            this.TrackSerializedObjectValue(so, _ => Refresh());
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

        SfxSong Song => so.targetObject as SfxSong;
        bool OneValidSong => so.targetObjects.Length == 1 && Song != null && Song.IsValid;
        static bool NativeThere => SfxUI.NativeThere;

        /// <summary>The page shown: PageSteps, PageMix, PageMaster or PageAuto.</summary>
        public int Page
        {
            get => page;
            set
            {
                page = Mathf.Clamp(value, 0, PageNames.Length - 1);
                Refresh();
            }
        }

        /// <summary>The pattern looked at: 0 = A .. 7 = H.</summary>
        public int Pattern
        {
            get => pattern;
            set
            {
                pattern = Mathf.Clamp(value, 0, Patterns - 1);
                Refresh();
            }
        }

        /// <summary>The sound chosen: 0..15.</summary>
        public int Sound
        {
            get => sound;
            set
            {
                sound = Mathf.Clamp(value, 0, Sounds - 1);
                Refresh();
            }
        }

        /// <summary>
        /// The intensity the steps are looked at with, and PLAY BAR plays with: 0..127. Not kept in the song.
        /// </summary>
        public int Intensity
        {
            get => intensity;
            set
            {
                intensity = Mathf.Clamp(value, 0, 127);
                Refresh();
            }
        }

        /// <summary>The note written on a step of the chosen sound's row: "E1", or nothing.</summary>
        public string NoteOnStep(int i) => noteTexts[i].text;

        /// <summary>The cells a sound's row shows: the grid of the pattern looked at - 16, 32 or 64.</summary>
        public int RowCount(int sound) => rows[sound].Count;

        /// <summary>What stands beside the page's title: "PATTERN B  1/32".</summary>
        public string PatternTitle => titlePattern.text;

        /// <summary>The row of the i-th lane shown on the AUTO page (null: there is none).</summary>
        public LaneRow LaneRowAt(int i) => i >= 0 && i < lanes.Length && lanes[i] != null ? lanes[i].row : null;

        public ValueTile SongTile(int i) => songTiles[i];

        public ValueTile SoundTile(int i) => soundTiles[i];

        public ValueTile MasterTile(int i) => masterTiles[i];

        public PatternTile PatternTileAt(int i) => patternTiles[i];

        public StepRow Row(int s) => rows[s];

        public MixTile MixTileAt(int s) => mixTiles[s];

        public IntensityStrip Heat => heat;
        public IntensityStrip LayerBar => layer;

        /// <summary>The arrangement as the row of letters shows it: "ABABCDCD".</summary>
        public string ArrangementText
        {
            get
            {
                var t = new StringBuilder();
                foreach (var b in bars)
                {
                    if (b != null && b.style.display != DisplayStyle.None)
                    {
                        t.Append(b.text);
                    }
                }

                return t.ToString();
            }
        }

        /// <summary>The lanes the AUTO page lists.</summary>
        public int LaneCount
        {
            get
            {
                int n = 0;
                foreach (var l in lanes)
                {
                    if (l != null && l.root.style.display != DisplayStyle.None)
                    {
                        n++;
                    }
                }

                return n;
            }
        }

        /// <summary>
        /// What the AUTO page says of its lane i: the sound, the knob, the points of the pattern looked at.
        /// </summary>
        public string LaneText(int i) =>
            lanes[i] == null ? "" : lanes[i].name.text + " | " + lanes[i].what.text + " | " + lanes[i].says.text;

        /// <summary>
        /// The title line as it shows: "SONG | PATTERN A | scale MINOR", "MIXER", "AUTOMATION | PATTERN F".
        /// </summary>
        public string TitleText
        {
            get
            {
                string t = titlePage.text;
                if (titlePattern.style.display != DisplayStyle.None)
                {
                    t += " | " + titlePattern.text;
                }

                if (titleScale.style.display != DisplayStyle.None)
                {
                    t += " | " + titleScale.text;
                }

                return t;
            }
        }

        public string SoundText => soundNote.Bold + " | " + soundNote.Small;
        public string ArrangementCaption => arrCap.text;

        /// <summary>True while there is one valid song to show (else: a message).</summary>
        public bool ShowsSong => builtValid;

        /// <summary>The notice under the top line: shown for a song asset from a newer SFX Forge.</summary>
        public SfxBanner NewerBanner => newer;

        /// <summary>
        /// What is said in place of a song: "NO SONG YET | …", "A NEWER SONG | …". "" while a song shows.
        /// </summary>
        public string MessageText { get; private set; } = "";

        /// <summary>
        /// The two lines under the keys: the link's state, or why SEND would be refused. "" while no song shows.
        /// </summary>
        public string LinkText => linkNote != null && builtValid ? linkNote.Bold + " | " + linkNote.Small : "";

        // ---- building -----------------------------------------------------------------------------------------------
        void Build()
        {
            Clear();

            // (they sat in the AUTO page that is gone now: LaneAt makes them again)
            Array.Clear(lanes, 0, lanes.Length);
            builtValid = Alive && OneValidSong;

            // the top line, and the notice for a song from a newer version
            var root = SfxUI.Panel();
            Add(root);
            header = new SfxHeader();
            root.Add(header);
            newer = new SfxBanner();
            root.Add(newer);
            ShowNewer();

            // a song that cannot be shown: words only
            MessageText = "";
            if (!builtValid)
            {
                Message(root);
                return;
            }

            if (Song.ArrangementBars > 0)
            {
                pattern = Mathf.Clamp(Song.ArrangementPattern(0), 0, Patterns - 1);
            }

            sound = Mathf.Clamp(sound, 0, Sounds - 1);

            // the title line (Refresh writes its texts)
            var title = SfxUI.Box("sfx-row");
            titlePage = SfxUI.Text("SONG", "sfx-title");
            titlePattern = SfxUI.Text("", "sfx-title");
            titlePattern.style.marginLeft = 10;
            titlePattern.style.color = SfxColors.Text;
            titleScale = SfxUI.Text("", "sfx-song-cap");
            titleScale.style.flexGrow = 1;
            titleScale.style.unityTextAlign = TextAnchor.UpperRight;
            titleScale.style.marginTop = 0;
            title.Add(titlePage);
            title.Add(titlePattern);
            title.Add(titleScale);
            root.Add(title);

            // the song's four numbers: TEMPO and SWING can be changed here, STEPS and CHAIN only show
            var values = SfxUI.Box("sfx-song-values");
            for (int i = 0; i < 4; i++)
            {
                var t = new ValueTile(i + 1, false, ValueTile.Look.Row);
                songTiles[i] = t;
                t.dragStarted += BeginDrag;
                t.dragEnded += EndDrag;
                values.Add(t);
            }

            songTiles[TileTempo].tooltip =
                "The song's tempo, 40 to 240 BPM. Drag, or double-click the value to type one.";
            songTiles[TileTempo].nudged += d => SetTempo(Song.Bpm + d);
            songTiles[TileTempo].typed += text =>
            {
                if (Number(text, out int v))
                {
                    SetTempo(v);
                }
            };
            songTiles[TileTempo].resetRequested += () => SetTempo(120);
            songTiles[TileSteps].tooltip = "Sixteenths in a bar. Set on the device: it only shows here.";
            songTiles[TileSteps].Interactive = false;
            songTiles[TileSwing].tooltip = "How far every second step waits, 0 to 100 %.";
            songTiles[TileSwing].nudged += d => SetSwing(Song.Swing + d);
            songTiles[TileSwing].typed += text =>
            {
                if (Number(text, out int v))
                {
                    SetSwing(v);
                }
            };
            songTiles[TileSwing].resetRequested += () => SetSwing(0);
            songTiles[TileChain].tooltip =
                "The patterns it walks through by itself, one bar each – unused while it has an arrangement. Set "
                + "on the device: it only shows here.";
            songTiles[TileChain].Interactive = false;
            root.Add(values);

            // the eight patterns: a click looks at one
            var patterns = SfxUI.Box("sfx-song-patterns");
            for (int k = 0; k < Patterns; k++)
            {
                int at = k;
                var cell = SfxUI.Box("sfx-song-pattern-cell");
                var tile = new PatternTile((char)('A' + k));
                patternTiles[k] = tile;
                tile.tooltip =
                    "Pattern "
                    + (char)('A' + k)
                    + ": along its foot, how many sounds play on each step. Click: look at it.";
                tile.clicked += () => Pattern = at;
                cell.Add(tile);
                patterns.Add(cell);
            }

            root.Add(patterns);

            // the arrangement: a letter a bar; a click looks at that bar's pattern
            arrCap = SfxUI.Text("", "sfx-song-cap");
            root.Add(arrCap);
            var arr = SfxUI.Box("sfx-arr");
            for (int i = 0; i < bars.Length; i++)
            {
                int bar = i;
                var b = new TextElement();
                bars[i] = b;
                b.AddToClassList("sfx-arr__bar");
                b.AddManipulator(
                    new Clickable(() =>
                    {
                        int p = Song.ArrangementPattern(bar);
                        if (p >= 0)
                        {
                            Pattern = p;
                        }
                    })
                );
                arr.Add(b);
            }

            root.Add(arr);

            // the four pages
            tabs = new PageTabs(PageNames);
            tabs.changed += i => Page = i;
            root.Add(tabs);
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i] = SfxUI.Box("sfx-song-page");
                root.Add(pages[i]);
            }

            BuildSteps(pages[PageSteps]);
            BuildMix(pages[PageMix]);
            BuildMaster(pages[PageMaster]);
            autoNone = SfxUI.Text("NO AUTOMATION   no knob moves by itself in this song", "sfx-song-cap");
            pages[PageAuto].Add(autoNone);

            // under the pages: the chosen sound's LAYER, VOLUME and PAN
            var own = SfxUI.Box("sfx-song-values");
            own.AddToClassList("sfx-song-values--three");
            for (int i = 0; i < 3; i++)
            {
                var t = new ValueTile(0, false, ValueTile.Look.Row);
                soundTiles[i] = t;
                t.dragStarted += BeginDrag;
                t.dragEnded += EndDrag;
                own.Add(t);
            }

            soundTiles[TileLayer].tooltip =
                "The chosen sound's LAYER mark: it plays while the game's intensity is at or above it. 0 %: always.";
            soundTiles[TileLayer].nudged += d => SetLayer(sound, Song.LayerRaw(sound) + d);
            soundTiles[TileLayer].typed += text =>
            {
                if (SfxUnits.TryParse(text, 0, 127, SfxUnits.Percent, out int v))
                {
                    SetLayer(sound, v);
                }
            };
            soundTiles[TileLayer].resetRequested += () => SetLayer(sound, 0);
            soundTiles[TileVolume].tooltip =
                "The chosen sound's fader in the song's mix: 100 = as designed, up to 127.";
            soundTiles[TileVolume].nudged += d => SetVolume(sound, Song.VolumeRaw(sound) + d);
            soundTiles[TileVolume].typed += text =>
            {
                if (Number(text, out int v))
                {
                    SetVolume(sound, v);
                }
            };
            soundTiles[TileVolume].resetRequested += () => SetVolume(sound, 100);
            soundTiles[TilePan].tooltip = "The chosen sound's pan: L64 .. C .. R63.";
            soundTiles[TilePan].nudged += d => SetPan(sound, Song.PanRaw(sound) + d);
            soundTiles[TilePan].typed += text =>
            {
                if (SfxUnits.TryParse(text, 0, 127, SfxUnits.PanText, out int v))
                {
                    SetPan(sound, v);
                }
            };
            soundTiles[TilePan].resetRequested += () => SetPan(sound, 64);
            root.Add(own);
            soundNote = new SfxFooter();
            root.Add(soundNote);

            // the keys, the link's state, and the note that says what is made on the device
            var keys = new SoftKeyRow();
            keys.AddToClassList("sfx-keys--four");
            playKey = keys.Add(
                "PLAY SOUND",
                () => PlaySound(sound),
                "Plays the chosen sound here, with the device's engine. A click on a sound's name or its MIX "
                    + "tile plays it too."
            );
            barKey = keys.Add(
                "PLAY BAR",
                PlayBar,
                "Plays the pattern looked at, once, as the game would at the INTENSITY set on the STEPS page."
            );
            keys.Add("STOP", SfxForgeEditorLink.StopPreview, "Silence.");
            sendKey = keys.Add("SEND", Send, "Replaces the 16 sounds and the song on the device.");
            root.Add(keys);
            linkNote = new SfxFooter();
            root.Add(linkNote);
            viewOnly = SfxUI.Text(
                "Here TEMPO, SWING and a sound's LAYER, VOLUME and PAN can be changed. Steps, patterns, the "
                    + "arrangement, the master and the lanes are made on the device: they only show.",
                "sfx-song-cap"
            );
            viewOnly.style.marginTop = 4;
            root.Add(viewOnly);

            Refresh();
        }

        // a song (or one of several that are selected) that a newer SFX
        // Forge made: say so, and what saving it here costs
        void ShowNewer()
        {
            if (!Alive)
            {
                return;
            }

            int count = 0;
            int highest = 0;
            int many = so.targetObjects.Length;
            foreach (var target in so.targetObjects)
            {
                if (target is SfxSong one && one.IsFromNewerVersion)
                {
                    count++;
                    highest = Mathf.Max(highest, one.Format);
                }
            }

            if (count == 0)
            {
                return;
            }

            string bold =
                many == 1
                    ? "FROM A NEWER SFX FORGE"
                    : (count == 1 ? "ONE OF THESE " + many + " SONGS IS" : count + " OF THESE " + many + " SONGS ARE")
                        + " FROM A NEWER SFX FORGE";
            newer.Set(
                bold,
                (many == 1 ? "This song was " : "Such a song was ")
                    + SfxAssetFormat.NewerText(highest, SfxSong.CurrentFormat)
            );
        }

        // no song to show: none yet in this asset, or several are selected
        void Message(VisualElement root)
        {
            int many = Alive ? so.targetObjects.Length : 0;
            header.Set(
                "-",
                many > 1 ? many + " SONGS"
                    : Alive ? so.targetObject.name.ToUpperInvariant()
                    : "",
                "SONG",
                SfxColors.Dim
            );
            header.HideLink();

            // a packed song with a format above ours is a newer song, not a broken one and not "none yet"
            bool newerSong = many == 1 && Song != null && Song.IsNewer;
            string bold =
                many > 1 ? "SELECT ONE SONG"
                : newerSong ? "A NEWER SONG"
                : "NO SONG YET";
            string small =
                many > 1 ? "A song is shown one at a time."
                : newerSong
                    ? "Its song is in format "
                        + Song.data[0]
                        + ", this version of SFX Forge knows up to "
                        + SfxProtocol.SongVersion
                        + ": it does not play here and nothing of it shows. It is kept as it is. Update the package "
                        + "to use it."
                : "Make one on SFX FORGE and press SEND on its SONG page (or FETCH SONG in Window > SFX Forge) – "
                    + "it arrives as an asset like this one.";
            MessageText = bold + " | " + small;
            root.Add(SfxUI.Text(bold, "sfx-song-message"));
            root.Add(SfxUI.Text(small, "sfx-song-message-small"));
        }

        void BuildSteps(VisualElement into)
        {
            for (int s = 0; s < Sounds; s++)
            {
                int at = s;
                var line = SfxUI.Box("sfx-song-row");
                var name = new TextElement();
                names[s] = name;
                name.AddToClassList("sfx-song-name");
                name.tooltip = "Sound " + (s + 1) + ". Click: choose it and play it.";
                name.AddManipulator(new Clickable(() => Choose(at)));
                rows[s] = new StepRow();
                line.Add(name);
                line.Add(rows[s]);
                into.Add(line);
            }

            // the chosen sound's notes, over its row (where there is room)
            noteLayer = SfxUI.Box("sfx-step-notes");
            noteLayer.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < Steps; i++)
            {
                noteTexts[i] = SfxUI.Text("", "sfx-step-note");
                noteLayer.Add(noteTexts[i]);
            }

            heat = new IntensityStrip
            {
                tooltip =
                    "Drag: look at the song at this intensity. A mark a sound: its LAYER – the sound plays while the "
                    + "intensity is at or right of it. (For looking and PLAY BAR only: the game sets the intensity.)",
            };
            heat.changed += v => Intensity = v;
            into.Add(Strip("INTENSITY", SfxColors.Ember, heat));
            layer = new IntensityStrip { Interactive = false };
            into.Add(Strip("LAYER", SfxColors.Dim, layer));
        }

        static VisualElement Strip(string name, Color colour, IntensityStrip strip)
        {
            var line = SfxUI.Box("sfx-song-row");
            line.AddToClassList("sfx-song-row--strip");
            var label = SfxUI.Text(name, "sfx-song-name");
            label.style.color = colour;
            label.style.marginTop = 6;
            line.Add(label);
            line.Add(strip);
            return line;
        }

        void BuildMix(VisualElement into)
        {
            var grid = SfxUI.Box("sfx-mix");
            for (int s = 0; s < Sounds; s++)
            {
                int at = s;
                var cell = SfxUI.Box("sfx-mix-cell");
                mixTiles[s] = new MixTile
                {
                    tooltip =
                        "Sound "
                        + (s + 1)
                        + ": its fader (the mark: 100, as designed) and its pan. Click: choose it and play it.",
                };
                mixTiles[s].clicked += () => Choose(at);
                cell.Add(mixTiles[s]);
                grid.Add(cell);
            }

            into.Add(grid);
        }

        void BuildMaster(VisualElement into)
        {
            master = new MasterView();
            into.Add(master);
            var values = SfxUI.Box("sfx-song-values");
            values.AddToClassList("sfx-song-values--five");
            for (int i = 0; i < masterTiles.Length; i++)
            {
                masterTiles[i] = new ValueTile(0, false, ValueTile.Look.Row)
                {
                    Interactive = false,
                    tooltip = "The song's master effects are set on the device: they only show here.",
                };
                values.Add(masterTiles[i]);
            }

            // five tiles in rows: one left over in the last row is as wide as the others, not as wide as the page
            float laidOutFor = -1f;
            values.RegisterCallback<GeometryChangedEvent>(e =>
            {
                float width = e.newRect.width;
                if (width <= 0f || Mathf.Approximately(width, laidOutFor))
                {
                    return;
                }

                laidOutFor = width;
                float least = Mathf.Max(20f, masterTiles[0].resolvedStyle.minWidth.value);
                int perRow = Mathf.Clamp((int)(width / (least + 4f)), 1, masterTiles.Length);
                foreach (var t in masterTiles)
                {
                    t.style.maxWidth = Mathf.Floor(width / perRow) - 4f;
                }
            });
            into.Add(values);
        }

        Lane LaneAt(int i)
        {
            if (lanes[i] != null)
            {
                return lanes[i];
            }

            var l = lanes[i] = new Lane { root = SfxUI.Box("sfx-lane"), row = new LaneRow() };
            var top = SfxUI.Box("sfx-row");
            l.name = SfxUI.Text("", "sfx-song-name");
            l.what = SfxUI.Text("", "sfx-lane__what");
            l.says = SfxUI.Text("", "sfx-lane__says");
            top.Add(l.name);
            top.Add(l.what);
            top.Add(l.says);
            l.root.Add(top);
            l.root.Add(l.row);
            pages[PageAuto].Add(l.root);
            return l;
        }

        // ---- what is shown ------------------------------------------------------------------------------------------

        /// <summary>A sound of the song has no name of its own: its pad's number and its engine.</summary>
        public static string SoundName(SfxSong song, int s) =>
            (s + 1).ToString(CultureInfo.InvariantCulture)
            + " "
            + SfxUnits.EngineShort[(int)song.sounds[s].engine % SfxUnits.EngineShort.Length];

        static string Letter(int p) => ((char)('A' + p)).ToString();

        /// <summary>A grid as the device names it: 16, 32, 64 steps a bar are "1/16", "1/32", "1/64".</summary>
        public static string GridName(int steps) =>
            steps == 64 ? "1/64"
            : steps == 32 ? "1/32"
            : "1/16";

        static Color Colour(SfxSong song, int s) => SfxColors.Engine(song.sounds[s].engine);

        /// <summary>Reads the song again and shows it.</summary>
        public void Refresh()
        {
            if (!Alive)
            {
                return;
            }

            if (OneValidSong != builtValid)
            {
                Build();
                return;
            }

            if (!builtValid)
            {
                return;
            }

            // what everything below shows: the song, the chosen sound's colour, the pattern looked at
            var song = Song;
            Color c = Colour(song, sound);
            int len = song.StepsPerPattern;
            int arr = song.ArrangementBars;
            int chain = song.Chain;

            // the pattern looked at: its cells, and those the bar plays of them
            int cells = Mathf.Clamp(song.Grid(pattern), Steps, StepRow.MaxSteps);
            int cellsPlayed = song.GridSteps(pattern);
            bool layerOn = intensity >= song.LayerRaw(sound);

            // the top line and the title
            header.Set(
                (sound + 1).ToString(),
                so.targetObject.name.ToUpperInvariant(),
                SfxUnits.EngineNames[(int)song.sounds[sound].engine % SfxUnits.EngineNames.Length],
                c
            );
            titlePage.text = PageTitles[page];
            titlePage.style.color = c;
            titlePattern.text = "PATTERN " + Letter(pattern) + "  " + GridName(cells);
            titlePattern.style.display = page == PageSteps || page == PageAuto ? DisplayStyle.Flex : DisplayStyle.None;
            titleScale.text = "scale " + SfxUnits.ScaleNames[song.Scale % SfxUnits.ScaleNames.Length];
            titleScale.style.display = page == PageSteps ? DisplayStyle.Flex : DisplayStyle.None;

            // the song's four numbers
            var tempoTile = songTiles[TileTempo];
            tempoTile.Show(
                "TEMPO",
                song.Bpm.ToString(CultureInfo.InvariantCulture) + " BPM",
                (song.Bpm - 40) / 200f,
                false,
                SfxColors.Pattern
            );
            var stepsTile = songTiles[TileSteps];
            stepsTile.Show(
                "STEPS",
                len.ToString(CultureInfo.InvariantCulture),
                len / (float)Steps,
                false,
                SfxColors.Pattern
            );
            var swingTile = songTiles[TileSwing];
            swingTile.Show(
                "SWING",
                song.Swing.ToString(CultureInfo.InvariantCulture) + "%",
                song.Swing / 100f,
                false,
                SfxColors.Pattern
            );
            var chainTile = songTiles[TileChain];
            chainTile.Show(
                arr > 0 ? "CHAIN (unused)" : "CHAIN",
                chain <= 1 ? "A" : "A-" + Letter(chain - 1),
                chain / (float)Patterns,
                false,
                arr > 0 ? SfxColors.Dim : SfxColors.Pattern
            );

            // the patterns, and the arrangement
            uint used = 0; // the sounds that play anywhere in the song
            for (int p = 0; p < Patterns; p++)
            {
                int div = Mathf.Max(1, song.StepsPerSixteenth(p));

                // a column a sixteenth: the sounds set anywhere in it (a finer pattern has steps between)
                for (int i = 0; i < Steps; i++)
                {
                    counts[i] = 0;
                    for (int s = 0; s < Sounds; s++)
                    {
                        for (int j = 0; j < div; j++)
                        {
                            if (song.GridStepOn(p, s, i * div + j))
                            {
                                counts[i]++;
                                used |= 1u << s;
                                break;
                            }
                        }
                    }
                }

                var patternTile = patternTiles[p];
                patternTile.Set(
                    counts,
                    p == pattern ? PatternState.Selected
                        : p < chain || arr > 0 ? PatternState.Used
                        : PatternState.Unused,
                    song.Grid(p)
                );
            }

            arrCap.text =
                arr > 0 ? "ARRANGEMENT   " + arr + (arr == 1 ? " bar" : " bars") + ", then again from the top"
                : chain <= 1 ? "NO ARRANGEMENT   it stays on one pattern"
                : "NO ARRANGEMENT   it walks its chain A-" + Letter(chain - 1) + ", a bar each";
            for (int i = 0; i < bars.Length; i++)
            {
                int p = song.ArrangementPattern(i);
                bars[i].style.display = p >= 0 ? DisplayStyle.Flex : DisplayStyle.None;
                if (p < 0)
                {
                    continue;
                }

                bars[i].text = Letter(p);
                bars[i].tooltip = "Bar " + (i + 1) + ": pattern " + Letter(p);
                bars[i].EnableInClassList("sfx-arr__bar--on", p == pattern);
            }

            // the pages: only the chosen one shows
            tabs.Accent = c;
            tabs.SetIndex(page);
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i].style.display = i == page ? DisplayStyle.Flex : DisplayStyle.None;
            }

            // STEPS: a row a sound – dim while its layer is silent at this intensity, or it is muted
            for (int s = 0; s < Sounds; s++)
            {
                Color sc = Colour(song, s);
                bool muted = song.Muted(s);
                bool on = intensity >= song.LayerRaw(s) && !muted;
                bool sel = s == sound;
                names[s].text = SoundName(song, s);
                names[s].style.backgroundColor = sel ? new StyleColor(sc) : new StyleColor(StyleKeyword.Null);
                names[s].style.color =
                    sel ? SfxColors.Bg
                    : muted ? SfxColors.Hex(0x58433A)
                    : on && (used >> s & 1) != 0 ? sc
                    : SfxColors.Mix(SfxColors.Bg, sc, 0.55f);
                names[s].EnableInClassList("sfx-song-name--on", sel);
                var row = rows[s];
                for (int i = 0; i < StepRow.MaxSteps; i++)
                {
                    bool set = i < cells && song.GridStepOn(pattern, s, i);

                    // as high as it is hard
                    row.Level[i] = set ? (6 + song.GridStepVelocity(pattern, s, i) * 2) / 20f : 0f;
                    row.Hold[i] = (byte)(set ? song.GridStepLength(pattern, s, i) : 0);
                }

                row.Count = cells;
                row.Length = cellsPlayed;
                row.Accent = sc;
                row.Dimmed = !on;
                row.Selected = sel;
                row.Changed();
                heat.SetMark(s, (used >> s & 1) != 0 ? song.LayerRaw(s) : -1, sc);
                mixTiles[s].Set(SoundName(song, s), sc, song.VolumeRaw(s), song.PanRaw(s), muted, sel);
            }

            if (noteLayer.parent != rows[sound])
            {
                rows[sound].Add(noteLayer);
            }

            for (int i = 0; i < Steps; i++)
            {
                // (written in the cells at 16 steps only: a finer cell has no room)
                int note = cells == Steps && song.StepOn(pattern, sound, i) ? song.StepNote(pattern, sound, i) : 0;
                noteTexts[i].text = note > 0 ? SfxUnits.NoteText(note) : "";
                noteTexts[i].style.color = layerOn && !song.Muted(sound) ? SfxColors.Bg : SfxColors.Text;
            }

            heat.Value = intensity;
            heat.Text = SfxUnits.Percent(intensity);
            layer.Value = song.LayerRaw(sound);
            layer.Fill = SfxColors.Mix(SfxColors.Bg, c, layerOn ? 0.75f : 0.35f);
            layer.Text = "SOUND " + (sound + 1) + " plays from " + SfxUnits.Percent(song.LayerRaw(sound));

            // MASTER
            int time = song.Master(SfxMaster.DelayTime);
            int fb = song.Master(SfxMaster.DelayFeedback);
            int size = song.Master(SfxMaster.ReverbSize);
            int damp = song.Master(SfxMaster.ReverbDamping);
            int drive = song.Master(SfxMaster.Drive);
            Color room = SfxColors.Engines[(int)SfxEngine.Wavetable];
            master.Set(time, fb, size, damp);
            masterTiles[0]
                .Show("DELAY TIME", SfxUnits.DelayTimeText(time), (time % 6 + 0.5f) / 6f, false, SfxColors.Pattern);
            masterTiles[1].Show("FEEDBACK", SfxUnits.Percent(fb), fb / 127f, false, SfxColors.Pattern);
            masterTiles[2].Show("ROOM SIZE", SfxUnits.Percent(size), size / 127f, false, room);
            masterTiles[3].Show("DAMPING", SfxUnits.Percent(damp), damp / 127f, false, room);
            masterTiles[4].Show("DRIVE", SfxUnits.Percent(drive), drive / 127f, false, SfxColors.Ember);

            // AUTO
            int shown = ShowLanes(song, cells, cellsPlayed);
            autoNone.style.display = shown == 0 ? DisplayStyle.Flex : DisplayStyle.None;

            // the chosen sound
            bool mix = song.HasMix;
            var layerTile = soundTiles[TileLayer];
            layerTile.Show(
                "LAYER",
                SfxUnits.Percent(song.LayerRaw(sound)),
                song.LayerRaw(sound) / 127f,
                false,
                layerOn ? c : SfxColors.Dim
            );
            var volumeTile = soundTiles[TileVolume];
            volumeTile.Show(
                "VOLUME",
                song.VolumeRaw(sound).ToString(CultureInfo.InvariantCulture),
                song.VolumeRaw(sound) / 127f,
                false,
                c
            );
            soundTiles[TilePan].Show("PAN", SfxUnits.PanText(song.PanRaw(sound)), song.PanRaw(sound) / 127f, true, c);
            soundTiles[TileVolume].Interactive = mix;
            soundTiles[TilePan].Interactive = mix;
            soundNote.Set(
                "SOUND "
                    + (sound + 1)
                    + "   "
                    + SfxUnits.EngineNames[(int)song.sounds[sound].engine % SfxUnits.EngineNames.Length]
                    + (song.Muted(sound) ? "   MUTED" : ""),
                c,
                NotesText(song)
            );

            UpdateLink();
        }

        // the AUTO page: a block a lane that moves a knob. Returns how many there are.
        // grid: the cells of the pattern looked at (16, 32 or 64). len: those of them the bar plays.
        int ShowLanes(SfxSong song, int grid, int len)
        {
            int n = 0;
            for (int l = 0; l < SfxSong.Lanes; l++)
            {
                int s = song.LaneSound(l);
                int to = song.LaneTarget(l);
                if (s < 0 || s >= Sounds || to <= 0 || to >= SfxAutoTarget.Count)
                {
                    continue;
                }

                var lane = LaneAt(n++);
                Color sc = Colour(song, s);
                lane.root.style.display = DisplayStyle.Flex;
                lane.name.text = SoundName(song, s);
                lane.name.style.color = sc;
                lane.what.text = SfxUnits.AutoName(to, song.sounds[s].engine);
                int points = 0;
                int low = int.MaxValue;
                int high = -1;
                for (int i = 0; i < LaneRow.MaxSteps; i++)
                {
                    int v = i < grid ? song.GridLanePoint(pattern, l, i) : -1;
                    lane.row.Point[i] = v;
                    lane.row.Plays[i] = i < grid && song.GridStepOn(pattern, s, i);
                    if (v < 0)
                    {
                        continue;
                    }

                    points++;
                    if (v < low)
                    {
                        low = v;
                    }

                    if (v > high)
                    {
                        high = v;
                    }
                }

                lane.row.Count = grid;
                lane.row.Hi = SfxUnits.AutoHi(to);
                lane.row.Length = len;
                lane.row.Accent = sc;
                lane.row.Changed();
                if (points > 0)
                {
                    lane.says.text =
                        Letter(pattern)
                        + ": "
                        + points
                        + (points == 1 ? " point, " : " points, ")
                        + (
                            low == high
                                ? SfxUnits.AutoText(to, low)
                                : SfxUnits.AutoText(to, low) + " to " + SfxUnits.AutoText(to, high)
                        );
                }
                else
                {
                    string where = "";
                    for (int p = 0; p < Patterns; p++)
                    {
                        for (int i = 0, n2 = song.Grid(p); i < n2; i++)
                        {
                            if (song.GridLanePoint(p, l, i) >= 0)
                            {
                                where += " " + Letter(p);
                                break;
                            }
                        }
                    }

                    lane.says.text =
                        "no points in " + Letter(pattern) + (where.Length > 0 ? " – it moves in" + where : "");
                }
            }

            for (int i = n; i < lanes.Length; i++)
            {
                if (lanes[i] != null)
                {
                    lanes[i].root.style.display = DisplayStyle.None;
                }
            }

            return n;
        }

        // the chosen sound's steps in the pattern looked at, as the SEQ page names them: a note, x (as designed), ·
        // (rests). A pattern of 32 or 64 steps: only the steps that are set, each with its number - "pattern B (1/32):
        // 1 E2 · 5 x · 9 G2"
        string NotesText(SfxSong song)
        {
            int grid = song.Grid(pattern);
            int set = 0;
            var t = new StringBuilder("pattern ").Append(Letter(pattern));
            if (grid > Steps)
            {
                const int Most = 16;
                t.Append(" (").Append(GridName(grid)).Append("):  ");
                for (int i = 0, n = song.GridSteps(pattern); i < n; i++)
                {
                    if (!song.GridStepOn(pattern, sound, i))
                    {
                        continue;
                    }

                    if (++set > Most)
                    {
                        continue;
                    }

                    if (set > 1)
                    {
                        t.Append(" · ");
                    }

                    int note = song.GridStepNote(pattern, sound, i);
                    t.Append(i + 1).Append(' ').Append(note > 0 ? SfxUnits.NoteText(note) : "x");
                }

                if (set > Most)
                {
                    t.Append(" · +").Append(set - Most).Append(" more");
                }
            }
            else
            {
                t.Append(":  ");
                for (int i = 0; i < song.StepsPerPattern; i++)
                {
                    if (i > 0)
                    {
                        t.Append(' ');
                    }

                    if (!song.StepOn(pattern, sound, i))
                    {
                        t.Append('·');
                        continue;
                    }

                    set++;
                    int note = song.StepNote(pattern, sound, i);
                    t.Append(note > 0 ? SfxUnits.NoteText(note) : "x");
                }
            }

            if (set == 0)
            {
                return "pattern " + Letter(pattern) + ":  it rests";
            }

            int lanesOfIt = song.AutomationLanes(sound);
            if (lanesOfIt > 0)
            {
                t.Append(lanesOfIt == 1 ? "   · 1 lane" : "   · " + lanesOfIt + " lanes");
            }

            return t.ToString();
        }

        /// <summary>
        /// What the line under the keys says of the link, and whether SEND is off: for a link that has an output or
        /// not, what the device said of itself (null: nothing) and the packed song. SEND is off without an output, and
        /// for a song this device cannot take (SfxLink.Refusal says why: a format-6 song and a firmware before 1.6, a
        /// value out of range - then the bold word is A DAMAGED SONG, else ANOTHER FORMAT).
        /// </summary>
        public static bool LinkState(
            bool linked,
            string port,
            SfxDeviceInfo? device,
            byte[] song,
            out string bold,
            out string small,
            out Color colour
        )
        {
            string refusal = linked ? SfxLink.Refusal(device, song) : null;
            if (refusal != null)
            {
                bold =
                    SfxPackedSong.IsValid(song) && !SfxPackedSong.IsPlayable(song)
                        ? "A DAMAGED SONG"
                        : "ANOTHER FORMAT";
                colour = SfxColors.Warn;
                small = "SEND is off: " + refusal + ".";
            }
            else if (linked)
            {
                bold = "LINKED";
                colour = SfxColors.Linked;
                small = "SEND replaces the 16 sounds and the song on " + port;
            }
            else
            {
                bold = "NOT CONNECTED";
                colour = SfxColors.Dim;
                small = "Choose the MIDI ports in Window > SFX Forge.";
            }

            return !linked || refusal != null;
        }

        void UpdateLink()
        {
            if (!builtValid || !Alive)
            {
                return;
            }

            bool linked = SfxLink.Shared.HasOutput;
            header.SetLink(linked, linked ? "Linked: " + SfxLink.Shared.OutputName : "Not connected");
            sendKey.Off = LinkState(
                linked,
                SfxLink.Shared.OutputName,
                SfxLink.Shared.Device,
                Song != null ? Song.data : null,
                out string bold,
                out string small,
                out Color colour
            );
            linkNote.Set(bold, colour, small);
            playKey.Off = !NativeThere;
            barKey.Off = !NativeThere;
        }

        // ---- changing the five numbers: through the SerializedObject, one undo step (a whole drag is one too) -------
        void BeginDrag()
        {
            Undo.IncrementCurrentGroup();
            dragGroup = Undo.GetCurrentGroup();
        }

        void EndDrag() => dragGroup = -1;

        bool Write(int at, int value, int at2 = -1, int value2 = 0)
        {
            if (!Alive || !OneValidSong)
            {
                return false;
            }

            if (dragGroup < 0)
            {
                Undo.IncrementCurrentGroup();
            }

            so.Update();
            if (at < 0 || at >= dataProp.arraySize || at2 >= dataProp.arraySize)
            {
                return false;
            }

            var first = dataProp.GetArrayElementAtIndex(at);
            var second = at2 >= 0 ? dataProp.GetArrayElementAtIndex(at2) : null;
            if (first.intValue == value && (second == null || second.intValue == value2))
            {
                return false;
            }

            first.intValue = value;
            if (second != null)
            {
                second.intValue = value2;
            }

            if (!so.ApplyModifiedProperties())
            {
                return false;
            }

            if (dragGroup >= 0)
            {
                Undo.CollapseUndoOperations(dragGroup);
            }

            Undo.SetCurrentGroupName(UndoEdit);
            Refresh();
            return true;
        }

        static bool Number(string text, out int value)
        {
            value = 0;
            if (!SfxUnits.Magnitude(text, out double m))
            {
                return false;
            }

            value = (int)Math.Round(m);
            return true;
        }

        /// <summary>The tempo: 40..240 BPM.</summary>
        public bool SetTempo(int bpm)
        {
            bpm = Mathf.Clamp(bpm, 40, 240);
            return Write(AtBpm, bpm >> 7, AtBpm + 1, bpm & 127);
        }

        /// <summary>The swing: 0..100.</summary>
        public bool SetSwing(int swing) => Write(AtSwing, Mathf.Clamp(swing, 0, 100));

        /// <summary>A sound's LAYER mark: 0..127 (0 = it always plays).</summary>
        public bool SetLayer(int s, int level127) =>
            s >= 0 && s < Sounds && Write(AtLayer + s, Mathf.Clamp(level127, 0, 127));

        /// <summary>A sound's fader: 0..127, 100 = as designed. (A song from before the mixer has none.)</summary>
        public bool SetVolume(int s, int volume127) =>
            s >= 0
            && s < Sounds
            && Alive
            && OneValidSong
            && Song.HasMix
            && Write(AtVolume + s, Mathf.Clamp(volume127, 0, 127));

        /// <summary>A sound's pan: 0 left .. 64 the middle .. 127 right.</summary>
        public bool SetPan(int s, int pan127) =>
            s >= 0
            && s < Sounds
            && Alive
            && OneValidSong
            && Song.HasMix
            && Write(AtPan + s, Mathf.Clamp(pan127, 0, 127));

        // ---- the keys -----------------------------------------------------------------------------------------------
        void Choose(int s)
        {
            Sound = s;
            PlaySound(s);
        }

        void PlaySound(int s)
        {
            if (Alive && OneValidSong && NativeThere)
            {
                SfxForgeEditorLink.Preview(Song.sounds[s]);
            }
        }

        void PlayBar()
        {
            if (!Alive || !OneValidSong || !NativeThere)
            {
                return;
            }

            float[] samples = RenderBar(Song, pattern, intensity / 127f);
            if (samples != null)
            {
                SfxForgeEditorLink.PreviewSamples(samples, 48000);
            }
        }

        void Send()
        {
            // (a song this device cannot take is refused there, in words: UpdateLink shows them)
            if (Alive && OneValidSong && SfxLink.Shared.HasOutput)
            {
                SfxLink.Shared.SendSong(Song.sounds, Song.data);
            }
        }

        /// <summary>
        /// One bar of a pattern as the game would play it at this intensity (0..1), with the song's mix and effects,
        /// and what rings after it: stereo samples, L R L R … Null: no native library, or not a song.
        /// </summary>
        public static float[] RenderBar(SfxSong song, int pattern, float intensity01, int sampleRate = 48000)
        {
            if (song == null || !song.IsValid || !SfxNative.Available)
            {
                return null;
            }

            const int Chunk = 1024;

            // (a step is a sixteenth; it ends just before the next bar's first step)
            int bar = Mathf.Max(Chunk, (int)(song.StepsPerPattern * 15.0 / Mathf.Max(40, song.Bpm) * sampleRate) - 64);
            int max = bar + sampleRate * 4;
            int n = 0;
            var both = new float[max * 2];
            float[] l = new float[Chunk];
            float[] r = new float[Chunk];
            IntPtr memory = Marshal.AllocHGlobal(SfxNative.PlayerSize);
            try
            {
                SfxNative.PlayerInit(memory, sampleRate);
                for (int i = 0; i < Sounds; i++)
                {
                    SfxNative.PlayerSetPatch(memory, i, song.sounds[i].Pack());
                }

                if (!SfxNative.PlayerSetSong(memory, song.data))
                {
                    return null;
                }

                var mods = SfxMods.None;
                SfxNative.PlayerSetLive(memory, ref mods, Mathf.Clamp01(intensity01));
                SfxNative.PlayerStart(memory, Mathf.Clamp(pattern, 0, Patterns - 1));
                while (n < max)
                {
                    int take = Mathf.Min(Chunk, (n < bar ? bar : max) - n);
                    Array.Clear(l, 0, take);
                    Array.Clear(r, 0, take);
                    uint sounding = SfxNative.PlayerRender(memory, l, r, take);
                    for (int i = 0; i < take; i++)
                    {
                        both[(n + i) * 2] = l[i];
                        both[(n + i) * 2 + 1] = r[i];
                    }

                    n += take;

                    // the bar is over: what is held lets go, the effects ring out
                    if (n == bar)
                    {
                        SfxNative.PlayerStop(memory);
                    }
                    else if (n > bar && sounding == 0)
                    {
                        break;
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(memory);
            }

            Array.Resize(ref both, n * 2);
            return both;
        }
    }
}
