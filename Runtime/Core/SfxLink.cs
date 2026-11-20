// SPDX-License-Identifier: MIT
using System;
using System.Text;

namespace CatSplat.SfxForge
{
    /// <summary>
    /// The connection to SFX FORGE (the VIBE FM firmware in the browser, the plugin or on the hardware) over two MIDI ports.
    /// One for the editor and the game: SfxLink.Shared. Call Poll() regularly – the events fire from there, on your thread.
    /// </summary>
    public sealed class SfxLink : IDisposable
    {
        public static readonly SfxLink Shared = new SfxLink();

        MidiIn input;
        MidiOut output;
        readonly int[] lastCc = new int[8];
        readonly byte[] songIn = new byte[SfxProtocol.SongBytes];
        readonly SfxPatch[] sounds = new SfxPatch[SfxProtocol.Slots];
        int soundsSeen;
        ulong songSeen;
        readonly System.Diagnostics.Stopwatch sinceSongPiece = System.Diagnostics.Stopwatch.StartNew();
        const double ForgetSongSeconds = 2.0;       // a song whose pieces stopped coming is forgotten after this
        string lastNewer;
        readonly System.Diagnostics.Stopwatch sinceNewer = new System.Diagnostics.Stopwatch();
        readonly System.Diagnostics.Stopwatch sinceIdentityAsked = new System.Diagnostics.Stopwatch();
        /// <summary>A device that has not said what it is this long after it was asked is taken not to answer.</summary>
        public const double IdentityAnswerSeconds = 1.0;

        public SfxLink() { Forget(); }

        /// <summary>The device sent a patch: EXPORT was pressed, or the answer to RequestPatch. (slot 0..15)</summary>
        public event Action<int, SfxPatch> PatchReceived;
        /// <summary>
        /// The device sent its song (SEND on its SONG page, or the answer to RequestSong): the packed song, and its 16
        /// sounds (null if not all of them came with it).
        /// </summary>
        public event Action<byte[], SfxPatch[]> SongReceived;
        /// <summary>A pad was played on the device (slot 0..15, velocity 0..1).</summary>
        public event Action<int, float> PadDown;
        public event Action<int> PadUp;
        /// <summary>An encoder turned on the device's LINK page: index 0..5 (encoders 1-4, A, B), value 0..1.</summary>
        public event Action<int, float> EncoderChanged;
        /// <summary>The device said what it is: the answer to RequestIdentity (firmware 1.4.0 on).</summary>
        public event Action<SfxDeviceInfo> IdentityReceived;
        /// <summary>
        /// The device sent a patch or a song in a format above this version's: a newer firmware. Nothing of it is taken
        /// (no PatchReceived, no SongReceived); the text says what came ("the device sent a patch in format 3 ...").
        /// Said once for what arrives within two seconds (a song comes in pieces, and the emulator delivers twice).
        /// </summary>
        public event Action<string> NewerReceived;
        /// <summary>Everything that comes in, as it is.</summary>
        public event Action<byte[]> MessageReceived;

        /// <summary>
        /// What the device said it is. Null until it answered: Open asks, and a firmware before 1.4.0 never answers
        /// (then SfxDeviceInfo.AssumedBefore14 is what to go by). Forgotten when the ports close, and when it is asked
        /// again: the device behind the same ports may be another one by then.
        /// </summary>
        public SfxDeviceInfo? Device { get; private set; }
        /// <summary>The device was asked what it is and a second went by without an answer: a firmware before 1.4, or nothing listens.</summary>
        public bool IdentityUnanswered => Device == null && sinceIdentityAsked.IsRunning && sinceIdentityAsked.Elapsed.TotalSeconds >= IdentityAnswerSeconds;
        /// <summary>
        /// Why the last SendPatch or SendSong sent nothing although it could have: the device said it speaks another
        /// format. Null when that call was not refused (it may still have failed: no output, a port that would not send).
        /// </summary>
        public string LastRefusal { get; private set; }

        public string InputName => input != null ? input.Name : null;
        public string OutputName => output != null ? output.Name : null;
        public bool HasInput => input != null;
        public bool HasOutput => output != null;
        public bool IsOpen => input != null || output != null;

        /// <summary>The first port whose name contains the text (any case), -1 = none.</summary>
        public static int Find(string[] names, string contains)
        {
            if (string.IsNullOrEmpty(contains)) return -1;
            for (int i = 0; i < names.Length; i++) if (names[i].IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0) return i;
            return -1;
        }

        /// <summary>Opens the ports whose names contain these texts (null or "": none). True if every port asked for is open.</summary>
        public bool Open(string inputContains, string outputContains)
        {
            Close();
            int i = Find(MidiPorts.Inputs(), inputContains), o = Find(MidiPorts.Outputs(), outputContains);
            if (i >= 0) input = MidiIn.TryOpen(i);
            if (o >= 0) output = MidiOut.TryOpen(o);
            RequestIdentity();                              // (a firmware before 1.4.0 ignores it)
            return (string.IsNullOrEmpty(inputContains) || input != null) && (string.IsNullOrEmpty(outputContains) || output != null);
        }

        public void Close()
        {
            if (input != null) { input.Dispose(); input = null; }
            if (output != null) { output.Dispose(); output = null; }
            songSeen = 0; soundsSeen = 0;
            Device = null; LastRefusal = null; sinceIdentityAsked.Reset();
            lastNewer = null; sinceNewer.Reset();
            Forget();
        }

        public void Dispose() => Close();

        /// <summary>
        /// Takes a piece of a song; true when it made the song whole. A piece that follows a long silence begins a new
        /// transfer (only a message that really is a piece counts as breaking the silence).
        /// </summary>
        bool TakeSongPiece(byte[] m)
        {
            if (m.Length < 9 || m[4] != SfxProtocol.SongChunk) return false;
            ulong before = sinceSongPiece.Elapsed.TotalSeconds > ForgetSongSeconds ? 0 : songSeen, seen = before;
            bool whole = SfxProtocol.TryTakeSongChunk(m, songIn, ref seen);
            if (seen == before && !whole) return false;             // not a piece, or one that was in already
            songSeen = seen;
            sinceSongPiece.Restart();
            return whole;
        }

        void Forget() { for (int i = 0; i < lastCc.Length; i++) lastCc[i] = -1; }

        /// <summary>Hands out what came in since the last call.</summary>
        public void Poll()
        {
            if (input == null) return;
            for (int guard = 0; guard < 256 && input.TryRead(out var m); guard++) Take(m);
        }

        // One message that came in. (Apart from Poll so that it can be tried without a port: tests/dotnet.)
        internal void Take(byte[] m)
        {
            MessageReceived?.Invoke(m);
            if (m.Length == 0) return;
            if (m[0] == 0xF0)
            {
                // an identity, or our own request come back, is never a patch or a piece of a song, and leaves a song that is arriving alone
                if (SfxProtocol.IsOurs(m) && (m[4] == SfxProtocol.Identity || m[4] == SfxProtocol.RequestIdentity))
                {
                    if (SfxProtocol.TryParseIdentity(m, out var device))
                    {
                        Device = device;                    // (the emulator delivers it twice: the second says the same)
                        IdentityReceived?.Invoke(device);
                    }
                }
                else if (SfxProtocol.TryParsePatch(m, out int slot, out var patch))
                {
                    sounds[slot] = patch; soundsSeen |= 1 << slot;
                    PatchReceived?.Invoke(slot, patch);
                }
                else if (TakeSongPiece(m))
                {
                    // (a newer song whose pieces are the size of ours comes together too: it was said to be newer at its first piece)
                    if (!SfxProtocol.IsNewerSong(songIn)) SongReceived?.Invoke((byte[])songIn.Clone(), soundsSeen == 0xFFFF ? (SfxPatch[])sounds.Clone() : null);
                    soundsSeen = 0;
                }
                // a patch or a song in a format above ours is newer, not broken: it is not taken, and that is said
                string newer = SfxProtocol.NewerIn(m);
                if (newer != null && (newer != lastNewer || sinceNewer.Elapsed.TotalSeconds > ForgetSongSeconds))
                {
                    lastNewer = newer; sinceNewer.Restart();
                    NewerReceived?.Invoke("the device sent " + newer + ", this SFX Forge knows up to "
                        + (newer.StartsWith("a patch") ? SfxPatch.Version : SfxProtocol.SongVersion) + ": it was not taken. Update the package to use it.");
                }
                return;
            }
            if (m.Length < 3) return;
            int kind = m[0] & 0xF0, pad = m[1] - SfxProtocol.PadNoteBase, enc = m[1] - SfxProtocol.EncoderCcBase;
            if (kind == 0x90 && m[2] > 0 && pad >= 0 && pad < SfxProtocol.Slots) PadDown?.Invoke(pad, m[2] / 127f);
            else if ((kind == 0x80 || kind == 0x90) && pad >= 0 && pad < SfxProtocol.Slots) PadUp?.Invoke(pad);
            else if (kind == 0xB0 && enc >= 0 && enc < SfxProtocol.EncoderCount) EncoderChanged?.Invoke(enc, m[2] / 127f);
        }

        // ---- to the device ------------------------------------------------------------------------------------------
        /// <summary>A game value 0..1. Sent only when its 7-bit value changes, so calling it every frame is fine.</summary>
        public bool SendTelemetry(SfxTelemetry channel, float value01)
        {
            int k = (int)channel;
            byte cc = SfxProtocol.ToCc(value01);
            if (output == null || k < 0 || k > 7 || lastCc[k] == cc) return false;
            lastCc[k] = cc;
            return output.Send(SfxProtocol.TelemetryStatus, (byte)(SfxProtocol.TelemetryCcBase + k), cc);
        }

        /// <summary>
        /// Asks the device what it is; the answer comes as IdentityReceived and stays in Device. Open asks by itself.
        /// What an earlier answer said is forgotten when the request goes out (Device is null until the new answer):
        /// another firmware may sit behind the same ports by now. A firmware before 1.4.0 does not answer: see
        /// IdentityUnanswered, which is true a second after any request that got no answer.
        /// </summary>
        public bool RequestIdentity()
        {
            if (output == null || !output.SendSysEx(SfxProtocol.BuildRequestIdentity())) return false;
            Asked();
            return true;
        }

        // A request went out: what was known of the device is not known any more. (Apart so that it can be tried without a port: tests/dotnet.)
        internal void Asked() { Device = null; sinceIdentityAsked.Restart(); }

        /// <summary>
        /// Why a device that said this of itself is not sent a patch (song: false) or a song with its sounds (song:
        /// true) from here: it speaks another format, so it would refuse the bytes or read them wrongly. Null: nothing
        /// stands against it – also for a device that said nothing (null), which is taken for a firmware before 1.4.
        /// </summary>
        public static string Refusal(SfxDeviceInfo? device, bool song)
        {
            if (device == null) return null;
            var p = device.Value.profile;
            if (p.patchFormat != SfxPatch.Version)
                return $"the device ({device.Value}) speaks patch format {p.patchFormat}, this SFX Forge speaks {SfxPatch.Version}: "
                    + (p.patchFormat > SfxPatch.Version ? "update the package" : "update the device's firmware");
            if (song && p.songFormat != SfxProtocol.SongVersion)
                return $"the device ({device.Value}) speaks song format {p.songFormat}, this SFX Forge speaks {SfxProtocol.SongVersion}: "
                    + (p.songFormat > SfxProtocol.SongVersion ? "update the package" : "update the device's firmware");
            return null;
        }

        /// <summary>
        /// A patch into a pad of the device (it selects that pad). False and nothing sent when the device said it
        /// speaks another patch format: LastRefusal says so.
        /// </summary>
        public bool SendPatch(int slot, SfxPatch patch)
        {
            if ((LastRefusal = Refusal(Device, false)) != null) return false;
            return output != null && output.SendSysEx(SfxProtocol.BuildLoad(slot, patch));
        }

        /// <summary>Asks for a pad's patch; the answer comes as PatchReceived.</summary>
        public bool RequestPatch(int slot) => output != null && output.SendSysEx(SfxProtocol.BuildRequest(slot));

        /// <summary>Asks for the 16 sounds and the song of the device; the answer comes as SongReceived.</summary>
        public bool RequestSong() { soundsSeen = 0; songSeen = 0; return output != null && output.SendSysEx(SfxProtocol.BuildRequestSong()); }

        /// <summary>
        /// 16 sounds and a packed song to the device: they replace its song and the 16 sounds of its MUSIC kit, which
        /// the song plays (the kit that is selected there is left alone). Needs firmware 1.3.1: an older one takes the
        /// song and ignores the sounds. False and nothing sent when the device said it speaks another patch or song
        /// format: LastRefusal says so.
        /// </summary>
        public bool SendSong(SfxPatch[] patches, byte[] song)
        {
            if ((LastRefusal = Refusal(Device, true)) != null) return false;
            if (output == null || patches == null || patches.Length != SfxProtocol.Slots) return false;
            bool ok = true;
            for (int i = 0; i < patches.Length; i++) ok &= output.SendSysEx(SfxProtocol.BuildLoadSongSound(i, patches[i]));
            foreach (var m in SfxProtocol.BuildSong(song)) ok &= output.SendSysEx(m);
            return ok;
        }

        /// <summary>The music on the device: how hot the game is, 0..1 (layers come in).</summary>
        public bool SendIntensity(float value01) => SendTelemetry(SfxTelemetry.Intensity, value01);
        /// <summary>The music on the device: this pattern (0..7) from the next bar line.</summary>
        public bool CuePattern(int pattern) => SendTelemetry(SfxTelemetry.Pattern, SfxProtocol.PatternValue(pattern));

        /// <summary>One SysEx message as it is (F0 … F7): for what this class has no call for. False when no output is open.</summary>
        public bool SendSysEx(byte[] message) => output != null && message != null && message.Length > 1 && output.SendSysEx(message);

        /// <summary>Text for the device's top line (a level's name, a state): 24 ASCII characters.</summary>
        public bool SendText(string text) => output != null && output.SendSysEx(SfxProtocol.BuildText(text));

        public bool PlayPad(int slot, float velocity = 0.8f)
        {
            if (output == null || slot < 0 || slot >= SfxProtocol.Slots) return false;
            byte v = SfxProtocol.ToCc(velocity);
            return output.Send(0x90, (byte)(SfxProtocol.PadNoteBase + slot), v == 0 ? (byte)1 : v);
        }

        public bool ReleasePad(int slot)
            => output != null && slot >= 0 && slot < SfxProtocol.Slots && output.Send(0x80, (byte)(SfxProtocol.PadNoteBase + slot), 0);

        public static string Describe(byte[] m)
        {
            var s = new StringBuilder();
            for (int i = 0; i < m.Length && i < 24; i++) s.Append(m[i].ToString("X2")).Append(' ');
            if (m.Length > 24) s.Append('…');
            return s.ToString().TrimEnd();
        }
    }
}
