using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using CatSplat.SfxForge;
using SfxShowcase.Audio;
using SfxShowcase.Core;
using UnityEngine;

namespace SfxShowcase.Tests
{
    /// <summary>
    /// What the game and SFX FORGE were doing in every recorded frame, written as <c>telemetry.json</c> beside the
    /// pictures: the player, the run, the music (intensity, pattern, bar, step, which instruments the intensity lets
    /// play), the three held sounds with their live values, the live voices, and two timed lists – every
    /// <see cref="GameEvents"/> event, and every SFX FORGE sound it started with its route (baked, live, held, music).
    ///
    /// It only listens and reads, like the audio folder does. The list of sounds is worked out from the events with
    /// the same rules <c>SoundEffects</c> uses (a soft landing is silent, a dense line of Chimes skips pings); the held
    /// sounds are read from their <c>SfxPlayer</c>s and from <c>ContinuousSounds</c> (held or let go). A frame's values
    /// are read after the frame's sound was rendered:
    /// the music's bar and step are those at the END of the frame's samples.
    /// </summary>
    public sealed class CaptureTelemetry : IDisposable
    {
        struct TimedEvent
        {
            public int Frame;
            public string Name;
            public string Value;
        }

        struct TimedSound
        {
            public int Frame;
            public string Sound;
            public string Route;
            public string Cause;
            public string Detail;
        }

        readonly PlayerBase player;
        readonly GameSessionBase session;
        readonly SfxMusicPlayer music;
        readonly MusicDirector director;
        readonly SoundEffects effects;
        readonly SfxPlayer[] heldPlayers;
        readonly SfxPlayer[] liveVoices;
        readonly object[] heldSounds; // ContinuousSounds' own HeldSound of each held player
        readonly PropertyInfo heldIsHeld;
        readonly bool[] wasHeld;
        readonly string[] instrumentNames = new string[SfxProtocol.Slots];
        readonly Dictionary<GameSound, SoundDefinition> library = new Dictionary<GameSound, SoundDefinition>();

        readonly List<string> frames = new List<string>();
        readonly List<TimedEvent> events = new List<TimedEvent>();
        readonly List<TimedSound> sounds = new List<TimedSound>();
        readonly StringBuilder line = new StringBuilder(1024);

        // SoundEffects' own rules, read from the component so this list follows its Inspector values
        readonly float landMinimumImpact;
        readonly float chimeMinimumInterval;
        readonly int chimeMaximumSteps;
        readonly int scatterMinimumPings;
        readonly int scatterMaximumPings;
        readonly int scatterChimesPerExtraPing;
        float lastChimeTime = float.NegativeInfinity;
        bool nextFootIsRight;
        int currentFrame;

        /// <summary>Starts listening. Frames are added by <see cref="NoteFrame"/>, which the recorder calls.</summary>
        public CaptureTelemetry(PlayerBase player, GameSessionBase session)
        {
            this.player = player;
            this.session = session;
            music = UnityEngine.Object.FindFirstObjectByType<SfxMusicPlayer>();
            director = UnityEngine.Object.FindFirstObjectByType<MusicDirector>(FindObjectsInactive.Include);
            effects = UnityEngine.Object.FindFirstObjectByType<SoundEffects>();
            var continuous = UnityEngine.Object.FindFirstObjectByType<ContinuousSounds>();
            heldPlayers = continuous != null ? continuous.GetComponentsInChildren<SfxPlayer>() : new SfxPlayer[0];
            liveVoices = effects != null ? effects.GetComponentsInChildren<SfxPlayer>() : new SfxPlayer[0];
            wasHeld = new bool[heldPlayers.Length];
            heldSounds = new object[heldPlayers.Length];
            if (continuous != null)
            {
                // ContinuousSounds keeps "held or let go" to itself; a player's IsPlaying stays true through the tail
                foreach (string field in new[] { "rollHum", "wind", "windUp" })
                {
                    object held = Setting<object>(continuous, field, null);
                    if (held == null)
                    {
                        throw new InvalidOperationException(
                            "ContinuousSounds." + field + " is not set: the telemetry cannot tell whether it is held."
                        );
                    }

                    PropertyInfo ofPlayer = held.GetType().GetProperty("Player");
                    heldIsHeld = held.GetType().GetProperty("IsHeld");
                    if (ofPlayer == null || heldIsHeld == null)
                    {
                        throw new InvalidOperationException(
                            "HeldSound has no Player or IsHeld any more: CaptureTelemetry must follow."
                        );
                    }

                    int index = Array.IndexOf(heldPlayers, (SfxPlayer)ofPlayer.GetValue(held));
                    if (index >= 0)
                    {
                        heldSounds[index] = held;
                    }
                }
            }

            foreach (SoundDefinition definition in SoundLibrary.All)
            {
                library[definition.Id] = definition;
            }

            for (int i = 0; i < instrumentNames.Length; i++)
            {
                instrumentNames[i] = "Sound " + (i + 1);
            }

            foreach (FieldInfo field in typeof(MusicKit).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.IsLiteral && field.FieldType == typeof(int))
                {
                    int index = (int)field.GetRawConstantValue();
                    if (index >= 0 && index < instrumentNames.Length)
                    {
                        instrumentNames[index] = field.Name;
                    }
                }
            }

            landMinimumImpact = Setting(effects, "landMinimumImpact", 4f);
            chimeMinimumInterval = Setting(effects, "chimeMinimumInterval", 0.045f);
            chimeMaximumSteps = Setting(effects, "chimeMaximumSteps", 10);
            scatterMinimumPings = Setting(effects, "scatterMinimumPings", 5);
            scatterMaximumPings = Setting(effects, "scatterMaximumPings", 8);
            scatterChimesPerExtraPing = Setting(effects, "scatterChimesPerExtraPing", 6);

            GameEvents.GameStarted += OnGameStarted;
            GameEvents.Jumped += OnJumped;
            GameEvents.Landed += OnLanded;
            GameEvents.Footstep += OnFootstep;
            GameEvents.TuckStarted += OnTuckStarted;
            GameEvents.WindUpCharged += OnWindUpCharged;
            GameEvents.BurstReleased += OnBurstReleased;
            GameEvents.ChimeCollected += OnChimeCollected;
            GameEvents.ChimesScattered += OnChimesScattered;
            GameEvents.WooferBounced += OnWooferBounced;
            GameEvents.Hurt += OnHurt;
            GameEvents.ClipperDefeated += OnClipperDefeated;
            GameEvents.CheckpointReached += OnCheckpointReached;
            GameEvents.GoalReached += OnGoalReached;
            GameEvents.Skidded += OnSkidded;
            GameEvents.Respawned += OnRespawned;
            GameEvents.UiConfirmed += OnUiConfirmed;
            GameEvents.SectionEntered += OnSectionEntered;
            GameEvents.FinalLaunch += OnFinalLaunch;
        }

        /// <summary>Stops listening.</summary>
        public void Dispose()
        {
            GameEvents.GameStarted -= OnGameStarted;
            GameEvents.Jumped -= OnJumped;
            GameEvents.Landed -= OnLanded;
            GameEvents.Footstep -= OnFootstep;
            GameEvents.TuckStarted -= OnTuckStarted;
            GameEvents.WindUpCharged -= OnWindUpCharged;
            GameEvents.BurstReleased -= OnBurstReleased;
            GameEvents.ChimeCollected -= OnChimeCollected;
            GameEvents.ChimesScattered -= OnChimesScattered;
            GameEvents.WooferBounced -= OnWooferBounced;
            GameEvents.Hurt -= OnHurt;
            GameEvents.ClipperDefeated -= OnClipperDefeated;
            GameEvents.CheckpointReached -= OnCheckpointReached;
            GameEvents.GoalReached -= OnGoalReached;
            GameEvents.Skidded -= OnSkidded;
            GameEvents.Respawned -= OnRespawned;
            GameEvents.UiConfirmed -= OnUiConfirmed;
            GameEvents.SectionEntered -= OnSectionEntered;
            GameEvents.FinalLaunch -= OnFinalLaunch;
        }

        // ---- the events, and the sounds they start ------------------------------------------------------------------

        void OnGameStarted() => Note("GameStarted");

        void OnJumped()
        {
            Note("Jumped");
            Sound(GameSound.Jump, "Jumped");
        }

        void OnTuckStarted()
        {
            Note("TuckStarted");
            Sound(GameSound.TuckStart, "TuckStarted");
        }

        void OnWindUpCharged(float charge01) => Note("WindUpCharged", Number(charge01));

        void OnWooferBounced()
        {
            Note("WooferBounced");
            Sound(GameSound.Woofer, "WooferBounced");
        }

        void OnClipperDefeated()
        {
            Note("ClipperDefeated");
            Sound(GameSound.ClipperPop, "ClipperDefeated");
        }

        void OnSkidded()
        {
            Note("Skidded");
            Sound(GameSound.Skid, "Skidded");
        }

        void OnUiConfirmed()
        {
            Note("UiConfirmed");
            Sound(GameSound.UiConfirm, "UiConfirmed");
        }

        void OnSectionEntered(SectionId section) => Note("SectionEntered", Text(section.ToString()));

        void OnFinalLaunch() => Note("FinalLaunch");

        void OnLanded(float impactSpeed)
        {
            Note("Landed", Number(impactSpeed));
            if (impactSpeed >= landMinimumImpact)
            {
                Sound(GameSound.Land, "Landed", "louder with the impact");
            }
        }

        void OnFootstep(float speed01)
        {
            Note("Footstep", Number(speed01));
            GameSound foot = nextFootIsRight ? GameSound.FootstepRight : GameSound.FootstepLeft;
            nextFootIsRight = !nextFootIsRight;
            Sound(foot, "Footstep", "pitch follows the speed");
        }

        void OnBurstReleased(float charge01)
        {
            Note("BurstReleased", Number(charge01));
            Sound(GameSound.Burst, "BurstReleased", "louder with the charge");
        }

        void OnChimeCollected(int combo)
        {
            Note("ChimeCollected", combo.ToString(CultureInfo.InvariantCulture));
            if (Time.time - lastChimeTime < chimeMinimumInterval)
            {
                return;
            }

            lastChimeTime = Time.time;
            int step = Mathf.Clamp(combo - 1, 0, chimeMaximumSteps);
            Sound(GameSound.Chime, "ChimeCollected", "step " + step + " of the scale");
        }

        void OnChimesScattered(int count)
        {
            Note("ChimesScattered", count.ToString(CultureInfo.InvariantCulture));
            int pings = Mathf.Clamp(
                scatterMinimumPings + count / scatterChimesPerExtraPing,
                scatterMinimumPings,
                scatterMaximumPings
            );
            Sound(GameSound.ChimeScatter, "ChimesScattered", pings + " falling pings");
        }

        void OnHurt()
        {
            Note("Hurt");
            Sound(GameSound.Hurt, "Hurt");
            if (director != null && director.enabled)
            {
                Music("Music dip", "Hurt", "volume down, intensity to 0");
            }
        }

        void OnCheckpointReached(int index)
        {
            Note("CheckpointReached", index.ToString(CultureInfo.InvariantCulture));
            Sound(GameSound.TuningFork, "CheckpointReached");
            if (director != null && director.enabled)
            {
                Music("Bell answer", "CheckpointReached", "three notes on the song's bells");
            }
        }

        void OnGoalReached()
        {
            Note("GoalReached");
            Sound(GameSound.Goal, "GoalReached");
            if (director != null && director.enabled)
            {
                Music("Fanfare", "GoalReached", "the song stops; brass, bells, timpani and strings by hand");
            }
        }

        void OnRespawned(SectionId section)
        {
            Note("Respawned", Text(section.ToString()));
            Sound(GameSound.Respawn, "Respawned");
        }

        void Note(string name, string value = null) =>
            events.Add(
                new TimedEvent
                {
                    Frame = currentFrame,
                    Name = name,
                    Value = value,
                }
            );

        /// <summary>
        /// Adds a sound effect to the list, if SFX FORGE can play it here (a baked sound needs its clip).
        /// </summary>
        void Sound(GameSound sound, string cause, string detail = null)
        {
            if (effects == null || !SfxNative.Available)
            {
                return;
            }

            if (!library.TryGetValue(sound, out SoundDefinition definition))
            {
                return;
            }

            if (definition.Route == SoundRoute.Baked && !effects.HasBakedClip(sound))
            {
                return;
            }

            sounds.Add(
                new TimedSound
                {
                    Frame = currentFrame,
                    Sound = definition.Name,
                    Route = RouteName(definition.Route),
                    Cause = cause,
                    Detail = detail,
                }
            );
        }

        void Music(string what, string cause, string detail)
        {
            if (music == null || !SfxNative.Available)
            {
                return;
            }

            sounds.Add(
                new TimedSound
                {
                    Frame = currentFrame,
                    Sound = what,
                    Route = "music",
                    Cause = cause,
                    Detail = detail,
                }
            );
        }

        static string RouteName(SoundRoute route)
        {
            switch (route)
            {
                case SoundRoute.Baked:
                    return "baked";
                case SoundRoute.LiveOneShot:
                    return "live";
                default:
                    return "held";
            }
        }

        // ---- the frames ---------------------------------------------------------------------------------------------

        /// <summary>
        /// Notes the state of the game for a frame; called by the recorder after the frame's sound was rendered.
        /// </summary>
        public void NoteFrame(int frame, long audioSampleFrames)
        {
            Vector2 position = player.Position;
            Vector2 velocity = player.Velocity;
            SectionId section = session.CurrentSection;
            line.Clear();
            line.Append("{\"f\":")
                .Append(frame)
                .Append(",\"t\":")
                .Append(Number(frame / (float)CaptureRecorder.FramesPerSecond))
                .Append(",\"audioEnd\":")
                .Append(audioSampleFrames)
                .Append(",\"phase\":")
                .Append(Text(session.Phase.ToString()))
                .Append(",\"runSeconds\":")
                .Append(Number(session.ElapsedSeconds))
                .Append(",\"section\":")
                .Append(Text(section.ToString()))
                .Append(",\"sectionName\":")
                .Append(Text(Sections.Get(section).DisplayName))
                .Append(",\"x\":")
                .Append(Number(position.x))
                .Append(",\"y\":")
                .Append(Number(position.y))
                .Append(",\"vx\":")
                .Append(Number(velocity.x))
                .Append(",\"vy\":")
                .Append(Number(velocity.y))
                .Append(",\"speed\":")
                .Append(Number(player.Speed))
                .Append(",\"speed01\":")
                .Append(Number(player.Speed01))
                .Append(",\"grounded\":")
                .Append(Flag(player.IsGrounded))
                .Append(",\"rolling\":")
                .Append(Flag(player.IsRolling))
                .Append(",\"charging\":")
                .Append(Flag(player.IsCharging))
                .Append(",\"charge\":")
                .Append(Number(player.Charge01))
                .Append(",\"chimes\":")
                .Append(session.ChimeCount);
            AppendMusic();
            AppendHeld(frame);
            AppendLive();
            line.Append('}');
            frames.Add(line.ToString());
            currentFrame = frame + 1;
        }

        void AppendMusic()
        {
            if (music == null)
            {
                return;
            }

            float intensity = director != null && director.enabled ? director.Intensity : music.intensity;
            int pattern = music.Pattern;
            line.Append(",\"music\":{\"playing\":")
                .Append(Flag(music.IsPlaying))
                .Append(",\"intensity\":")
                .Append(Number(intensity))
                .Append(",\"volume\":")
                .Append(Number(music.volume))
                .Append(",\"pattern\":")
                .Append(pattern)
                .Append(",\"letter\":")
                .Append(Text(((char)('A' + pattern)).ToString()))
                .Append(",\"bar\":")
                .Append(music.Bar)
                .Append(",\"step\":")
                .Append(music.Step)
                .Append(",\"layers\":[");
            bool first = true;
            for (int i = 0; music.song != null && i < SfxProtocol.Slots; i++)
            {
                if (!LayerPlays(music.song.Layer(i), intensity))
                {
                    continue;
                }

                if (!first)
                {
                    line.Append(',');
                }

                first = false;
                line.Append(i);
            }

            line.Append("]}");
        }

        /// <summary>A sound of the song plays at or above its layer mark; both are 0..127 on the device.</summary>
        static bool LayerPlays(float layer01, float intensity01) =>
            Mathf.RoundToInt(intensity01 * 127f) >= Mathf.RoundToInt(layer01 * 127f);

        void AppendHeld(int frame)
        {
            line.Append(",\"held\":{");
            for (int i = 0; i < heldPlayers.Length; i++)
            {
                SfxPlayer held = heldPlayers[i];
                bool sounding = held.IsPlaying; // true through the tail after it is let go, too
                bool isHeld = heldSounds[i] != null && (bool)heldIsHeld.GetValue(heldSounds[i]);
                if (isHeld != wasHeld[i])
                {
                    wasHeld[i] = isHeld;
                    sounds.Add(
                        new TimedSound
                        {
                            Frame = frame,
                            Sound = NameOf(held),
                            Route = "held",
                            Cause = isHeld ? "is held" : "is let go",
                        }
                    );
                }

                if (i > 0)
                {
                    line.Append(',');
                }

                line.Append(Text(NameOf(held)))
                    .Append(":{\"held\":")
                    .Append(Flag(isHeld))
                    .Append(",\"sounding\":")
                    .Append(Flag(sounding))
                    .Append(",\"pitchSemitones\":")
                    .Append(Number(held.pitchSemitones))
                    .Append(",\"cutoffOctaves\":")
                    .Append(Number(held.cutoffOctaves))
                    .Append(",\"fm\":")
                    .Append(Number(held.fm))
                    .Append(",\"level\":")
                    .Append(Number(held.level))
                    .Append('}');
            }

            line.Append('}');
        }

        void AppendLive()
        {
            line.Append(",\"live\":[");
            for (int i = 0; i < liveVoices.Length; i++)
            {
                if (i > 0)
                {
                    line.Append(',');
                }

                line.Append("{\"sound\":")
                    .Append(liveVoices[i].preset != null ? Text(liveVoices[i].preset.name) : "null")
                    .Append(",\"sounding\":")
                    .Append(Flag(liveVoices[i].IsPlaying))
                    .Append('}');
            }

            line.Append(']');
        }

        static string NameOf(SfxPlayer voice) => voice.preset != null ? voice.preset.name : voice.name;

        // ---- the file -----------------------------------------------------------------------------------------------

        /// <summary>Writes telemetry.json into the take's folder.</summary>
        public void Write(string folder, CaptureRecorder.Summary summary)
        {
            var json = new StringBuilder(frames.Count * 700 + 8192);
            json.Append("{\n");
            json.Append("\"take\":").Append(Text(summary.Take)).Append(",\n");
            json.Append("\"framesPerSecond\":").Append(CaptureRecorder.FramesPerSecond).Append(",\n");
            json.Append("\"width\":")
                .Append(CaptureRecorder.Width)
                .Append(",\"height\":")
                .Append(CaptureRecorder.Height)
                .Append(",\n");
            json.Append("\"frameCount\":").Append(summary.Frames).Append(",\n");
            json.Append("\"audio\":{\"file\":\"audio.wav\",\"sampleRate\":")
                .Append(summary.SampleRate)
                .Append(",\"channels\":2,\"samplesPerFrame\":")
                .Append(summary.SampleRate / CaptureRecorder.FramesPerSecond)
                .Append(",\"sampleFrames\":")
                .Append(summary.AudioFrames)
                .Append(",\"peak\":")
                .Append(Number(summary.Peak, "0.#####"))
                .Append(",\"rms\":")
                .Append(Number(summary.Rms, "0.#####"))
                .Append(",\"clippedSamples\":")
                .Append(summary.ClippedSamples)
                .Append(",\"listenerVolume\":")
                .Append(Number(CaptureRecorder.ListenerVolume))
                .Append("},\n");
            json.Append("\"notes\":[")
                .Append(
                    Text(
                        "Unity mixes in whole audio blocks, so a frame's sound is one or two blocks: audioEnd is the "
                            + "wav's length in sample frames once frame f is rendered, never more than a block from "
                            + "(f + 1) * samplesPerFrame."
                    )
                )
                .Append(',')
                .Append(
                    Text(
                        "An event or sound of frame f is in picture f, and its sound starts in the block(s) rendered "
                            + "for frame f: between the audioEnd of frame f - 1 and that of frame f."
                    )
                )
                .Append(',')
                .Append(
                    Text(
                        "A frame's values are read after the frame's sound was rendered: music.bar and music.step are "
                            + "those at audioEnd."
                    )
                )
                .Append(',')
                .Append(
                    Text(
                        "music.layers lists the song's sounds (by index) whose layer mark the intensity reaches; "
                            + "whether one has notes depends on the pattern."
                    )
                )
                .Append(',')
                .Append(
                    Text(
                        "sounds is worked out from the events with the rules of SoundEffects; held sounds and live "
                            + "voices are read from their SfxPlayers."
                    )
                )
                .Append(',')
                .Append(
                    Text(
                        "A held sound's 'held' is true from Hold to LetGo; 'sounding' is its SfxPlayer's IsPlaying, "
                            + "which stays true while the tail fades. Its values are the last ones written."
                    )
                )
                .Append(',')
                .Append(
                    Text(
                        "The take was recorded at audio.listenerVolume, which makes the mix measurable (Unity limits "
                            + "its output to full scale): the game itself plays at 1 and peaks at audio.peak / "
                            + "audio.listenerVolume, which every take asserts to be under 0.95."
                    )
                )
                .Append(',')
                .Append(
                    Text(
                        "song.byHand lists the notes MusicDirector plays with PlaySound: start and length in "
                            + "sixteenths from the 'music' entry of that name in sounds."
                    )
                )
                .Append("],\n");
            AppendSong(json);
            AppendSections(json);
            AppendLibrary(json);

            json.Append("\"frames\":[\n");
            for (int i = 0; i < frames.Count; i++)
            {
                json.Append(frames[i]).Append(i < frames.Count - 1 ? ",\n" : "\n");
            }

            json.Append("],\n\"events\":[\n");
            for (int i = 0; i < events.Count; i++)
            {
                TimedEvent timed = events[i];
                json.Append("{\"f\":")
                    .Append(timed.Frame)
                    .Append(",\"t\":")
                    .Append(Number(timed.Frame / (float)CaptureRecorder.FramesPerSecond))
                    .Append(",\"name\":")
                    .Append(Text(timed.Name))
                    .Append(",\"value\":")
                    .Append(timed.Value ?? "null")
                    .Append('}')
                    .Append(i < events.Count - 1 ? ",\n" : "\n");
            }

            json.Append("],\n\"sounds\":[\n");
            for (int i = 0; i < sounds.Count; i++)
            {
                TimedSound timed = sounds[i];
                json.Append("{\"f\":")
                    .Append(timed.Frame)
                    .Append(",\"t\":")
                    .Append(Number(timed.Frame / (float)CaptureRecorder.FramesPerSecond))
                    .Append(",\"sound\":")
                    .Append(Text(timed.Sound))
                    .Append(",\"route\":")
                    .Append(Text(timed.Route))
                    .Append(",\"cause\":")
                    .Append(Text(timed.Cause))
                    .Append(",\"detail\":")
                    .Append(timed.Detail != null ? Text(timed.Detail) : "null")
                    .Append('}')
                    .Append(i < sounds.Count - 1 ? ",\n" : "\n");
            }

            json.Append("]\n}\n");
            File.WriteAllText(Path.Combine(folder, "telemetry.json"), json.ToString(), new UTF8Encoding(false));
        }

        void AppendSong(StringBuilder json)
        {
            SfxSong song = music != null ? music.song : null;
            if (song == null)
            {
                json.Append("\"song\":null,\n");
                return;
            }

            json.Append("\"song\":{\"name\":")
                .Append(Text(song.name))
                .Append(",\"bpm\":")
                .Append(song.Bpm)
                .Append(",\"stepsPerBar\":")
                .Append(song.StepsPerPattern)
                .Append(",\"arrangement\":[");
            for (int bar = 0; bar < song.ArrangementBars; bar++)
            {
                json.Append(bar > 0 ? "," : "").Append(song.ArrangementPattern(bar));
            }

            json.Append("],\"instruments\":[\n");
            for (int i = 0; i < SfxProtocol.Slots; i++)
            {
                json.Append("{\"index\":")
                    .Append(i)
                    .Append(",\"name\":")
                    .Append(Text(instrumentNames[i]))
                    .Append(",\"layer\":")
                    .Append(Number(song.Layer(i)))
                    .Append('}')
                    .Append(i < SfxProtocol.Slots - 1 ? ",\n" : "\n");
            }

            json.Append("],\"byHand\":{");
            AppendPhrase(json, "Bell answer", "checkpointAnswer");
            json.Append(',');
            AppendPhrase(json, "Fanfare", "fanfare");
            json.Append("}},\n");
        }

        /// <summary>
        /// The notes of one of MusicDirector's phrases, which it plays by hand on the song's instruments.
        /// </summary>
        void AppendPhrase(StringBuilder json, string name, string field)
        {
            MusicNote[] phrase = Setting(director, field, new MusicNote[0]);
            json.Append(Text(name)).Append(":[");
            for (int i = 0; i < phrase.Length; i++)
            {
                json.Append(i > 0 ? "," : "")
                    .Append("{\"sound\":")
                    .Append(phrase[i].Sound)
                    .Append(",\"start\":")
                    .Append(Number(phrase[i].StartSixteenths))
                    .Append(",\"length\":")
                    .Append(Number(phrase[i].LengthSixteenths))
                    .Append('}');
            }

            json.Append(']');
        }

        void AppendSections(StringBuilder json)
        {
            var cues = Setting<SectionCue[]>(director, "sectionCues", null);
            json.Append("\"sections\":[\n");
            for (int i = 0; i < Sections.All.Count; i++)
            {
                SectionInfo info = Sections.All[i];
                json.Append("{\"id\":")
                    .Append(Text(info.Id.ToString()))
                    .Append(",\"name\":")
                    .Append(Text(info.DisplayName))
                    .Append(",\"startX\":")
                    .Append(Number(info.StartX))
                    .Append(",\"endX\":")
                    .Append(Number(info.EndX));
                if (cues != null)
                {
                    foreach (SectionCue cue in cues)
                    {
                        if (cue.Section != info.Id)
                        {
                            continue;
                        }

                        json.Append(",\"pattern\":")
                            .Append(cue.Pattern)
                            .Append(",\"letter\":")
                            .Append(cue.Pattern >= 0 ? Text(((char)('A' + cue.Pattern)).ToString()) : "null")
                            .Append(",\"intensityFloor\":")
                            .Append(Number(cue.IntensityFloor));
                    }
                }

                json.Append('}').Append(i < Sections.All.Count - 1 ? ",\n" : "\n");
            }

            json.Append("],\n");
        }

        void AppendLibrary(StringBuilder json)
        {
            json.Append("\"soundLibrary\":[\n");
            for (int i = 0; i < SoundLibrary.All.Count; i++)
            {
                SoundDefinition definition = SoundLibrary.All[i];
                json.Append("{\"sound\":")
                    .Append(Text(definition.Name))
                    .Append(",\"route\":")
                    .Append(Text(RouteName(definition.Route)))
                    .Append(",\"engine\":")
                    .Append(Text(definition.Patch.engine.ToString()))
                    .Append(",\"midiNote\":")
                    .Append(definition.MidiNote)
                    .Append(",\"volume\":")
                    .Append(Number(definition.Volume))
                    .Append('}')
                    .Append(i < SoundLibrary.All.Count - 1 ? ",\n" : "\n");
            }

            json.Append("],\n");
        }

        // ---- small things -------------------------------------------------------------------------------------------

        /// <summary>
        /// A private field of a component; the fallback only if the component itself is missing. A field that is not
        /// there any more (renamed) is an error: the telemetry would be wrong without anybody noticing.
        /// </summary>
        static T Setting<T>(object owner, string field, T fallback)
        {
            if (owner == null)
            {
                return fallback;
            }

            FieldInfo info = owner
                .GetType()
                .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (info == null)
            {
                throw new InvalidOperationException(
                    owner.GetType().Name + " has no field '" + field + "' any more: CaptureTelemetry must follow."
                );
            }

            return info.GetValue(owner) is T value ? value : fallback;
        }

        static string Number(float value, string format = "0.###")
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return "0";
            }

            return value.ToString(format, CultureInfo.InvariantCulture);
        }

        static string Flag(bool value) => value ? "true" : "false";

        static string Text(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
