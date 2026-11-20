using System.Collections;
using System.Collections.Generic;
using CatSplat.SfxForge;
using SfxShowcase.Core;
using UnityEngine;

namespace SfxShowcase.Audio
{
    /// <summary>
    /// USE 1 OF 3 – ONE-SHOTS. Listens to <see cref="GameEvents"/> and plays the matching sound effect; gameplay never
    /// calls this component. Two routes are shown, chosen per sound in <see cref="SoundLibrary"/>:
    ///
    ///  • BAKED – at start every baked sound is rendered once with <c>SfxPreset.Bake</c> into an AudioClip (no audio file
    ///    exists anywhere) and then played with <c>AudioSource.PlayOneShot</c>. Right for short sounds that fire often
    ///    and are always the same; the volume can still follow the game (landing by impact, Burst by charge).
    ///  • LIVE – the Chime is synthesized per trigger by an <see cref="SfxPlayer"/> from the <see cref="LiveVoicePool"/>,
    ///    because each Chime of a combo is played one step higher. A sample could not do that without a clip per pitch.
    ///
    /// The held sounds (roll, wind, Wind-Up) are in <see cref="ContinuousSounds"/>; the music in <see cref="MusicDirector"/>.
    /// </summary>
    public sealed class SoundEffects : MonoBehaviour
    {
        [Header("Wired by AudioInstaller")]
        [Tooltip("Every sound effect of the game with its preset. Baked and LiveOneShot entries are played here.")]
        [SerializeField] SoundBinding[] sounds = new SoundBinding[0];

        [Tooltip("Plays the baked clips (PlayOneShot, so they overlap). 2D, no clip of its own.")]
        [SerializeField] AudioSource oneShotSource;

        [Tooltip("Plays the baked footsteps. A source of its own because its pitch follows the player's speed.")]
        [SerializeField] AudioSource footstepSource;

        [Tooltip("The SfxPlayer voices that synthesize the live one-shots (the Chime, the scatter).")]
        [SerializeField] LiveVoicePool liveVoices;

        [Header("Landing")]
        [Tooltip("Impact speed (m/s) below which a landing is silent.")]
        [Min(0f)] [SerializeField] float landMinimumImpact = 4f;

        [Tooltip("Impact speed (m/s) at which a landing is at full volume.")]
        [Min(0.1f)] [SerializeField] float landFullImpact = 25f;

        [Tooltip("The volume of the softest audible landing, as a part of the full volume.")]
        [Range(0f, 1f)] [SerializeField] float landMinimumVolume = 0.25f;

        [Header("Footsteps")]
        [Tooltip("The footstep AudioSource's pitch at walking pace.")]
        [Range(0.5f, 2f)] [SerializeField] float footstepSlowPitch = 1f;

        [Tooltip("The footstep AudioSource's pitch at top speed: the ticks rise a little as the player speeds up.")]
        [Range(0.5f, 2f)] [SerializeField] float footstepFastPitch = 1.3f;

        [Header("Burst")]
        [Tooltip("The volume of a Burst released with the smallest charge, as a part of the full volume.")]
        [Range(0f, 1f)] [SerializeField] float burstMinimumVolume = 0.5f;

        [Header("Chime combo")]
        [Tooltip("How many steps up E minor pentatonic the combo may climb (10 = two octaves).")]
        [Min(0)] [SerializeField] int chimeMaximumSteps = 10;

        [Tooltip("The shortest time between two Chime pings, seconds. Keeps a dense line of Chimes from stacking more than about four ringing pings.")]
        [Min(0f)] [SerializeField] float chimeMinimumInterval = 0.045f;

        [Header("Chime scatter")]
        [Tooltip("The fewest pings of the spill when carried Chimes scatter.")]
        [Min(1)] [SerializeField] int scatterMinimumPings = 5;

        [Tooltip("The most pings of the spill.")]
        [Min(1)] [SerializeField] int scatterMaximumPings = 8;

        [Tooltip("One more ping for every this many Chimes scattered.")]
        [Min(1)] [SerializeField] int scatterChimesPerExtraPing = 6;

        [Tooltip("Seconds between two pings of the spill.")]
        [Min(0.01f)] [SerializeField] float scatterPingInterval = 0.055f;

        [Tooltip("How much quieter the last ping of the spill is than the first, as a part of the volume.")]
        [Range(0f, 1f)] [SerializeField] float scatterFade = 0.5f;

        readonly Dictionary<GameSound, SoundBinding> bindings = new Dictionary<GameSound, SoundBinding>();
        readonly Dictionary<GameSound, AudioClip> bakedClips = new Dictionary<GameSound, AudioClip>();
        bool nextFootIsRight;
        float lastChimeTime = float.NegativeInfinity;

        /// <summary>Sets the sounds and the components that play them; called by <see cref="AudioInstaller"/>.</summary>
        public void Configure(SoundBinding[] soundBindings, AudioSource oneShots, AudioSource footsteps, LiveVoicePool voicePool)
        {
            sounds = soundBindings;
            oneShotSource = oneShots;
            footstepSource = footsteps;
            liveVoices = voicePool;
        }

        /// <summary>Whether the clip of a baked sound exists (false before Awake, for live sounds and without the native library).</summary>
        public bool HasBakedClip(GameSound sound) => bakedClips.ContainsKey(sound);

        void Awake()
        {
            foreach (SoundBinding binding in sounds) bindings[binding.Sound] = binding;

            // SFX FORGE's engine is a native library (Windows x64). Without it nothing can be synthesized: say so once
            // and stay silent – every Play method below simply finds no clip and no running voice.
            if (!SfxNative.Available)
            {
                Debug.LogWarning("SFX FORGE's native library is not available on this platform: the showcase runs without sound.", this);
                return;
            }
            BakeClips();
        }

        /// <summary>Renders every baked sound once, at the output's sample rate and as the note it is tuned to.</summary>
        void BakeClips()
        {
            int sampleRate = AudioSettings.outputSampleRate;
            foreach (SoundBinding binding in sounds)
            {
                if (binding.Route != SoundRoute.Baked || binding.Preset == null) continue;
                AudioClip clip = binding.Preset.Bake(sampleRate, midiNote: binding.MidiNote);
                if (clip != null) bakedClips[binding.Sound] = clip;
            }
        }

        void OnDestroy()
        {
            foreach (AudioClip clip in bakedClips.Values) Destroy(clip);
            bakedClips.Clear();
        }

        void OnEnable()
        {
            GameEvents.Jumped += OnJumped;
            GameEvents.Landed += OnLanded;
            GameEvents.Footstep += OnFootstep;
            GameEvents.TuckStarted += OnTuckStarted;
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
        }

        void OnDisable()
        {
            GameEvents.Jumped -= OnJumped;
            GameEvents.Landed -= OnLanded;
            GameEvents.Footstep -= OnFootstep;
            GameEvents.TuckStarted -= OnTuckStarted;
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
        }

        // ---- the plain one-shots: an event, a baked clip ----------------------------------------------------------------

        void OnJumped() => PlayBaked(GameSound.Jump);
        void OnTuckStarted() => PlayBaked(GameSound.TuckStart);
        void OnWooferBounced() => PlayBaked(GameSound.Woofer);
        void OnHurt() => PlayBaked(GameSound.Hurt);
        void OnClipperDefeated() => PlayBaked(GameSound.ClipperPop);
        void OnCheckpointReached(int index) => PlayBaked(GameSound.TuningFork);
        void OnGoalReached() => PlayBaked(GameSound.Goal);
        void OnSkidded() => PlayBaked(GameSound.Skid);
        void OnRespawned(SectionId section) => PlayBaked(GameSound.Respawn);
        void OnUiConfirmed() => PlayBaked(GameSound.UiConfirm);

        // ---- one-shots that vary with the game ----------------------------------------------------------------------------

        /// <summary>A harder landing is louder; a step off a kerb is silent.</summary>
        void OnLanded(float impactSpeed)
        {
            if (impactSpeed < landMinimumImpact) return;
            float hardness = Mathf.InverseLerp(landMinimumImpact, landFullImpact, impactSpeed);
            PlayBaked(GameSound.Land, Mathf.Lerp(landMinimumVolume, 1f, hardness));
        }

        /// <summary>Left, right, left … and a little higher the faster the player runs.</summary>
        void OnFootstep(float speed01)
        {
            GameSound foot = nextFootIsRight ? GameSound.FootstepRight : GameSound.FootstepLeft;
            nextFootIsRight = !nextFootIsRight;
            if (footstepSource == null || !bakedClips.TryGetValue(foot, out AudioClip clip)) return;
            footstepSource.pitch = Mathf.Lerp(footstepSlowPitch, footstepFastPitch, speed01);
            footstepSource.PlayOneShot(clip, bindings[foot].Volume);
        }

        /// <summary>More charge, more whoosh.</summary>
        void OnBurstReleased(float charge01) => PlayBaked(GameSound.Burst, Mathf.Lerp(burstMinimumVolume, 1f, charge01));

        /// <summary>
        /// The signature sound, played live: every Chime collected within the combo window is one step further up
        /// E minor pentatonic (E G A B D E …), so a line of Chimes plays a rising run that fits the song.
        /// </summary>
        void OnChimeCollected(int combo)
        {
            if (Time.time - lastChimeTime < chimeMinimumInterval) return;
            lastChimeTime = Time.time;
            int step = Mathf.Clamp(combo - 1, 0, chimeMaximumSteps);
            PlayLive(GameSound.Chime, 1f, Pitch.PentatonicSemitones(step));
        }

        void OnChimesScattered(int count) => StartCoroutine(PlayScatter(count));

        /// <summary>The collect sound running backwards and broken: a quick run of crushed pings, falling and fading.</summary>
        IEnumerator PlayScatter(int count)
        {
            int pings = Mathf.Clamp(scatterMinimumPings + count / scatterChimesPerExtraPing, scatterMinimumPings, scatterMaximumPings);
            var wait = new WaitForSeconds(scatterPingInterval);
            for (int ping = 0; ping < pings; ping++)
            {
                float velocity = 1f - scatterFade * ping / pings;
                PlayLive(GameSound.ChimeScatter, velocity, Pitch.PentatonicSemitones(pings - 1 - ping));
                yield return wait;
            }
        }

        // ---- the two routes ---------------------------------------------------------------------------------------------------

        /// <summary>Plays a baked clip; several may overlap. Silent if the clip was not baked.</summary>
        void PlayBaked(GameSound sound, float volumeScale = 1f)
        {
            if (oneShotSource == null || !bakedClips.TryGetValue(sound, out AudioClip clip)) return;
            oneShotSource.PlayOneShot(clip, bindings[sound].Volume * volumeScale);
        }

        /// <summary>Synthesizes a sound now, transposed by semitones from the note it is tuned to.</summary>
        void PlayLive(GameSound sound, float velocityScale, float semitones)
        {
            if (liveVoices == null || !bindings.TryGetValue(sound, out SoundBinding binding)) return;
            liveVoices.Play(binding.Preset, binding.Volume * velocityScale, semitones, binding.MidiNote);
        }
    }
}
