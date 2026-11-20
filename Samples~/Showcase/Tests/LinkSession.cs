using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using CatSplat.SfxForge;
using NUnit.Framework;
using SfxShowcase.Audio;
using SfxShowcase.Core;
using SfxShowcase.Level;
using UnityEngine;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;

namespace SfxShowcase.Tests
{
    /// <summary>
    /// THE LINK, RECORDED: the showcase plays while SFX FORGE runs in the emulator on the other side of two MIDI ports,
    /// and both ends are seen doing it. Like a capture take (<see cref="CaptureTakes"/>) this writes pictures, sound and
    /// telemetry.json into <c>out/unity-video/link-session/</c>; unlike one it runs at the speed of a clock on the wall,
    /// because the device on the other side lives in real time. link.json beside them says, with the wall clock of every
    /// frame, what went over the ports.
    ///
    /// What happens, in the order of the take:
    ///  • the game names itself and every section it enters on the device's top line (<c>SfxLink.SendText</c>);
    ///  • the game's intensity moves the device's music, every frame (<c>SfxLink.SendIntensity</c>);
    ///  • SEND is pressed on the device's SONG page: the song arrives (<c>SfxLink.SongReceived</c>) and the game's
    ///    music player is given it;
    ///  • a sound is changed on the device (MUTATE) and EXPORT is pressed: the patch arrives
    ///    (<c>SfxLink.PatchReceived</c>) and the game plays it, three notes.
    ///
    /// The keys of the device are pressed by tests/emulator-link.mjs, which reads the lines this take appends to the cue
    /// file (out/link/cues.txt; another place: SFXFORGE_LINK_CUES). Start that script first, with --cues and --frames:
    /// see docs/video/make_link_video.py. Without the ports the take ends as "inconclusive".
    /// </summary>
    [Category("Link")]
    public sealed class LinkSession : ShowcaseTestBase
    {
        const int TakeTimeoutMilliseconds = 30 * 60 * 1000;
        const string FolderVariable = "SFXFORGE_CAPTURE_DIR", CuesVariable = "SFXFORGE_LINK_CUES";
        const string InVariable = "SFXFORGE_LINK_IN", OutVariable = "SFXFORGE_LINK_OUT";
        const float AnswerSeconds = 12f;

        CaptureRecorder recorder;
        CaptureTelemetry telemetry;
        SfxLink link;
        string cueFile;
        readonly Stopwatch wall = new Stopwatch();
        readonly List<double> frameWallClock = new List<double>();
        readonly List<string> linkEvents = new List<string>();
        int framesRolled;

        static string CaptureFolder => ShowcaseFolders.CaptureFolder(FolderVariable);

        [TearDown]
        public void PutTheRigAway()
        {
            GameEvents.SectionEntered -= OnSectionEntered;
            telemetry?.Dispose();
            telemetry = null;
            if (recorder != null) UnityEngine.Object.Destroy(recorder.gameObject);
            recorder = null;
            if (link != null) link.Dispose();
            link = null;
        }

        static double UnixSeconds => (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

        /// <summary>Writes down something that went over the ports, with the frame it belongs to.</summary>
        void Note(string direction, string what)
        {
            linkEvents.Add(string.Format(CultureInfo.InvariantCulture, "{{\"f\":{0},\"wall\":{1:0.000},\"direction\":\"{2}\",\"what\":\"{3}\"}}",
                framesRolled, UnixSeconds, direction, what.Replace("\\", "\\\\").Replace("\"", "\\\"")));
            Debug.Log($"LINK SESSION  f={framesRolled}  {direction}  {what}");
        }

        /// <summary>Asks the script on the device's side to press a key there (or "wait:seconds").</summary>
        void Cue(string keys)
        {
            File.AppendAllText(cueFile, keys + "\n");
            Note("cue", keys);
        }

        void Say(string text)
        {
            if (link.SendText(text)) Note("game to device", "text \"" + text + "\"");
        }

        void OnSectionEntered(SectionId section) => Say(Sections.Get(section).DisplayName.ToUpperInvariant());

        /// <summary>
        /// One frame of the take: it lasts a thirtieth of a second on the wall clock too (the recorder alone would run
        /// as fast as the machine can), the link is polled, and the game's intensity goes to the device.
        /// </summary>
        IEnumerator Frames(Func<bool> until, float seconds, MusicDirector director)
        {
            float end = Time.time + seconds;
            while (Time.time < end && !until())
            {
                double due = (framesRolled + 1) / (double)CaptureRecorder.FramesPerSecond;
                while (wall.Elapsed.TotalSeconds < due) Thread.Sleep(1);
                link.Poll();
                if (director != null && link.SendIntensity(director.Intensity) && framesRolled % 15 == 0)
                    Note("game to device", "intensity " + director.Intensity.ToString("0.00", CultureInfo.InvariantCulture));
                frameWallClock.Add(UnixSeconds);
                framesRolled++;
                yield return null;
            }
        }

        [UnityTest, Explicit, Timeout(TakeTimeoutMilliseconds)]
        public IEnumerator TheDeviceAndTheGame()
        {
            // ---- the ports ----
            string wantedIn = Environment.GetEnvironmentVariable(InVariable), wantedOut = Environment.GetEnvironmentVariable(OutVariable);
            if (string.IsNullOrEmpty(wantedIn)) wantedIn = LinkTakes.FromDevice;
            if (string.IsNullOrEmpty(wantedOut)) wantedOut = LinkTakes.ToDevice;
            string[] inputs = MidiPorts.Inputs(), outputs = MidiPorts.Outputs();
            if (SfxLink.Find(inputs, wantedIn) < 0 || SfxLink.Find(outputs, wantedOut) < 0)
                Assert.Inconclusive($"the link needs a MIDI input named like \"{wantedIn}\" and an output named like \"{wantedOut}\"; inputs: [{string.Join(", ", inputs)}], outputs: [{string.Join(", ", outputs)}]");
            if (!SfxNative.Available) Assert.Inconclusive("the native library is not here: there would be nothing to hear");
            link = new SfxLink();
            Assert.IsTrue(link.Open(wantedIn, wantedOut), "both ports open");

            cueFile = Environment.GetEnvironmentVariable(CuesVariable);
            if (string.IsNullOrEmpty(cueFile)) cueFile = Path.Combine(CaptureFolder, "..", "link", "cues.txt");
            cueFile = Path.GetFullPath(cueFile);
            Directory.CreateDirectory(Path.GetDirectoryName(cueFile));

            var music = UnityEngine.Object.FindFirstObjectByType<SfxMusicPlayer>();
            var director = UnityEngine.Object.FindFirstObjectByType<MusicDirector>();
            var follow = UnityEngine.Object.FindFirstObjectByType<FollowCamera>();
            Assert.IsNotNull(music, "the scene has its music player");
            Assert.IsNotNull(director, "the scene has its music director");
            Assert.IsNotNull(follow, "the scene has its camera");

            byte[] songIn = null;
            SfxPatch[] soundsIn = null;
            int patchSlot = -1;
            SfxPatch patchIn = default;
            int messagesIn = 0;
            var incoming = new List<byte[]>();
            link.MessageReceived += m => { messagesIn++; incoming.Add(m); };
            link.SongReceived += (song, sounds) => { songIn = song; soundsIn = sounds; };
            link.PatchReceived += (slot, patch) => { patchSlot = slot; patchIn = patch; };

            // ---- the rig ----
            recorder = CaptureRecorder.Create(follow.GetComponent<Camera>(), CaptureFolder, "link-session");
            yield return null;
            yield return null;
            telemetry = new CaptureTelemetry(player, session);
            recorder.BeginRecording(telemetry);
            wall.Restart();
            GameEvents.SectionEntered += OnSectionEntered;

            // ---- the title card: the game says hello, the device goes to its SONG page ----
            Say("BRIO: FIRST HARMONIC");
            Cue("K5");
            Cue("K5");
            yield return Frames(() => false, 3f, director);

            // ---- the run: section names and the intensity go to the device ----
            input.JumpHeld = true;
            yield return Frames(() => session.Phase == GamePhase.Playing, 0.5f, director);
            Assert.AreEqual(GamePhase.Playing, session.Phase, "Jump leaves the title card");
            var driver = new GameObject("Capture Pilot").AddComponent<CapturePilot>();
            driver.Pilot = new RunPilot(player, input, Route("JumpStretches"), Route("TuckStretches"));
            yield return Frames(() => false, 9f, director);

            // ---- SEND on the device: its song arrives and the game plays it ----
            int before = messagesIn;
            Cue("F5");
            yield return Frames(() => songIn != null, AnswerSeconds, director);
            Assert.IsNotNull(songIn, $"the device's song arrived within {AnswerSeconds} s of SEND ({messagesIn - before} messages came in: {LinkTakes.Describe(incoming)})");
            Assert.IsNotNull(soundsIn, "with all 16 sounds");
            var received = ScriptableObject.CreateInstance<SfxSong>();
            received.Set(soundsIn, songIn);
            Assert.IsTrue(received.IsValid, "what arrived is a song");
            music.song = received;
            music.Reload();
            Note("device to game", $"the song: {songIn.Length} bytes and 16 sounds in {messagesIn - before} messages; the game's music player has it");
            Say("GOT THE SONG");
            yield return Frames(() => false, 7f, director);

            // ---- a sound changed on the device, EXPORT: the patch arrives and the game plays it ----
            patchSlot = -1;                                          // (the song's 16 sounds came as patches too)
            Cue("K1");
            Cue("wait:1");
            Cue("F2");
            Cue("wait:1.5");
            Cue("F5");
            yield return Frames(() => patchSlot >= 0, AnswerSeconds, director);
            Assert.GreaterOrEqual(patchSlot, 0, $"the exported patch arrived within {AnswerSeconds} s");
            Note("device to game", $"a patch: pad {patchSlot + 1}, engine {patchIn.engine}; the game plays it");
            var voice = new GameObject("Received Patch").AddComponent<SfxPlayer>();
            voice.volume = 0.25f;
            int[] notes = { Pitch.E4, Pitch.G4, Pitch.B4 };
            foreach (int note in notes)
            {
                voice.Release();                                     // (it may be a held sound)
                yield return Frames(() => false, 0.45f, director);
                voice.Play(patchIn, 0.9f, 0f, note);
            }
            yield return Frames(() => false, 0.6f, director);
            voice.Release();
            Say("GOT PAD " + (patchSlot + 1));
            yield return Frames(() => false, 5f, director);

            driver.Pilot = null;
            UnityEngine.Object.Destroy(driver.gameObject);
            input.Release();
            Say("THANKS");
            Cue("quit-after:4");
            yield return Frames(() => false, 1f, director);

            // ---- wrap ----
            string folder = recorder.TakeFolder;
            CaptureRecorder.Summary summary = recorder.Finish();
            recorder = null;
            telemetry.Write(folder, summary);
            telemetry.Dispose();
            telemetry = null;
            WriteLinkJson(folder);
            UnityEngine.Object.Destroy(voice.gameObject);

            Assert.Greater(summary.Frames, 0, "the take has pictures");
            Assert.AreEqual(summary.PictureSeconds, summary.AudioSeconds, 0.03f, "the wav is as long as the pictures");
            Assert.Less(summary.Peak / CaptureRecorder.ListenerVolume, CaptureRecorder.MixCeiling, "the game's mix, at full volume, stays under full scale");
            double realSeconds = wall.Elapsed.TotalSeconds;
            Assert.AreEqual(summary.PictureSeconds, realSeconds, 0.05 * realSeconds + 1.0, "the take ran at the wall clock's speed, so both ends of the link are on one time line");
        }

        void WriteLinkJson(string folder)
        {
            var json = new StringBuilder("{\n\"take\":\"link-session\",\n\"notes\":[\"wall is Unix time in seconds, read when the frame began; frame f of the take is picture f%05d.png.\",\"direction: 'game to device', 'device to game', or 'cue' (a key the take asked tests/emulator-link.mjs to press on the device).\"],\n\"frameWallClock\":[");
            for (int i = 0; i < frameWallClock.Count; i++)
                json.Append(i > 0 ? "," : "").Append(frameWallClock[i].ToString("0.000", CultureInfo.InvariantCulture));
            json.Append("],\n\"events\":[\n").Append(string.Join(",\n", linkEvents)).Append("\n]\n}\n");
            File.WriteAllText(Path.Combine(folder, "link.json"), json.ToString());
        }

        static RunPilot.Stretch[] Route(string table)
        {
            FieldInfo field = typeof(ShowcaseTestBase).GetField(table, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"ShowcaseTestBase has its {table}");
            return (RunPilot.Stretch[])field.GetValue(null);
        }
    }
}
