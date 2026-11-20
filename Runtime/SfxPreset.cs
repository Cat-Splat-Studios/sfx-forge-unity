// SPDX-License-Identifier: MIT
using UnityEngine;

namespace CatSplat.SfxForge
{
    /// <summary>One SFX FORGE sound as an asset: made here, or captured from the device (Window > SFX Forge).</summary>
    [CreateAssetMenu(menuName = "SFX Forge/Preset", fileName = "New SFX", order = 220)]
    public class SfxPreset : ScriptableObject
    {
        public SfxPatch patch = SfxPatch.Default;

        [Tooltip("For a sustained sound: how long a baked clip holds it before letting go.")]
        [Min(0.05f)] public float holdSeconds = 1.5f;

        /// <summary>
        /// The sound with its effects as a stereo AudioClip, rendered by the device's engine. midiNote above 0: as that
        /// note. Null where the native library is missing.
        /// </summary>
        public AudioClip Bake(int sampleRate = 48000, float semitones = 0f, float velocity = 1f, int midiNote = 0)
        {
            float[] samples = SfxNative.BakeInterleaved(patch, sampleRate, velocity, midiNote, semitones, holdSeconds);
            if (samples == null || samples.Length == 0) return null;
            var clip = AudioClip.Create(name, samples.Length / 2, 2, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
