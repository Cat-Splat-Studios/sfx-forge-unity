using System.Collections.Generic;
using CatSplat.SfxForge;

namespace SfxShowcase.Audio
{
    /// <summary>
    /// THE WHOLE SOUND SET OF THE GAME, ON ONE SCREEN. Every sound effect is one named SFX FORGE patch below – no audio
    /// files anywhere. To retune a sound, change a number here and rebuild the scene (or edit the generated preset asset
    /// in the Inspector while the game runs: a live SfxPlayer follows the edit).
    ///
    /// How to read a patch. Every value is 0..127, exactly as on the device (docs/MANUAL.md, "Every value"):
    ///   frequency     the pitch, logarithmic 20 Hz..14.5 kHz. IGNORED when the sound is played as a MIDI note – every
    ///                 pitched effect here is (the "note" column of the table below; baked, live and held alike), so
    ///                 it is in tune with the song (E minor pentatonic, see <see cref="Pitch"/>).
    ///   pitchEnv      64 = none. Above: the pitch starts higher and falls to the note; below: starts lower and rises.
    ///   pitchEnvTime  how long that takes (5 ms..2.6 s). 25 ≈ 17 ms, 45 ≈ 45 ms, 60 ≈ 95 ms, 75 ≈ 200 ms, 90 ≈ 420 ms.
    ///   engineA/B     the engine's own two controls:  IMPACT and SCI-FI FM: FM ratio (21 = 1:2, 42 = 1:1, 64 = 2:1) / FM depth
    ///                 · UI BLIP: arpeggio step (37 = a fifth, 64 = an octave) / arpeggio speed · FOLEY: grain rate / grain amount
    ///                 · WAVETABLE: position / warp.
    ///   noise         how much of the sound is noise (FOLEY: 127).
    ///   cutoff        the low-pass filter (FOLEY: the band the noise sits in). 62 ≈ 0.7 kHz, 90 ≈ 2.7 kHz, 110 ≈ 7 kHz.
    ///   decay         how fast it fades. 22 ≈ very short tick, 35 ≈ short, 50 ≈ medium, 70 ≈ long, 105 ≈ rings for seconds.
    ///   sustain       0 = a ONE-SHOT that plays to its end. Above 0 = HELD until released (attack and release then matter).
    ///   lfo…          a slow wave (lfoRate: 50 ≈ 1 Hz, 105 ≈ 11 Hz) moving pitch, filter or timbre.
    ///   chorus, delay, reverb   the sends to the three effects.     level   100 = as designed.
    /// The "starts from" names are the device's factory presets (sfx-forge/sfx-forge.c, PRESETS).
    /// </summary>
    public static class SoundLibrary
    {
        // ---- the player's body ---------------------------------------------------------------------------------------

        /// <summary>
        /// JUMP – a quick, bright chirp that leaps upward; light, never shrill, under 0.2 s.
        /// Starts from POWER-UP, without its arpeggio: a square wave that starts 1¼ octaves low and reaches E5 in 35 ms,
        /// then is gone. The filter is a little closed so the square is not harsh.
        /// </summary>
        public static readonly SfxPatch Jump = Make(SfxEngine.UiBlip,
            pitchEnv: 44, pitchEnvTime: 40, engineA: 0, engineB: 0, cutoff: 104, resonance: 10, decay: 30, level: 90);

        /// <summary>
        /// LAND – a soft, low thump. Starts from THUD: a sine near 65 Hz that drops into place, a trace of noise for the
        /// contact, a closed filter so it stays soft. Its loudness follows the impact speed (set when it is played).
        /// </summary>
        public static readonly SfxPatch Land = Make(SfxEngine.Impact,
            frequency: 23, pitchEnv: 90, pitchEnvTime: 35, engineA: 0, engineB: 0, noise: 8, cutoff: 68, resonance: 0, decay: 48);

        /// <summary>
        /// FOOTSTEP (left) – a tiny dry tick, the quietest sound in the game. Starts from FOOTSTEP, shorter: a band of
        /// noise around 1 kHz that snaps downward in 12 ms. Pure noise has no pitch, so the right foot differs by its
        /// band, and the speed-up comes from the AudioSource's pitch.
        /// </summary>
        public static readonly SfxPatch FootstepLeft = Make(SfxEngine.Foley,
            frequency: 30, pitchEnv: 80, pitchEnvTime: 18, engineA: 90, engineB: 30, noise: 127, cutoff: 68, resonance: 15, decay: 22);

        /// <summary>FOOTSTEP (right) – the left foot's tick with its band a little higher, so the two feet alternate.</summary>
        public static readonly SfxPatch FootstepRight = With(FootstepLeft, SfxParam.FilterCutoff, 74);

        /// <summary>
        /// TUCK – a short zip downward, like a zipper closing. Starts from LASER, softer and shorter: an FM tone that
        /// falls 2¼ octaves to B4 in 28 ms; SCI-FI FM lets the brightness fall with the pitch, which is the "zip".
        /// </summary>
        public static readonly SfxPatch TuckStart = Make(SfxEngine.SciFiFm,
            pitchEnv: 100, pitchEnvTime: 35, engineA: 40, engineB: 45, cutoff: 100, resonance: 15, decay: 35, level: 90);

        /// <summary>
        /// ROLL HUM – a low held hum while rolling on the ground. Starts from ENGINE HUM: FM with the modulator an octave
        /// below the note (ratio 1:2) growls; SUSTAIN at full makes it a held sound. A soft attack and release so it
        /// fades in and out. The game bends it live: pitch 0..+12 semitones, cutoff and level follow speed.
        /// </summary>
        public static readonly SfxPatch RollHum = Make(SfxEngine.SciFiFm,
            pitchEnv: 64, engineA: 21, engineB: 55, noise: 8, cutoff: 78, resonance: 45, decay: 60,
            attack: 60, sustain: 127, release: 60);

        /// <summary>
        /// WIND-UP REV – a held rev that the game retriggers on every charge tap and bends upward with the charge.
        /// From ENGINE HUM / WARP: FM at ratio 1:1 (a purer, more "wound" tone than the roll), a fast LFO on the timbre
        /// for the spinning flutter, a short release so each tap re-attacks cleanly. Live: pitch 0..+12, FM depth rising.
        /// </summary>
        public static readonly SfxPatch WindUpRev = Make(SfxEngine.SciFiFm,
            pitchEnv: 70, pitchEnvTime: 30, engineA: 42, engineB: 35, noise: 5, cutoff: 86, resonance: 35, decay: 60,
            attack: 10, sustain: 110, release: 45, lfoRate: 110, lfoTimbre: 40);

        /// <summary>
        /// BURST – a punchy noise whoosh with a falling tail. WHOOSH turned round and given PUNCH's instant attack: the
        /// noise band starts two octaves up and falls in 0.15 s, some resonance makes the fall audible, a little reverb
        /// carries it away. Louder with more charge (set when it is played).
        /// </summary>
        public static readonly SfxPatch Burst = Make(SfxEngine.Foley,
            frequency: 30, pitchEnv: 96, pitchEnvTime: 70, engineA: 0, engineB: 0, noise: 127, cutoff: 82, resonance: 40, decay: 62,
            reverb: 15);

        // ---- Chimes ----------------------------------------------------------------------------------------------------

        /// <summary>
        /// CHIME – the signature sound: a clear, glassy ping that must be lovely fifty times in a row. Starts from COIN:
        /// a square wave whose arpeggio climbs by fifths every 75 ms while it fades. UI BLIP's arpeggio always takes
        /// three steps and stays on the last, so the notes are E, B, F♯, C♯ at roughly 100 %, 40 %, 17 % and 7 % of
        /// the level: the ear hears E → B and the two faint upper steps as the sparkle (they are outside the
        /// pentatonic; engineA 64 would climb in octaves instead and stay on E). Wide-open filter, a touch of reverb
        /// for the glass. Played LIVE so each one can be a step higher up E minor pentatonic than the last.
        /// </summary>
        public static readonly SfxPatch Chime = Make(SfxEngine.UiBlip,
            pitchEnv: 64, engineA: 37, engineB: 70, cutoff: 127, resonance: 0, decay: 45, reverb: 25, level: 90);

        /// <summary>
        /// CHIME SCATTER – the collect sound broken: the Chime's ping, shorter, bit-crushed and dry. The game plays five
        /// to eight of them in a falling run when carried Chimes spill.
        /// </summary>
        public static readonly SfxPatch ChimeScatter = Make(SfxEngine.UiBlip,
            pitchEnv: 64, engineA: 37, engineB: 90, cutoff: 118, resonance: 0, decay: 35, crush: 55, level: 90);

        // ---- the level's pieces ------------------------------------------------------------------------------------------

        /// <summary>
        /// WOOFER – a fat, rubbery "boing", the most characterful one-shot. From WARP / POWER-UP, low: an FM tone that
        /// sweeps up 1¼ octaves to E3 in 0.1 s (the launch), with an 11 Hz LFO on the pitch for the cone's wobble and a
        /// long enough decay to hear it wobble back.
        /// </summary>
        public static readonly SfxPatch Woofer = Make(SfxEngine.SciFiFm,
            pitchEnv: 44, pitchEnvTime: 62, engineA: 42, engineB: 45, cutoff: 95, resonance: 25, decay: 68,
            lfoRate: 105, lfoPitch: 45);

        /// <summary>
        /// HURT – a harsh, short, falling buzz: the only ugly sound in the game, so it is unmistakable. From ERROR / ZAP:
        /// a low square wave falling 1¼ octaves to D3, heavily bit-crushed, with a resonant, half-closed filter.
        /// </summary>
        public static readonly SfxPatch Hurt = Make(SfxEngine.UiBlip,
            pitchEnv: 84, pitchEnvTime: 55, engineA: 0, engineB: 0, cutoff: 85, resonance: 25, decay: 50, crush: 60);

        /// <summary>
        /// CLIPPER POP – a crunchy pop with a metallic ring. METAL HIT plus PUNCH: an FM clang (ratio just under 3:1, so not quite harmonic)
        /// on B4 that drops into place in 12 ms, a third of it noise for the crunch, a little reverb for the ring.
        /// </summary>
        public static readonly SfxPatch ClipperPop = Make(SfxEngine.Impact,
            pitchEnv: 92, pitchEnvTime: 18, engineA: 75, engineB: 75, noise: 30, cutoff: 112, resonance: 35, decay: 50, reverb: 15);

        /// <summary>
        /// TUNING FORK – a struck fork: a pure, long, ringing E with reverb. From METAL HIT with nearly all the FM taken
        /// out (a faint overtone two octaves up is the strike), no noise, a decay of seconds. The one effect allowed to
        /// ring longer than 1.5 s.
        /// </summary>
        public static readonly SfxPatch TuningFork = Make(SfxEngine.Impact,
            pitchEnv: 64, engineA: 85, engineB: 12, noise: 0, cutoff: 120, resonance: 0, decay: 105, reverb: 70, level: 90);

        /// <summary>
        /// GOAL – a big rising shimmer that leads into the fanfare. From POWER-UP, long: a square wave that rises 1½
        /// octaves to E4 in 0.3 s while its arpeggio climbs in octaves, a slight fast vibrato for the shimmer, delay and
        /// a lot of reverb for size.
        /// </summary>
        public static readonly SfxPatch Goal = Make(SfxEngine.UiBlip,
            pitchEnv: 40, pitchEnvTime: 85, engineA: 64, engineB: 60, cutoff: 115, resonance: 10, decay: 78,
            lfoRate: 110, lfoPitch: 15, delay: 30, reverb: 70, level: 90);

        // ---- movement at speed ---------------------------------------------------------------------------------------------

        /// <summary>
        /// WIND – held, filtered noise from 60 % of top speed. Starts from WIND with less grain (a flutter, not a
        /// crackle): a resonant band of noise, a slow LFO drifting the band so it is never static, slow attack and
        /// release so it can never click. The game moves its level and its band (cutoff) with speed.
        /// </summary>
        public static readonly SfxPatch Wind = Make(SfxEngine.Foley,
            frequency: 30, pitchEnv: 64, engineA: 20, engineB: 35, noise: 127, cutoff: 72, resonance: 50, decay: 60,
            attack: 60, sustain: 127, release: 70, lfoRate: 25, lfoFilter: 35, reverb: 20);

        /// <summary>
        /// SKID – a short, gritty scrape. Starts from CRACKLE, short: noise chopped into dense random grains (the grit),
        /// a high band, a little bit-crush.
        /// </summary>
        public static readonly SfxPatch Skid = Make(SfxEngine.Foley,
            frequency: 30, pitchEnv: 64, engineA: 110, engineB: 127, noise: 127, cutoff: 100, resonance: 20, decay: 45, crush: 25);

        // ---- the run ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// RESPAWN – a gentle upward warp, soft and reassuring. Starts from WARP, quiet: an FM tone that glides up almost
        /// two octaves to E4 over 0.3 s, mild FM, some delay and reverb.
        /// </summary>
        public static readonly SfxPatch Respawn = Make(SfxEngine.SciFiFm,
            pitchEnv: 34, pitchEnvTime: 85, engineA: 40, engineB: 40, cutoff: 100, resonance: 20, decay: 80, delay: 25, reverb: 35);

        /// <summary>UI CONFIRM – one clean, short blip on B5. SELECT as it is: a plain square wave, no arpeggio, short.</summary>
        public static readonly SfxPatch UiConfirm = Make(SfxEngine.UiBlip,
            pitchEnv: 64, engineA: 0, engineB: 0, cutoff: 120, resonance: 0, decay: 35);

        // ---- how each sound is played: the route, the note it is tuned to, its place in the mix ------------------------------
        // The volumes were set from measurements, not by ear: with the music at MusicDirector.musicVolume the whole mix,
        // at full listener volume, stays under 0.95 of full scale in every capture take (Tests/CaptureRecorder.MixCeiling).
        // Raise one and record the takes again to see whether there is still room.

        static readonly SoundDefinition[] Table =
        {
            //                 sound                    patch          route                   note      volume
            new SoundDefinition(GameSound.Jump,          Jump,          SoundRoute.Baked,       Pitch.E5, 0.70f),
            new SoundDefinition(GameSound.Land,          Land,          SoundRoute.Baked,       0,        0.80f),
            new SoundDefinition(GameSound.FootstepLeft,  FootstepLeft,  SoundRoute.Baked,       0,        0.35f),
            new SoundDefinition(GameSound.FootstepRight, FootstepRight, SoundRoute.Baked,       0,        0.35f),
            new SoundDefinition(GameSound.TuckStart,     TuckStart,     SoundRoute.Baked,       Pitch.B4, 0.45f),
            new SoundDefinition(GameSound.RollHum,       RollHum,       SoundRoute.LiveHeld,    Pitch.E2, 0.27f),
            new SoundDefinition(GameSound.WindUpRev,     WindUpRev,     SoundRoute.LiveHeld,    Pitch.E3, 0.35f),
            new SoundDefinition(GameSound.Burst,         Burst,         SoundRoute.Baked,       0,        0.60f),
            new SoundDefinition(GameSound.Chime,         Chime,         SoundRoute.LiveOneShot, Pitch.E5, 0.55f),
            new SoundDefinition(GameSound.ChimeScatter,  ChimeScatter,  SoundRoute.LiveOneShot, Pitch.E5, 0.50f),
            new SoundDefinition(GameSound.Woofer,        Woofer,        SoundRoute.Baked,       Pitch.E3, 0.80f),
            new SoundDefinition(GameSound.Hurt,          Hurt,          SoundRoute.Baked,       Pitch.D3, 0.80f),
            new SoundDefinition(GameSound.ClipperPop,    ClipperPop,    SoundRoute.Baked,       Pitch.B4, 0.75f),
            new SoundDefinition(GameSound.TuningFork,    TuningFork,    SoundRoute.Baked,       Pitch.E5, 0.70f),
            new SoundDefinition(GameSound.Goal,          Goal,          SoundRoute.Baked,       Pitch.E4, 0.80f),
            new SoundDefinition(GameSound.Wind,          Wind,          SoundRoute.LiveHeld,    0,        0.30f),
            new SoundDefinition(GameSound.Skid,          Skid,          SoundRoute.Baked,       0,        0.60f),
            new SoundDefinition(GameSound.Respawn,       Respawn,       SoundRoute.Baked,       Pitch.E4, 0.70f),
            new SoundDefinition(GameSound.UiConfirm,     UiConfirm,     SoundRoute.Baked,       Pitch.B5, 0.70f),
        };

        /// <summary>Every sound effect of the game: its patch, its route, its note and its volume.</summary>
        public static IReadOnlyList<SoundDefinition> All => Table;

        /// <summary>
        /// <c>SfxPatch.Create</c> with a name for every value, so a patch reads like the device's pages. The defaults are
        /// SFX FORGE's own plain patch: a short one-shot, nothing modulated, no effects. (A bare <c>new SfxPatch()</c>
        /// has level 0 and is silent – always start from Create.)
        /// </summary>
        static SfxPatch Make(SfxEngine engine,
            byte frequency = 60, byte pitchEnv = 64, byte pitchEnvTime = 40, byte engineA = 40, byte engineB = 40, byte noise = 0,
            byte cutoff = 110, byte resonance = 10, byte decay = 60, byte crush = 0,
            byte attack = 0, byte sustain = 0, byte release = 34, byte filterEnv = 64, byte filterEnvTime = 40,
            byte lfoRate = 50, byte lfoPitch = 0, byte lfoFilter = 0, byte lfoTimbre = 0,
            byte spread = 0, byte chorus = 0, byte delay = 0, byte reverb = 0, byte level = 100, byte table = 0)
        {
            return SfxPatch.Create(engine,
                frequency, pitchEnv, pitchEnvTime, engineA, engineB, noise, cutoff, resonance, decay, crush,
                attack, sustain, release, filterEnv, filterEnvTime, lfoRate, lfoPitch, lfoFilter, lfoTimbre,
                spread, chorus, delay, reverb, level, table);
        }

        /// <summary>A copy of a patch with one value changed: how a variation of a sound is made.</summary>
        static SfxPatch With(SfxPatch patch, SfxParam parameter, byte value)
        {
            patch[parameter] = value;
            return patch;
        }
    }
}
