// SPDX-License-Identifier: MIT
//
// What a device is and what it holds.
//   SfxProfile     the formats a device speaks and its sizes, as numbers
//   SfxDeviceInfo  what a device said it is: hardware, firmware version, profile, flags
// SfxProtocol.TryParseIdentity reads one from a message; SfxLink.Device has it once the device answered.
using System;

namespace CatSplat.SfxForge
{
    /// <summary>
    /// What a device offers, as numbers: the formats it speaks and how much it holds. A device says them in its
    /// identity (SfxProtocol.Identity); <see cref="VibeFm1"/> is what SFX FORGE 1.x offers on VIBE FM, made of this
    /// package's own constants. In this version every sound and every song fits it: the type is here so that a later
    /// one can ask "does this fit that device" of a device that holds less, or more.
    /// </summary>
    [Serializable]
    public struct SfxProfile : IEquatable<SfxProfile>
    {
        /// <summary>Which profile: 1 = SFX FORGE 1.x on VIBE FM.</summary>
        public int id;

        /// <summary>The first byte of its patches (2) and of its packed song (6; 5 up to firmware 1.5.0).</summary>
        public int patchFormat;

        /// <summary>The first byte of its packed song (6; 5 up to firmware 1.5.0).</summary>
        public int songFormat;

        /// <summary>
        /// Sounds in a kit (16), kits (2), patterns (8), the most steps a pattern holds (64; 16 up to firmware 1.5.0),
        /// automation lanes (24), bars in an arrangement (32).
        /// </summary>
        public int sounds;

        /// <summary>Kits (2).</summary>
        public int kits;

        /// <summary>Patterns in a song (8).</summary>
        public int patterns;

        /// <summary>The most steps a pattern holds (64; 16 up to firmware 1.5.0).</summary>
        public int steps;

        /// <summary>Automation lanes in a song (24).</summary>
        public int lanes;

        /// <summary>Bars in an arrangement (32).</summary>
        public int bars;

        /// <summary>Voices that sound at once (16), engines (5), wavetables (6), values in a patch (25).</summary>
        public int voices;

        /// <summary>Engines (5).</summary>
        public int engines;

        /// <summary>Wavetables (6).</summary>
        public int wavetables;

        /// <summary>Values in a patch (25).</summary>
        public int patchValues;

        /// <summary>
        /// What SFX FORGE 1.6 on offers on VIBE FM (the emulator, the plugin, the hardware): song format 6, up to 64
        /// steps a pattern.
        /// </summary>
        public static SfxProfile VibeFm1 =>
            new SfxProfile
            {
                id = 1,
                patchFormat = SfxPatch.Version,
                songFormat = SfxProtocol.SongVersion,
                sounds = SfxProtocol.Slots,
                kits = SfxProtocol.Kits,
                patterns = SfxProtocol.Patterns,
                steps = SfxProtocol.GridSteps,
                lanes = SfxProtocol.Lanes,
                bars = SfxProtocol.Bars,
                voices = SfxProtocol.Voices,
                engines = SfxPatch.EngineCount,
                wavetables = SfxPatch.TableCount,
                patchValues = SfxPatch.ParamCount,
            };

        /// <summary>
        /// What the firmwares 1.4 and 1.5 said of themselves (and what one before them is taken for): song format 5, 16
        /// steps a pattern.
        /// </summary>
        public static SfxProfile VibeFm15
        {
            get
            {
                var p = VibeFm1;
                p.songFormat = 5;
                p.steps = SfxProtocol.Steps;
                return p;
            }
        }

        /// <summary>
        /// The sound is one this profile's device can hold: the same patch format with as many values, an engine it
        /// has, every value 0..127. (A table beyond its last wraps round on the device, so any table fits.)
        /// </summary>
        public bool Fits(SfxPatch patch)
        {
            if (patchFormat != SfxPatch.Version || patchValues != SfxPatch.ParamCount || (int)patch.engine >= engines)
            {
                return false;
            }

            for (int i = 0; i < SfxPatch.ParamCount; i++)
            {
                if (patch[(SfxParam)i] > 127)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// The packed song is within this profile's limits: a format up to its own, a length that format can have,
        /// every byte 0..127, a bar of 1..16 sixteenths, no pattern with more steps and no more bars arranged than it
        /// holds. An older song fits too – that says nothing of the wire: SfxLink.Refusal says what a device is sent.
        /// Nor of the values inside: SfxPackedSong.IsPlayable says whether the player takes them.
        /// </summary>
        public bool Fits(byte[] song)
        {
            if (
                song == null
                || song.Length < 7
                || song[0] < 1
                || song[0] > Math.Min(songFormat, SfxProtocol.SongVersion)
            )
            {
                return false;
            }

            int v = song[0];
            if (sounds < SfxProtocol.Slots || patterns < SfxProtocol.Patterns)
            {
                return false;
            }

            for (int i = 0; i < song.Length; i++)
            {
                if (song[i] > 127)
                {
                    return false;
                }
            }

            // (the bar, in sixteenths: in every format)
            if (song[1] < 1 || song[1] > SfxProtocol.Steps)
            {
                return false;
            }

            if (v <= 5)
            {
                int length =
                    v == 5 ? SfxProtocol.SongBytesV5
                    : v == 4 ? SfxProtocol.SongBytesV4
                    : v == 3 ? SfxProtocol.SongBytesV3
                    : v == 2 ? SfxProtocol.SongBytesV2
                    : SfxProtocol.SongBytesV1;
                if (song.Length != length || song[1] > steps)
                {
                    return false;
                }
            }
            else
            {
                if (!SfxPackedSong.IsValid(song))
                {
                    return false;
                }

                // (a pattern of 32 or 64 steps on a device that holds 16)
                for (int p = 0; p < SfxProtocol.Patterns; p++)
                {
                    if (SfxPackedSong.Grid(song, p) > steps)
                    {
                        return false;
                    }
                }
            }

            // (the arrangement's length: where SfxSong reads it)
            if (v >= 4 && song[v == 4 ? 66 + SfxProtocol.Slots : 66] > bars)
            {
                return false;
            }

            if (v >= 5 && lanes < SfxProtocol.Lanes)
            {
                return false;
            }

            return true;
        }

        public bool Equals(SfxProfile o) =>
            id == o.id
            && patchFormat == o.patchFormat
            && songFormat == o.songFormat
            && sounds == o.sounds
            && kits == o.kits
            && patterns == o.patterns
            && steps == o.steps
            && lanes == o.lanes
            && bars == o.bars
            && voices == o.voices
            && engines == o.engines
            && wavetables == o.wavetables
            && patchValues == o.patchValues;

        public override bool Equals(object obj) => obj is SfxProfile o && Equals(o);

        public override int GetHashCode() =>
            ((id * 31 + patchFormat) * 31 + songFormat) * 31
            + sounds
            + kits * 7
            + patterns * 11
            + steps * 13
            + lanes * 17
            + bars * 19
            + voices * 23
            + engines * 29
            + wavetables * 37
            + patchValues * 41;

        public static bool operator ==(SfxProfile a, SfxProfile b) => a.Equals(b);

        public static bool operator !=(SfxProfile a, SfxProfile b) => !a.Equals(b);

        public override string ToString() =>
            $"profile {id}: patch format {patchFormat}, song format {songFormat}, "
            + $"{kits} kits of {sounds} sounds, {patterns} patterns of {steps} steps, {lanes} lanes, {bars} bars, "
            + $"{voices} voices, {engines} engines, {wavetables} wavetables";
    }

    /// <summary>
    /// What a device said it is (its identity, SfxProtocol.TryParseIdentity) – or, for one that does not answer, what
    /// is assumed (<see cref="AssumedBefore14"/>). SfxLink.Device has it once the device answered.
    /// </summary>
    [Serializable]
    public struct SfxDeviceInfo : IEquatable<SfxDeviceInfo>
    {
        /// <summary>hardware: the device did not say, or said something this version does not know.</summary>
        public const int HardwareUnknown = 0;

        /// <summary>hardware: VIBE FM (the emulator, the plugin or the VIBE Box).</summary>
        public const int HardwareVibeFm = 1;

        /// <summary>flags, bit 0: it has pads.</summary>
        public const int FlagPads = 1;

        /// <summary>flags, bit 1: it has encoders for the game (the LINK page).</summary>
        public const int FlagEncoders = 2;

        /// <summary>flags, bit 2: it takes a song's sounds.</summary>
        public const int FlagSongSounds = 4;

        /// <summary>flags, bit 3: it takes telemetry (CC 10..17 on channel 2).</summary>
        public const int FlagTelemetry = 8;

        /// <summary>
        /// 0: unknown. 1: VIBE FM – the SDK's host, which is the emulator, the plugin or the VIBE Box: the firmware
        /// cannot tell them apart.
        /// </summary>
        public int hardware;

        /// <summary>The firmware's version: major, minor, patch (1.4.0 is the first that says it).</summary>
        public int major;

        /// <summary>The firmware's version: its minor number.</summary>
        public int minor;

        /// <summary>The firmware's version: its patch number.</summary>
        public int patch;

        /// <summary>The formats it speaks and how much it holds.</summary>
        public SfxProfile profile;

        /// <summary>The Flag… bits: read them as HasPads, HasEncoders, TakesSongSounds, TakesTelemetry.</summary>
        public int flags;

        /// <summary>
        /// True: nothing answered, this is what is taken for granted of a firmware before 1.4 (no version is known).
        /// </summary>
        public bool assumed;

        /// <summary>It has pads that send notes 36..51.</summary>
        public bool HasPads => (flags & FlagPads) != 0;

        /// <summary>It has encoders for the game: CC 20..25 from its LINK page.</summary>
        public bool HasEncoders => (flags & FlagEncoders) != 0;

        /// <summary>It takes a song's 16 sounds (LOAD for slots 16..31), not only the song.</summary>
        public bool TakesSongSounds => (flags & FlagSongSounds) != 0;

        /// <summary>It takes the game's values: CC 10..17 on channel 2.</summary>
        public bool TakesTelemetry => (flags & FlagTelemetry) != 0;

        /// <summary>
        /// The firmware is this version or a later one. False for an assumed device: its version is not known.
        /// </summary>
        public bool IsAtLeast(int wantMajor, int wantMinor, int wantPatch) =>
            !assumed
            && (
                major != wantMajor ? major > wantMajor
                : minor != wantMinor ? minor > wantMinor
                : patch >= wantPatch
            );

        /// <summary>
        /// A device that does not answer the request: a firmware before 1.4.0 ignores it. It is taken for SFX FORGE on
        /// VIBE FM with profile 1 as it was then (song format 5, 16 steps a pattern), pads, encoders and telemetry.
        /// Whether it takes a song's sounds is not known (1.3.1 does, older ones do not), so that flag is not set.
        /// </summary>
        public static SfxDeviceInfo AssumedBefore14 =>
            new SfxDeviceInfo
            {
                hardware = HardwareVibeFm,
                profile = SfxProfile.VibeFm15,
                flags = FlagPads | FlagEncoders | FlagTelemetry,
                assumed = true,
            };

        /// <summary>The hardware in words: "VIBE FM", "unknown hardware" or "hardware N".</summary>
        public string HardwareName =>
            hardware == HardwareVibeFm ? "VIBE FM"
            : hardware == HardwareUnknown ? "unknown hardware"
            : "hardware " + hardware;

        /// <summary>"VIBE FM, firmware 1.4.0, profile 1" – or "… firmware before 1.4 …" for an assumed one.</summary>
        public override string ToString() =>
            HardwareName
            + ", firmware "
            + (assumed ? "before 1.4" : major + "." + minor + "." + patch)
            + ", profile "
            + profile.id;

        public bool Equals(SfxDeviceInfo o) =>
            hardware == o.hardware
            && major == o.major
            && minor == o.minor
            && patch == o.patch
            && flags == o.flags
            && assumed == o.assumed
            && profile.Equals(o.profile);

        public override bool Equals(object obj) => obj is SfxDeviceInfo o && Equals(o);

        public override int GetHashCode() =>
            (((hardware * 31 + major) * 31 + minor) * 31 + patch) * 31
            + flags
            + profile.GetHashCode() * 7
            + (assumed ? 1 : 0);
    }
}
