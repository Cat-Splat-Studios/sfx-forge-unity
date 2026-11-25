// SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace CatSplat.SfxForge
{
    /// <summary>
    /// Plays a variation set through this object's AudioSource: every Play is the next variant of a shuffled order, a
    /// little higher or lower and a little quieter, as the set says. It is plain AudioSource playback (PlayOneShot), so
    /// spatial blend, roll-off and the mixer group are the AudioSource's own: it works in 3D.
    /// Every play sets the AudioSource's pitch (what it was before is not kept), and one-shots still ringing on the
    /// same AudioSource take the pitch of the next play - for plays that overlap and must stay independent, use
    /// several players.
    /// </summary>
    [AddComponentMenu("SFX Forge/SFX Variation Player")]
    [RequireComponent(typeof(AudioSource))]
    public class SfxVariationPlayer : MonoBehaviour
    {
        /// <summary>The variation set it plays.</summary>
        public SfxVariationSet set;

        /// <summary>The volume of every play, 0..1.</summary>
        [Range(0f, 1f)]
        public float volume = 1f;

        [Tooltip(
            "Make the set's clips when this component is enabled, so that the first plays do not have to "
                + "(only where the set is not baked)."
        )]
        public bool prepareOnEnable = true;

        [Tooltip(
            "0: another order every time. Anything else: the same order of variants, pitches and volumes every run."
        )]
        public int orderSeed;

        AudioSource source;
        SfxShuffle order;
        SfxVariationSet orderOf;
        int last = -1;
        int orderNoRepeat;
        int orderSeedWas;

        /// <summary>The variant played last (0 = the preset itself), -1: none yet.</summary>
        public int Last => last;

        void OnEnable()
        {
            source = GetComponent<AudioSource>();
            if (prepareOnEnable && set != null)
            {
                set.Prepare();
            }
        }

        /// <summary>
        /// Plays the next variant. Returns which one (0..PlayCount-1), -1: nothing played (no set, or it has nothing to
        /// play). One-shots of this AudioSource that still ring take the new pitch.
        /// </summary>
        public int Play(float volumeScale = 1f)
        {
            if (set == null)
            {
                return -1;
            }

            if (source == null)
            {
                source = GetComponent<AudioSource>();
            }

            // another set, another number of variants, another "no repeat" or another seed: a new order
            if (
                order == null
                || orderOf != set
                || order.Count != set.PlayCount
                || orderNoRepeat != set.noRepeat
                || orderSeedWas != orderSeed
            )
            {
                order = set.NewOrder(unchecked((uint)orderSeed));
                orderOf = set;
                orderNoRepeat = set.noRepeat;
                orderSeedWas = orderSeed;
            }

            int played = PlayOn(source, set, order, volume * volumeScale);
            if (played >= 0)
            {
                last = played;
            }

            return played;
        }

        /// <summary>Play(), for a UnityEvent or an animation event.</summary>
        public void PlayNext() => Play();

        /// <summary>
        /// The next variant of an order on any AudioSource: its pitch is set, the clip goes out with PlayOneShot.
        /// Returns the variant, -1: nothing played.
        /// </summary>
        public static int PlayOn(AudioSource source, SfxVariationSet set, SfxShuffle order, float volumeScale = 1f)
        {
            if (source == null || set == null || order == null)
            {
                return -1;
            }

            AudioClip clip = set.Next(order, out float pitch, out float gain);
            if (clip == null)
            {
                return -1;
            }

            source.pitch = pitch;
            source.PlayOneShot(clip, gain * volumeScale);
            int n = set.PlayCount;
            return order.Last < n ? order.Last : order.Last % n;
        }
    }
}
