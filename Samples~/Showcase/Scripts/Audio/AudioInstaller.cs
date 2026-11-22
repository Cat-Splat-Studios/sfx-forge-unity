using System;
using CatSplat.SfxForge;
using SfxShowcase.Core;
using UnityEngine;

namespace SfxShowcase.Audio
{
    /// <summary>
    /// Builds the game's whole sound as one "Audio" object and wires it – read it top to bottom to see how to set SFX
    /// FORGE up by hand in a scene of your own. Plain runtime code (no editor calls): the scene builder runs it while
    /// generating the scene, and it works the same from a script at runtime.
    ///
    /// <code>
    /// Audio
    /// ├─ Music            AudioSource + SfxMusicPlayer (the song) + MusicDirector
    /// ├─ Effects          AudioSource (baked one-shots) + SoundEffects + LiveVoicePool
    /// │  ├─ Footsteps     AudioSource (baked footsteps; its pitch follows speed)
    /// │  └─ Live Voice n  AudioSource + SfxPlayer (the pool: Chime, scatter)
    /// └─ Continuous       ContinuousSounds
    ///    ├─ Roll Hum      AudioSource + SfxPlayer (held; follows speed)
    ///    ├─ Wind          AudioSource + SfxPlayer (held; follows speed)
    ///    └─ Wind-Up Rev   AudioSource + SfxPlayer (held; follows charge)
    /// </code>
    /// An SfxPlayer / SfxMusicPlayer adds its sound to the AudioSource beside it and keeps that source running itself:
    /// the source needs no clip.
    /// </summary>
    public static class AudioInstaller
    {
        /// <summary>
        /// SfxPlayers in the pool for live one-shots. One per patch that may sound at the same moment (the Chime and
        /// the scatter), and one to spare.
        /// </summary>
        public const int LiveVoiceCount = 3;

        /// <summary>The SfxPlayer volume of the pool's voices; each sound's own volume is its velocity.</summary>
        const float LiveVoiceVolume = 0.8f;

        /// <summary>
        /// Creates the Audio object with everything on it. The sound reads <paramref name="player"/> (speed, rolling,
        /// charge) and <paramref name="session"/> (the phase) and listens to <see cref="GameEvents"/>; that is all it
        /// needs from the game. <paramref name="song"/> is the music; <paramref name="presets"/> turns each patch of
        /// the <see cref="SoundLibrary"/> into an SfxPreset (null: presets are made in memory).
        /// </summary>
        public static void Install(PlayerBase player, GameSessionBase session, SfxSong song, PresetFactory presets)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (presets == null)
            {
                presets = CreatePresetInMemory;
            }

            // every sound of the library becomes an SfxPreset: the patch as an asset the Inspector can show and edit
            var bindings = new SoundBinding[SoundLibrary.All.Count];
            for (int i = 0; i < bindings.Length; i++)
            {
                SoundDefinition definition = SoundLibrary.All[i];
                bindings[i] = new SoundBinding(
                    definition,
                    presets(definition.Name, definition.Patch, definition.HoldSeconds)
                );
            }

            // built inactive and switched on when everything is wired, so
            // that at runtime no component wakes up half set up
            var audio = new GameObject("Audio");
            audio.SetActive(false);
            InstallMusic(audio.transform, player, session, song);
            InstallEffects(audio.transform, bindings);
            InstallContinuous(audio.transform, player, bindings);
            audio.SetActive(true);
        }

        /// <summary>Use 3: the song on an SfxMusicPlayer, and the director that shapes it.</summary>
        static void InstallMusic(Transform root, PlayerBase player, GameSessionBase session, SfxSong song)
        {
            GameObject music = CreateChild(root, "Music");
            var musicPlayer = music.AddComponent<SfxMusicPlayer>(); // (adds the AudioSource it needs)
            Prepare(music.GetComponent<AudioSource>());
            musicPlayer.song = song;
            musicPlayer.playOnEnable = true; // the song plays under the title card
            musicPlayer.startPattern = 0;
            music.AddComponent<MusicDirector>().Wire(musicPlayer, player, session);
        }

        /// <summary>
        /// Use 1: two AudioSources for the baked clips and a pool of SfxPlayers for the live one-shots.
        /// </summary>
        static void InstallEffects(Transform root, SoundBinding[] bindings)
        {
            GameObject effects = CreateChild(root, "Effects");
            AudioSource oneShots = Prepare(effects.AddComponent<AudioSource>());
            AudioSource footsteps = Prepare(CreateChild(effects.transform, "Footsteps").AddComponent<AudioSource>());

            var voices = new SfxPlayer[LiveVoiceCount];
            for (int i = 0; i < voices.Length; i++)
            {
                voices[i] = CreateLivePlayer(effects.transform, $"Live Voice {i + 1}", null, LiveVoiceVolume);
            }

            var pool = effects.AddComponent<LiveVoicePool>();
            pool.Configure(voices);

            effects.AddComponent<SoundEffects>().Configure(bindings, oneShots, footsteps, pool);
        }

        /// <summary>
        /// Use 2: one SfxPlayer for each held sound, its preset assigned, and the component that bends them. The
        /// component is also told the MIDI note of each sound: a held sound is started with
        /// <c>SfxPlayer.Play(velocity, semitones, midiNote)</c> like any other, or it would sound at the patch's FREQ.
        /// </summary>
        static void InstallContinuous(Transform root, PlayerBase player, SoundBinding[] bindings)
        {
            GameObject continuous = CreateChild(root, "Continuous");
            SoundBinding rollHum = Find(bindings, GameSound.RollHum);
            SoundBinding wind = Find(bindings, GameSound.Wind);
            SoundBinding windUp = Find(bindings, GameSound.WindUpRev);

            var sounds = continuous.AddComponent<ContinuousSounds>();
            sounds.Wire(player);
            sounds.WireVoices(
                CreateHeldPlayer(continuous.transform, "Roll Hum", rollHum),
                rollHum.MidiNote,
                CreateHeldPlayer(continuous.transform, "Wind", wind),
                wind.MidiNote,
                CreateHeldPlayer(continuous.transform, "Wind-Up Rev", windUp),
                windUp.MidiNote
            );
        }

        static SfxPlayer CreateHeldPlayer(Transform parent, string name, SoundBinding binding) =>
            CreateLivePlayer(parent, name, binding.Preset, binding.Volume);

        /// <summary>An SfxPlayer on its own object: it synthesizes its preset into the AudioSource beside it.</summary>
        static SfxPlayer CreateLivePlayer(Transform parent, string name, SfxPreset preset, float volume)
        {
            GameObject voice = CreateChild(parent, name);
            var sfxPlayer = voice.AddComponent<SfxPlayer>(); // (adds the AudioSource it needs)
            Prepare(voice.GetComponent<AudioSource>());
            sfxPlayer.preset = preset;
            sfxPlayer.volume = volume;
            sfxPlayer.playOnEnable = false; // the game starts it, not the scene
            return sfxPlayer;
        }

        /// <summary>A plain 2D source with no clip that does nothing by itself.</summary>
        static AudioSource Prepare(AudioSource source)
        {
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            return source;
        }

        static GameObject CreateChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child;
        }

        static SoundBinding Find(SoundBinding[] bindings, GameSound sound)
        {
            foreach (SoundBinding binding in bindings)
            {
                if (binding.Sound == sound)
                {
                    return binding;
                }
            }

            throw new ArgumentException($"The sound library has no entry for {sound}.", nameof(sound));
        }

        /// <summary>The preset factory used when the caller gives none: a preset that only lives in memory.</summary>
        static SfxPreset CreatePresetInMemory(string name, SfxPatch patch, float holdSeconds)
        {
            var preset = ScriptableObject.CreateInstance<SfxPreset>();
            preset.name = name;
            preset.patch = patch;
            preset.holdSeconds = holdSeconds;
            return preset;
        }
    }
}
