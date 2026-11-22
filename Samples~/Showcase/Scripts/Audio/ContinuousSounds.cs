using CatSplat.SfxForge;
using SfxShowcase.Core;
using UnityEngine;

namespace SfxShowcase.Audio
{
    /// <summary>
    /// USE 2 OF 3 – SOUNDS THAT FOLLOW THE GAME WHILE THEY PLAY. This is what a synthesizer gives that samples cannot:
    /// three held patches, each on an <see cref="SfxPlayer"/> of its own, whose pitch, filter, timbre and level are
    /// written from the player's state every frame.
    ///
    ///  • the ROLL HUM – while rolling on the ground; pitch, brightness and level follow speed;
    ///  • the WIND – from 60 % of top speed; level and band follow speed;
    ///  • the WIND-UP REV – retriggered on every charge tap; pitch and FM depth follow the charge.
    ///
    /// Nothing is sent to this component sixty times a second: it READS <see cref="IPlayerState"/> in Update. Only the
    /// Wind-Up's taps and the Burst arrive as <see cref="GameEvents"/>.
    /// </summary>
    public sealed class ContinuousSounds : MonoBehaviour
    {
        [Header("Wired by AudioInstaller")]
        [Tooltip("The player whose state drives the sounds.")]
        [SerializeField]
        PlayerBase player;

        [Tooltip("The SfxPlayer holding the Roll Hum preset.")]
        [SerializeField]
        SfxPlayer rollHumPlayer;

        [Tooltip("The SfxPlayer holding the Wind preset.")]
        [SerializeField]
        SfxPlayer windPlayer;

        [Tooltip("The SfxPlayer holding the Wind-Up Rev preset.")]
        [SerializeField]
        SfxPlayer windUpPlayer;

        [Tooltip("The MIDI note the Roll Hum is held at (from the sound library). 0 = the patch's own FREQ.")]
        [Range(0, 127)]
        [SerializeField]
        int rollHumNote;

        [Tooltip("The MIDI note the Wind is held at. 0 = the patch's own FREQ: wind is noise and has no note.")]
        [Range(0, 127)]
        [SerializeField]
        int windNote;

        [Tooltip("The MIDI note the Wind-Up Rev is held at (from the sound library). 0 = the patch's own FREQ.")]
        [Range(0, 127)]
        [SerializeField]
        int windUpNote;

        [Header("Roll hum (follows speed)")]
        [Tooltip("Below this speed (part of top speed) a roll makes no hum.")]
        [Range(0f, 1f)]
        [SerializeField]
        float rollMinimumSpeed01 = 0.03f;

        [Tooltip("Semitones the hum has risen at top speed.")]
        [Range(0f, 24f)]
        [SerializeField]
        float rollPitchRange = 12f;

        [Tooltip("The filter at rest, in octaves from the patch's cutoff (negative = darker).")]
        [Range(-4f, 4f)]
        [SerializeField]
        float rollSlowCutoff = -1f;

        [Tooltip("The filter at top speed, in octaves from the patch's cutoff.")]
        [Range(-4f, 4f)]
        [SerializeField]
        float rollFastCutoff = 1.5f;

        [Tooltip("The hum's level when barely moving.")]
        [Range(0f, 1f)]
        [SerializeField]
        float rollSlowLevel = 0.35f;

        [Header("Wind (follows speed)")]
        [Tooltip("The speed (part of top speed) at which the wind begins.")]
        [Range(0f, 1f)]
        [SerializeField]
        float windStartSpeed01 = 0.6f;

        [Tooltip("Seconds the wind takes to rise to full or fall to nothing, so it never jumps.")]
        [Min(0.01f)]
        [SerializeField]
        float windFadeSeconds = 0.4f;

        [Tooltip("The wind's band where it begins, in octaves from the patch's band (negative = lower, duller).")]
        [Range(-4f, 4f)]
        [SerializeField]
        float windSlowCutoff = -1.5f;

        [Tooltip("The wind's band at top speed, in octaves from the patch's band.")]
        [Range(-4f, 4f)]
        [SerializeField]
        float windFastCutoff = 1.5f;

        [Header("Wind-Up rev (follows charge)")]
        [Tooltip("Semitones the rev has risen at full charge.")]
        [Range(0f, 24f)]
        [SerializeField]
        float windUpPitchRange = 12f;

        [Tooltip("FM depth added at full charge: the rev gets brighter and rougher as it winds.")]
        [Range(0f, 1f)]
        [SerializeField]
        float windUpTimbreRange = 0.6f;

        [Tooltip("Octaves the filter has opened at full charge.")]
        [Range(0f, 4f)]
        [SerializeField]
        float windUpCutoffRange = 1f;

        [Tooltip(
            "Seconds after a charge tap during which the rev keeps sounding even if the player does not "
                + "report charging yet."
        )]
        [Min(0f)]
        [SerializeField]
        float windUpGraceSeconds = 0.15f;

        const float Silent = 0.001f;

        HeldSound rollHum;
        HeldSound wind;
        HeldSound windUp;
        float windLevel;
        float lastChargeTapTime = float.NegativeInfinity;

        /// <summary>Sets the player to read; called by <see cref="AudioInstaller"/>.</summary>
        public void Wire(PlayerBase playerToFollow) => player = playerToFollow;

        /// <summary>
        /// Sets the three SfxPlayers and the MIDI note each is held at (0 = the patch's own FREQ); called by
        /// <see cref="AudioInstaller"/>.
        /// </summary>
        public void WireVoices(
            SfxPlayer rollHumVoice,
            int rollHumMidiNote,
            SfxPlayer windVoice,
            int windMidiNote,
            SfxPlayer windUpVoice,
            int windUpMidiNote
        )
        {
            rollHumPlayer = rollHumVoice;
            rollHumNote = rollHumMidiNote;
            windPlayer = windVoice;
            windNote = windMidiNote;
            windUpPlayer = windUpVoice;
            windUpNote = windUpMidiNote;
        }

        void Awake()
        {
            rollHum = new HeldSound(rollHumPlayer, rollHumNote);
            wind = new HeldSound(windPlayer, windNote);
            windUp = new HeldSound(windUpPlayer, windUpNote);
        }

        void OnEnable()
        {
            GameEvents.WindUpCharged += OnWindUpCharged;
            GameEvents.BurstReleased += OnBurstReleased;
        }

        void OnDisable()
        {
            GameEvents.WindUpCharged -= OnWindUpCharged;
            GameEvents.BurstReleased -= OnBurstReleased;
            rollHum.LetGo();
            wind.LetGo();
            windUp.LetGo();
        }

        void Update()
        {
            if (player == null)
            {
                return;
            }

            UpdateRollHum(player);
            UpdateWind(player);
            UpdateWindUp(player);
        }

        /// <summary>
        /// An engine hum that follows speed: held while rolling on the ground, higher, brighter and louder
        /// the faster it goes.
        /// </summary>
        void UpdateRollHum(IPlayerState state)
        {
            float speed01 = state.Speed01;
            bool rolling = state.IsRolling && state.IsGrounded && !state.IsCharging && speed01 > rollMinimumSpeed01;
            if (!rolling)
            {
                rollHum.LetGo();
                return;
            }

            rollHum.Hold();
            if (!rollHum.IsHeld)
            {
                return;
            }

            rollHumPlayer.pitchSemitones = speed01 * rollPitchRange;
            rollHumPlayer.cutoffOctaves = Mathf.Lerp(rollSlowCutoff, rollFastCutoff, speed01);
            rollHumPlayer.level = Mathf.Lerp(rollSlowLevel, 1f, speed01);
        }

        /// <summary>Wind that is silent below the start speed, then rises in level and in band with speed.</summary>
        void UpdateWind(IPlayerState state)
        {
            float strength = Mathf.InverseLerp(windStartSpeed01, 1f, state.Speed01);
            windLevel = Mathf.MoveTowards(windLevel, strength, Time.deltaTime / windFadeSeconds);
            if (windLevel <= Silent)
            {
                wind.LetGo();
                return;
            }

            wind.Hold();
            if (!wind.IsHeld)
            {
                return;
            }

            windPlayer.level = windLevel;
            windPlayer.cutoffOctaves = Mathf.Lerp(windSlowCutoff, windFastCutoff, windLevel);
        }

        /// <summary>
        /// While charging, the rev's pitch and timbre follow the charge – also down again as the charge leaks.
        /// </summary>
        void UpdateWindUp(IPlayerState state)
        {
            if (!windUp.IsHeld)
            {
                return;
            }

            bool justTapped = Time.time - lastChargeTapTime < windUpGraceSeconds;
            if (!state.IsCharging && !justTapped)
            {
                windUp.LetGo();
                return;
            }

            if (state.IsCharging)
            {
                ApplyCharge(state.Charge01);
            }
        }

        /// <summary>
        /// Every tap starts the rev again (a new attack: the tap is heard) at the pitch of the new charge.
        /// </summary>
        void OnWindUpCharged(float charge01)
        {
            lastChargeTapTime = Time.time;
            ApplyCharge(charge01);
            windUp.Retrigger();
        }

        /// <summary>The Burst takes over: the whoosh is a one-shot in <see cref="SoundEffects"/>.</summary>
        void OnBurstReleased(float charge01) => windUp.LetGo();

        void ApplyCharge(float charge01)
        {
            if (windUpPlayer == null)
            {
                return;
            }

            windUpPlayer.pitchSemitones = charge01 * windUpPitchRange;
            windUpPlayer.fm = charge01 * windUpTimbreRange;
            windUpPlayer.cutoffOctaves = charge01 * windUpCutoffRange;
        }
    }
}
