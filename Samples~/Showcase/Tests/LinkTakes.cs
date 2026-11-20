using System;
using System.Collections;
using System.Collections.Generic;
using CatSplat.SfxForge;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SfxShowcase.Tests
{
    /// <summary>
    /// THE LINK, END TO END: Unity on one side of two MIDI ports, SFX FORGE on the other (the emulator in Chrome or
    /// Edge, the plugin, or the hardware). These tests need that other side to be there, so they never run by
    /// themselves: they are explicit, and without the ports they end as "inconclusive" with the list of ports found.
    ///
    /// What must be set up first (docs/UNITY.md, "From the device to the game"):
    ///  • two MIDI ports, one each way. Without the hardware: a virtual MIDI cable (for example loopMIDI) with two
    ///    ports. Their names are searched for <see cref="FromDevice"/> and <see cref="ToDevice"/>; other names go in
    ///    the environment variables SFXFORGE_LINK_IN and SFXFORGE_LINK_OUT;
    ///  • SFX FORGE running with its MIDI out on the first port and its MIDI in on the second.
    ///
    ///     Unity -batchmode -projectPath unity/Demo -runTests -testPlatform PlayMode
    ///           -testFilter SfxShowcase.Tests.LinkTakes -testResults out/link.xml -logFile out/link.log
    ///
    /// Every step that worked writes a line starting with "LINK: ok" to the log.
    /// </summary>
    [Category("Link")]
    public sealed class LinkTakes
    {
        /// <summary>Part of the name of the port the device sends on, unless SFXFORGE_LINK_IN names another.</summary>
        public const string FromDevice = "SFX to Unity";
        /// <summary>Part of the name of the port the device listens on, unless SFXFORGE_LINK_OUT names another.</summary>
        public const string ToDevice = "Unity to SFX";

        const string InVariable = "SFXFORGE_LINK_IN", OutVariable = "SFXFORGE_LINK_OUT";
        const float AnswerSeconds = 15f;
        const float PieceSeconds = 0.1f;                    // between two pieces of a song sent one by one
        const float ForgetSeconds = 2.5f;                   // a little more than the device's two seconds for a transfer that broke off

        SfxLink link;
        readonly List<byte[]> messages = new List<byte[]>();
        byte[] restoreSong;                                 // the device's own song and sounds while a test has put others there
        SfxPatch[] restoreSounds;

        [SetUp]
        public void Open()
        {
            string wantedIn = Environment.GetEnvironmentVariable(InVariable);
            string wantedOut = Environment.GetEnvironmentVariable(OutVariable);
            if (string.IsNullOrEmpty(wantedIn)) wantedIn = FromDevice;
            if (string.IsNullOrEmpty(wantedOut)) wantedOut = ToDevice;

            string[] inputs = MidiPorts.Inputs(), outputs = MidiPorts.Outputs();
            string found = $"inputs: [{string.Join(", ", inputs)}], outputs: [{string.Join(", ", outputs)}]";
            if (SfxLink.Find(inputs, wantedIn) < 0 || SfxLink.Find(outputs, wantedOut) < 0)
                Assert.Inconclusive($"the link needs a MIDI input named like \"{wantedIn}\" and an output named like \"{wantedOut}\"; {found}");

            link = new SfxLink();
            Assert.IsTrue(link.Open(wantedIn, wantedOut), $"both ports open ({found}); another program may hold one of them");
            messages.Clear();
            link.MessageReceived += messages.Add;
            Debug.Log($"LINK: ok ports open: from the device \"{link.InputName}\", to the device \"{link.OutputName}\"");
        }

        [TearDown]
        public void Close()
        {
            if (link != null && restoreSong != null && restoreSounds != null)
            {
                bool sent = link.SendSong(restoreSounds, restoreSong);   // a test that failed half way: the device's own song back
                System.Threading.Thread.Sleep(1500);
                Debug.Log(sent ? "LINK: the device's own song and sounds were sent back after a failed test" : "LINK: the device's own song could NOT be sent back: it has what the failed test left");
            }
            restoreSong = null; restoreSounds = null;
            if (link != null) link.Dispose();
            link = null;
        }

        /// <summary>Polls the link every frame until the condition holds or the time is up (real time).</summary>
        IEnumerator Until(Func<bool> done, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!done() && Time.realtimeSinceStartup < end)
            {
                link.Poll();
                yield return null;
            }
        }

        /// <summary>Unity asks for a pad's patch; the device answers with it.</summary>
        [UnityTest, Explicit]
        public IEnumerator TheDeviceAnswersWithAPatch()
        {
            const int pad = 0;
            int gotSlot = -1;
            SfxPatch got = default;
            link.PatchReceived += (slot, patch) => { gotSlot = slot; got = patch; };

            Assert.IsTrue(link.SendText("UNITY: PATCH?"), "text goes out");
            Assert.IsTrue(link.RequestPatch(pad), "the request goes out");
            yield return Until(() => gotSlot >= 0, AnswerSeconds);

            Assert.AreEqual(pad, gotSlot, $"the device answered with pad {pad + 1}'s patch within {AnswerSeconds} s ({messages.Count} messages came in). Is SFX FORGE running, with its MIDI in and out on these ports?");
            Assert.Greater((int)got.level, 0, "and the patch is a sound, not silence");
            Debug.Log($"LINK: ok a patch came from the device: pad {gotSlot + 1}, engine {got.engine}, {messages.Count} message(s) in");
        }

        /// <summary>
        /// Unity asks for the song; the device answers with its 16 sounds and the song in 39 pieces, and what arrives
        /// plays: an <see cref="SfxMusicPlayer"/> is given the received song and its bar counter moves.
        /// </summary>
        [UnityTest, Explicit]
        public IEnumerator TheDeviceSendsItsSongAndItPlays()
        {
            byte[] packed = null;
            SfxPatch[] sounds = null;
            link.SongReceived += (song, itsSounds) => { packed = song; sounds = itsSounds; };

            Assert.IsTrue(link.SendText("UNITY: SONG?"), "text goes out");
            Assert.IsTrue(link.RequestSong(), "the request goes out");
            yield return Until(() => packed != null, AnswerSeconds);

            Assert.IsNotNull(packed, $"the song arrived within {AnswerSeconds} s ({messages.Count} messages came in: {Describe(messages)}). Is SFX FORGE running, with its MIDI in and out on these ports?");
            Assert.AreEqual(SfxProtocol.SongBytes, packed.Length, "a whole packed song");
            Assert.IsNotNull(sounds, "and all 16 sounds came with it");
            Debug.Log($"LINK: ok the song came from the device: {packed.Length} bytes and {sounds.Length} sounds in {messages.Count} messages");

            var song = ScriptableObject.CreateInstance<SfxSong>();
            song.Set(sounds, packed);
            Assert.IsTrue(song.IsValid, "what arrived is a song the package can play");
            Debug.Log($"LINK: ok the song is valid: {song.Bpm} BPM, {song.StepsPerPattern} steps a pattern, {song.ArrangementBars} bars arranged; {CompareWithTheDemoSong(song)}");

            if (!SfxNative.Available) Assert.Inconclusive("the native library is not here, so the song cannot be played");
            var listener = new GameObject("Listener").AddComponent<AudioListener>();
            var music = new GameObject("Received Song").AddComponent<SfxMusicPlayer>();
            music.song = song;
            music.Play();
            yield return Until(() => music.IsPlaying && music.Bar >= 1, 6f);
            bool played = music.IsPlaying && music.Bar >= 1;
            music.Stop();
            UnityEngine.Object.Destroy(music.gameObject);
            UnityEngine.Object.Destroy(listener.gameObject);
            Assert.IsTrue(played, "the received song plays: its second bar was reached");
            link.SendText("UNITY: GOT THE SONG");
            Debug.Log("LINK: ok the received song plays in an SfxMusicPlayer");
        }

        /// <summary>
        /// The song twenty times in a row, while the game keeps sending to the device as it does in play (a value
        /// every frame): every transfer must arrive whole. One that does not is written out piece by piece.
        /// </summary>
        [UnityTest, Explicit]
        public IEnumerator TheSongArrivesEveryTime()
        {
            const int transfers = 20;
            int arrived = 0;
            link.SongReceived += (song, itsSounds) => arrived++;
            var failures = new List<string>();
            for (int n = 0; n < transfers; n++)
            {
                messages.Clear();
                int before = arrived, frame = 0;
                Assert.IsTrue(link.RequestSong(), "the request goes out");
                yield return Until(() => { link.SendIntensity((frame++ % 100) / 100f); return arrived > before; }, 6f);
                if (arrived == before) failures.Add($"transfer {n + 1}: {messages.Count} messages: {Describe(messages)}");
                yield return Until(() => false, 0.3f);
            }
            Debug.Log($"LINK: {(failures.Count == 0 ? "ok" : "FAILED")} the song arrived whole {arrived} times out of {transfers}");
            Assert.IsEmpty(failures, $"{failures.Count} of {transfers} transfers did not arrive whole: {string.Join(" | ", failures)}");
        }

        /// <summary>
        /// Songs go TO the device and are asked back: what comes back is what was sent, the 16 sounds too.
        ///  1. a song, piece by piece in order;
        ///  2. another, last piece first;
        ///  3. the last 20 pieces of one song and nothing more, then, after the two seconds in which the device forgets
        ///     a transfer that broke off, another song whole with two pairs of neighbours swapped; and the same with
        ///     the first 20 pieces of one, then another whole, last piece first. (Without those two seconds the pieces
        ///     of the two songs can add up to a whole one: no piece says which song it belongs to.)
        ///  4. a song and 16 changed sounds with <c>SfxLink.SendSong</c>, all at once: the sounds must be in the MUSIC
        ///     kit and the kit that is selected on the device must be as it was.
        /// The three songs are the device's own and two made from it that differ from it and from each other in every
        /// one of the 39 pieces, so that a piece that was lost, stale or from the wrong song shows wherever it is.
        /// The device's own song and sounds are put back at the end, also when the test fails on the way.
        /// </summary>
        [UnityTest, Explicit]
        public IEnumerator ASongSentToTheDeviceComesBackTheSame()
        {
            byte[] back = null;
            SfxPatch[] backSounds = null;
            int padSlot = -1;
            SfxPatch pad = default;
            link.SongReceived += (song, itsSounds) => { back = song; backSounds = itsSounds; };
            link.PatchReceived += (slot, patch) => { padSlot = slot; pad = patch; };

            IEnumerator Fetch()
            {
                back = null; backSounds = null;
                messages.Clear();
                Assert.IsTrue(link.RequestSong(), "the request goes out");
                yield return Until(() => back != null, AnswerSeconds);
                Assert.IsNotNull(back, $"the song came back within {AnswerSeconds} s ({messages.Count} messages came in: {Describe(messages)})");
                Assert.IsNotNull(backSounds, "and its 16 sounds came with it");
            }
            IEnumerator FetchSelectedPad()
            {
                padSlot = -1;
                Assert.IsTrue(link.RequestPatch(0), "the request goes out");
                yield return Until(() => padSlot == 0, AnswerSeconds);
                Assert.AreEqual(0, padSlot, "pad 1 of the selected kit came back");
            }
            IEnumerator Send(byte[] song, int[] order, int from, int count)
            {
                byte[][] pieces = SfxProtocol.BuildSong(song);
                for (int n = from; n < from + count; n++)
                {
                    Assert.IsTrue(link.SendSysEx(pieces[order[n]]), $"piece {order[n]} goes out");
                    yield return Until(() => false, PieceSeconds);
                }
                yield return Until(() => false, 0.5f);
            }

            int pieceCount = SfxProtocol.SongChunks;
            var inOrder = new int[pieceCount];
            var lastFirst = new int[pieceCount];
            for (int i = 0; i < pieceCount; i++) { inOrder[i] = i; lastFirst[i] = pieceCount - 1 - i; }
            var swapped = (int[])inOrder.Clone();
            (swapped[9], swapped[10]) = (swapped[10], swapped[9]);
            (swapped[36], swapped[37]) = (swapped[37], swapped[36]);

            yield return FetchSelectedPad();
            SfxPatch selectedPadBefore = pad;
            yield return Fetch();
            byte[] own = back;
            SfxPatch[] ownSounds = backSounds;
            restoreSong = own; restoreSounds = ownSounds;            // (TearDown puts them back if the test does not get to)
            byte[] second = AnotherSong(own), third = AnotherSong(second);
            for (int k = 0; k < pieceCount; k++)
                Assert.IsTrue(PieceDiffers(own, second, k) && PieceDiffers(second, third, k) && PieceDiffers(own, third, k), $"the three songs differ in piece {k}");

            // 1. piece by piece, in order
            yield return Send(second, inOrder, 0, pieceCount);
            yield return Fetch();
            Assert.AreEqual(0, Different(second, back), "sent in order: bytes in which the device's song differs from the one sent");
            Assert.AreEqual(0, DifferentSounds(ownSounds, backSounds), "and its sounds are as they were (sounds that differ)");
            Debug.Log("LINK: ok a song sent to the device piece by piece came back the same");

            // 2. last piece first
            yield return Send(third, lastFirst, 0, pieceCount);
            yield return Fetch();
            Assert.AreEqual(0, Different(third, back), "sent last piece first: bytes in which the device's song differs from the one sent");
            Debug.Log("LINK: ok a song sent to the device last piece first came back the same");

            // 3. a transfer that broke off, then a whole one: the last 20 pieces of one song, then another with neighbours swapped …
            yield return Send(second, inOrder, pieceCount - 20, 20);
            yield return Until(() => false, ForgetSeconds);
            yield return Send(own, swapped, 0, pieceCount);
            yield return Fetch();
            Assert.AreEqual(0, Different(own, back), "after the last 20 pieces of another song, a whole song with neighbours swapped: bytes that differ");
            // … and the first 20 of one, then another, last piece first
            yield return Send(third, inOrder, 0, 20);
            yield return Until(() => false, ForgetSeconds);
            yield return Send(second, lastFirst, 0, pieceCount);
            yield return Fetch();
            Assert.AreEqual(0, Different(second, back), "after the first 20 pieces of another song, a whole song last piece first: bytes that differ");
            Debug.Log("LINK: ok a transfer that broke off (its first pieces, or its last) did not spoil the next");

            // 4. SfxLink.SendSong: 16 changed sounds and a song the device does not have, all at once
            var otherSounds = (SfxPatch[])ownSounds.Clone();
            for (int i = 0; i < otherSounds.Length; i++) otherSounds[i].level = (byte)(otherSounds[i].level > 60 ? otherSounds[i].level - 5 : otherSounds[i].level + 5);
            Assert.AreEqual(otherSounds.Length, DifferentSounds(ownSounds, otherSounds), "every sound to be sent differs from the device's");
            Assert.IsTrue(link.SendSong(otherSounds, third), "the sounds and the song go out");
            yield return Until(() => false, 2.5f);
            yield return Fetch();
            Assert.AreEqual(0, Different(third, back), "sent with SendSong: bytes in which the device's song differs from the one sent");
            Assert.AreEqual(0, DifferentSounds(otherSounds, backSounds), "sent with SendSong: sounds of the device's MUSIC kit that differ from those sent (a firmware before 1.3.1 ignores them)");
            yield return FetchSelectedPad();
            bool musicKitSelected = Same(selectedPadBefore, ownSounds[0]);
            if (musicKitSelected) Debug.Log("LINK: (the MUSIC kit seems to be selected on the device, so whether SendSong leaves the selected kit alone cannot be seen)");
            else Assert.IsTrue(Same(selectedPadBefore, pad), "SendSong left the kit that is selected on the device alone (its pad 1 is as before)");
            Debug.Log("LINK: ok SfxLink.SendSong put the song and its 16 sounds on the device" + (musicKitSelected ? "" : " and left the selected kit alone"));

            // the device's own song and sounds back
            Assert.IsTrue(link.SendSong(ownSounds, own), "the device's own sounds and song go back");
            yield return Until(() => false, 2.5f);
            yield return Fetch();
            Assert.AreEqual(0, Different(own, back), "the device has its own song again (bytes that differ)");
            Assert.AreEqual(0, DifferentSounds(ownSounds, backSounds), "and its own sounds (sounds that differ)");
            restoreSong = null; restoreSounds = null;
            Debug.Log("LINK: ok the device has its own song and sounds again");
        }

        static int Different(byte[] a, byte[] b) { int n = 0; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) n++; return n; }

        static bool Same(SfxPatch a, SfxPatch b)
        {
            byte[] x = a.Pack(), y = b.Pack();
            for (int i = 0; i < x.Length; i++) if (x[i] != y[i]) return false;
            return true;
        }

        static int DifferentSounds(SfxPatch[] a, SfxPatch[] b)
        {
            int n = 0;
            for (int i = 0; i < a.Length; i++) if (!Same(a[i], b[i])) n++;
            return n;
        }

        static bool PieceDiffers(byte[] a, byte[] b, int piece)
        {
            int from = piece * SfxProtocol.SongChunkBytes, to = Math.Min(a.Length, from + SfxProtocol.SongChunkBytes);
            for (int i = from; i < to; i++) if (a[i] != b[i]) return true;
            return false;
        }

        /// <summary>
        /// A song that differs from the given one in every piece and is still a song the device takes and gives back
        /// unchanged: another swing in the first piece, and in each further piece one note of a step or one value of an
        /// automation lane, one higher. Where those are is the packed song's layout (core/sfx_song.h, sfx_song_pack):
        /// at its end 8 patterns x 24 lanes x 16 values, before that 8 patterns x 16 sounds x (3 + 16 notes + 16 more).
        /// </summary>
        static byte[] AnotherSong(byte[] song)
        {
            const int Patterns = 8, Sounds = 16, Steps = 16, Lanes = 24, Swing = 4, SwingMost = 100;
            const int Record = 3 + Steps + Steps, FirstNote = 3;
            int automation = song.Length - Patterns * Lanes * Steps, patterns = automation - Patterns * Sounds * Record;
            var other = (byte[])song.Clone();
            other[Swing] = (byte)((other[Swing] + 17) % (SwingMost + 1));
            for (int piece = 1; piece < SfxProtocol.SongChunks; piece++)
            {
                int from = piece * SfxProtocol.SongChunkBytes, to = Math.Min(song.Length, from + SfxProtocol.SongChunkBytes);
                for (int at = from; at < to; at++)
                {
                    int inRecord = (at - patterns) % Record;
                    bool aNote = at >= patterns && at < automation && inRecord >= FirstNote && inRecord < FirstNote + Steps;
                    if (!aNote && at < automation) continue;
                    other[at] = (byte)((other[at] + 1) & 0x7F);
                    break;
                }
            }
            return other;
        }

        /// <summary>
        /// The game drives the device: pads played, intensity moved, a pattern cued, text shown. Nothing comes back for
        /// these, so the test can only say that every message left; whether the device followed is seen and heard there.
        /// </summary>
        [UnityTest, Explicit]
        public IEnumerator TheGameDrivesTheDevice()
        {
            Assert.IsTrue(link.SendText("UNITY DRIVES"), "text goes out");
            for (int pad = 0; pad < 4; pad++)
            {
                Assert.IsTrue(link.PlayPad(pad, 0.9f), $"pad {pad + 1} is played");
                yield return Until(() => false, 0.3f);
                Assert.IsTrue(link.ReleasePad(pad), $"pad {pad + 1} is released");
            }
            for (int step = 0; step <= 10; step++)
            {
                link.SendIntensity(step / 10f);                      // (false when the 7-bit value did not change)
                yield return Until(() => false, 0.2f);
            }
            Assert.IsTrue(link.CuePattern(2), "pattern C is cued");
            Assert.IsTrue(link.SendText("UNITY: DONE"), "text goes out");
            Debug.Log("LINK: ok pads, intensity, a cue and text were sent to the device");
        }

        /// <summary>
        /// Unity asks the device what it is; the device answers with its identity: its hardware, its firmware's version,
        /// its profile and its flags. A firmware before 1.4.0 does not answer, and this test fails there by design.
        /// </summary>
        [UnityTest, Explicit]
        public IEnumerator TheDeviceSaysWhatItIs()
        {
            int answers = 0;
            SfxDeviceInfo said = default;
            link.IdentityReceived += device => { answers++; said = device; };

            Assert.IsTrue(link.SendText("UNITY: WHO?"), "text goes out");
            Assert.IsTrue(link.RequestIdentity(), "the request goes out");     // (Open asked once already: a second answer says the same)
            yield return Until(() => answers > 0, AnswerSeconds);

            Assert.Greater(answers, 0, $"the device said what it is within {AnswerSeconds} s ({messages.Count} messages came in: {Describe(messages)}). Is SFX FORGE running, firmware 1.4.0 or later, with its MIDI in and out on these ports?");
            Assert.IsTrue(link.Device.HasValue && link.Device.Value.Equals(said), "and the link keeps what it said");
            Debug.Log($"LINK: ok the device said what it is: {said} ({said.profile}; flags {said.flags})");

            Assert.AreEqual(SfxDeviceInfo.HardwareVibeFm, said.hardware, "it is SFX FORGE on VIBE FM");
            Assert.IsTrue(said.IsAtLeast(1, 4, 0), $"its firmware is 1.4.0 or later (it says {said.major}.{said.minor}.{said.patch})");
            Assert.AreEqual(SfxProfile.VibeFm1, said.profile, "its profile is the one this package is made for");
            Assert.IsTrue(said.HasPads, "it has pads");
            Assert.IsTrue(said.HasEncoders, "it has encoders for the game");
            Assert.IsTrue(said.TakesSongSounds, "it takes a song's sounds");
            Assert.IsTrue(said.TakesTelemetry, "it takes telemetry");
            Assert.IsNull(SfxLink.Refusal(link.Device, true), "so nothing stands against sending it a patch or a song");
            Debug.Log("LINK: ok firmware 1.4.0 or later, the profile of this package, pads, encoders, a song's sounds, telemetry");
        }

        /// <summary>What came in, in order and in short: "p3" a patch for pad 4, "s7/208" piece 7 of the song and its length, "id/24" an identity and its length, otherwise the first byte and the length.</summary>
        public static string Describe(List<byte[]> incoming)
        {
            var text = new System.Text.StringBuilder();
            foreach (byte[] m in incoming)
            {
                if (m.Length > 6 && m[0] == 0xF0 && m[1] == 0x7D && m[4] == SfxProtocol.Dump) text.Append('p').Append(m[5]);
                else if (m.Length > 6 && m[0] == 0xF0 && m[1] == 0x7D && m[4] == SfxProtocol.SongChunk) text.Append('s').Append(m[5]).Append('/').Append(m.Length);
                else if (m.Length > 6 && m[0] == 0xF0 && m[1] == 0x7D && m[4] == SfxProtocol.Identity) text.Append("id/").Append(m.Length);
                else text.Append(m.Length > 0 ? m[0].ToString("X2") : "--").Append('/').Append(m.Length);
                text.Append(' ');
            }
            return text.ToString().TrimEnd();
        }

        /// <summary>Says whether the received song is the demo song of the demo project, byte for byte.</summary>
        static string CompareWithTheDemoSong(SfxSong received)
        {
#if UNITY_EDITOR
            var demo = UnityEditor.AssetDatabase.LoadAssetAtPath<SfxSong>(ShowcaseFolders.SongPath);
            if (demo == null || !demo.IsValid) return "the demo song asset is not here to compare with";
            if (demo.data.Length != received.data.Length) return "it is not the demo song (another length)";
            int different = 0;
            for (int i = 0; i < demo.data.Length; i++) if (demo.data[i] != received.data[i]) different++;
            int soundsDifferent = 0;
            for (int i = 0; i < SfxProtocol.Slots; i++)
            {
                byte[] a = demo.sounds[i].Pack(), b = received.sounds[i].Pack();
                for (int k = 0; k < a.Length; k++) if (a[k] != b[k]) { soundsDifferent++; break; }
            }
            return different == 0 && soundsDifferent == 0
                ? "it is the demo song of the demo project, byte for byte"
                : $"it differs from the demo project's song in {different} byte(s) and {soundsDifferent} sound(s) (the device's song was edited, or is another)";
#else
            return "";
#endif
        }
    }
}
