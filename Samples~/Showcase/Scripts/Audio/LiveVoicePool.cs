using CatSplat.SfxForge;
using UnityEngine;

namespace SfxShowcase.Audio
{
    /// <summary>
    /// A small pool of <see cref="SfxPlayer"/> voices for one-shots that are synthesized as they play.
    ///
    /// Why a pool: one SfxPlayer plays ONE patch at a time – up to sixteen overlapping notes of it, but giving it a
    /// different patch reshapes whatever it is still sounding. So rapid triggers of the same sound (a line of Chimes)
    /// share one voice and overlap freely, and a different sound takes another voice.
    /// </summary>
    public sealed class LiveVoicePool : MonoBehaviour
    {
        [Tooltip(
            "The pool's voices: SfxPlayer components, each on its own AudioSource. Their Volume is the pool's volume."
        )]
        [SerializeField]
        SfxPlayer[] voices = new SfxPlayer[0];

        [Tooltip(
            "Seconds after a trigger during which a voice counts as busy even if it does not report sound "
                + "yet (the audio thread starts it a moment later)."
        )]
        [Min(0f)]
        [SerializeField]
        float claimSeconds = 0.1f;

        float[] startedAt = new float[0];

        /// <summary>Sets the voices; called by <see cref="AudioInstaller"/>.</summary>
        public void Configure(SfxPlayer[] poolVoices) => voices = poolVoices;

        void Awake()
        {
            startedAt = new float[voices.Length];
            for (int i = 0; i < startedAt.Length; i++)
            {
                startedAt[i] = float.NegativeInfinity;
            }
        }

        /// <summary>
        /// Plays a preset once. velocity 0..1 is its loudness, semitones transposes it, midiNote above 0 plays it as
        /// that note (0 = at the patch's own frequency).
        /// </summary>
        public void Play(SfxPreset preset, float velocity, float semitones, int midiNote)
        {
            if (preset == null || voices.Length == 0 || startedAt.Length != voices.Length)
            {
                return;
            }

            int index = ChooseVoice(preset);
            SfxPlayer voice = voices[index];
            if (voice == null)
            {
                return;
            }

            voice.preset = preset;
            voice.Play(velocity, semitones, midiNote);
            startedAt[index] = Time.time;
        }

        /// <summary>
        /// The voice already playing this preset, else a silent one, else the one that started longest ago.
        /// </summary>
        int ChooseVoice(SfxPreset preset)
        {
            for (int i = 0; i < voices.Length; i++)
            {
                if (voices[i] != null && voices[i].preset == preset)
                {
                    return i;
                }
            }

            int oldest = 0;
            for (int i = 0; i < voices.Length; i++)
            {
                if (voices[i] == null)
                {
                    continue;
                }

                bool busy = voices[i].IsPlaying || Time.time - startedAt[i] < claimSeconds;
                if (!busy)
                {
                    return i;
                }

                if (startedAt[i] < startedAt[oldest])
                {
                    oldest = i;
                }
            }

            return oldest;
        }
    }
}
