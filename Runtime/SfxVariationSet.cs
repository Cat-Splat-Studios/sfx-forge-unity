// SPDX-License-Identifier: MIT
using UnityEngine;

namespace CatSplat.SfxForge
{
    /// <summary>
    /// One sound as several: a preset and a seed give a fixed row of variants (the first is the preset itself), and a
    /// play takes the next one in a shuffled order, a little higher or lower, a little quieter - footsteps, hits, coins
    /// that do not sound the same twice. BAKE in its Inspector writes the variants as clips, which play on every
    /// platform; without them the variants are made when first played, where the native library is (Windows x64).
    /// Baked clips are what plays for as long as the set has them - also after the preset or the set changed (it is
    /// then out of date and says so in the editor), and also when count was changed since: then as many variants play
    /// as there are clips. Play it with an SfxVariationPlayer, or with Next and an AudioSource of your own.
    /// </summary>
    [CreateAssetMenu(menuName = "SFX Forge/Variation Set", fileName = "New Variation Set", order = 221)]
    public class SfxVariationSet : ScriptableObject, ISerializationCallbackReceiver
    {
        /// <summary>The asset format this version of SFX Forge writes.</summary>
        public const int CurrentFormat = SfxAssetFormat.Current;

        /// <summary>The most variants a set has.</summary>
        public const int MaxCount = 16;

        // Which asset format wrote this file. A file without the line is format 1.
        [SerializeField, HideInInspector]
        int format = CurrentFormat;

        // Which asset format saved this file last: always this version's number when this version writes the file.
        // A newer SFX Forge that finds its own "format" with a lower number here knows the file went through an older
        // version, which wrote back only what it knew. (Nothing reads it in this version.)
        [SerializeField, HideInInspector]
        int savedBy = CurrentFormat;

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            savedBy = CurrentFormat;
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize() { }

        [Tooltip("The sound this set varies. Variant 1 is this sound itself.")]
        public SfxPreset preset;

        [Tooltip("Which variants these are: the same preset, seed, amount and locks give the same variants again.")]
        public int seed = 1;

        [Tooltip("How many variants, the preset itself included.")]
        [Range(1, MaxCount)]
        public int count = 6;

        [Tooltip(
            "How far a variant's values move from the preset's, in percent. 0: every variant is the preset "
                + "(pitch and volume still vary)."
        )]
        [Range(0, 100)]
        public int amount = 10;

        [Tooltip("The values the variants keep as the preset has them: a bit for each SfxParam (SfxMutate.Bit).")]
        public int locked;

        [Tooltip("A play is tuned up or down by up to this many cents (100 = a semitone).")]
        [Range(0f, 1200f)]
        public float pitchCents = 50f;

        [Tooltip("A play is quieter by up to this many dB.")]
        [Range(0f, 24f)]
        public float volumeDb = 2f;

        [Tooltip("The last N variants played are not chosen again.")]
        [Min(0)]
        public int noRepeat = 2;

        [Tooltip("The clips as one channel.")]
        public bool mono;

        // what BAKE wrote, and the stamp of what it was made from
        [SerializeField]
        AudioClip[] clips = new AudioClip[0];

        [SerializeField]
        int bakedStamp;

        [System.NonSerialized]
        AudioClip[] made; // clips made here, when there are no baked ones

        // ... and the variants the engine gave no sound for (not tried again)
        [System.NonSerialized]
        bool[] refused;

        [System.NonSerialized]
        int madeStamp;

        [System.NonSerialized]
        bool saidNothing;

        [System.NonSerialized]
        bool saidStale;

        [System.NonSerialized]
        double staleLooked = -10.0;

        // counts NewOrder(0): two orders made in the same moment still differ
        static uint orders;

        /// <summary>For the package's tests: a set as on a platform without the native library.</summary>
        internal static bool PretendNativeMissing;
        static bool NativeThere => SfxNative.Available && !PretendNativeMissing;

        /// <summary>The asset format the file was written in: 1 today.</summary>
        public int Format => format;

        /// <summary>A newer SFX Forge made this asset: there may be more in its file than this version reads.</summary>
        public bool IsFromNewerVersion => format > CurrentFormat;

        void OnEnable() => SfxAssetFormat.WarnIfNewer(this, format);

        void OnDisable() => ReleaseMadeClips();

        /// <summary>How many variants: count, inside 1..MaxCount.</summary>
        public int Count => Mathf.Clamp(count, 1, MaxCount);

        /// <summary>The amount inside 0..100.</summary>
        public int Amount => Mathf.Clamp(amount, 0, 100);

        /// <summary>
        /// Variant index (0..Count-1) as a patch: 0 is the preset's own, n is SfxMutate.Variant of it. Raising count
        /// adds variants and leaves these as they are. Without a preset: an empty patch.
        /// </summary>
        public SfxPatch VariantPatch(int index) =>
            preset == null
                ? default
                : SfxMutate.Variant(preset.patch, unchecked((uint)seed), index, Amount, unchecked((uint)locked));

        /// <summary>
        /// A number for everything a bake would be made from now (SfxMutate.Stamp). 0 without a preset.
        /// </summary>
        public int Stamp =>
            preset == null
                ? 0
                : SfxMutate.Stamp(
                    preset.patch.Pack(),
                    Mathf.RoundToInt(preset.holdSeconds * 100f),
                    unchecked((uint)seed),
                    Count,
                    Amount,
                    unchecked((uint)locked),
                    mono
                );

        /// <summary>
        /// BAKE wrote the set's clips, and none of them is missing: they are what plays. (After count was changed there
        /// are not as many as Count says - the set is then out of date, IsStale, and plays the clips it
        /// has: BakedCount.)
        /// </summary>
        public bool IsBaked
        {
            get
            {
                if (clips == null || clips.Length == 0)
                {
                    return false;
                }

                foreach (AudioClip clip in clips)
                {
                    if (clip == null)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>How many clips BAKE wrote; 0 while the set is not baked.</summary>
        public int BakedCount => IsBaked ? clips.Length : 0;

        /// <summary>How many variants a play chooses from: the baked clips while there are any, else Count.</summary>
        public int PlayCount => IsBaked ? clips.Length : Count;

        /// <summary>Baked, and the preset or the set changed since (count too): the clips are the old sound.</summary>
        public bool IsStale => IsBaked && Stamp != bakedStamp;

        /// <summary>
        /// The clip of a variant (0..PlayCount-1): the baked one whenever the set is baked (also when it is out of
        /// date); otherwise made now, where the native library is - at 48 kHz, sample for sample what BAKE writes - and
        /// kept until the asset unloads or what it is made from changes. Null: no preset, no clips and no library, or
        /// the engine gave no sound for this variant (it is not tried again until something changes).
        /// </summary>
        public AudioClip Clip(int index)
        {
            if (index < 0)
            {
                return null;
            }

            if (IsBaked)
            {
                return index < clips.Length ? clips[index] : null;
            }

            int n = Count;
            if (index >= n || preset == null || !NativeThere)
            {
                return null;
            }

            int stamp = Stamp;
            if (made == null || made.Length != n || madeStamp != stamp)
            {
                ReleaseMadeClips();
                made = new AudioClip[n];
                refused = new bool[n];
                madeStamp = stamp;
            }

            if (made[index] == null)
            {
                if (refused[index])
                {
                    return null;
                }

                var options = new SfxBakeOptions { mono = mono };
                SfxBaked baked = SfxBake.Render(VariantPatch(index), preset.holdSeconds, options);
                if (baked == null || baked.frames <= 0)
                {
                    refused[index] = true;
                    return null;
                }

                var clip = AudioClip.Create(
                    name + "_" + (index + 1).ToString("00"),
                    baked.frames,
                    baked.channels,
                    baked.sampleRate,
                    false
                );
                clip.hideFlags = HideFlags.HideAndDontSave;
                clip.SetData(baked.samples, 0);
                made[index] = clip;
            }

            return made[index];
        }

        /// <summary>
        /// Every clip now, so that no play has to make one (each costs a render and some garbage): call it when a level
        /// loads. False: nothing can play - no preset, or no baked clips and no native library (said once
        /// in the Console).
        /// </summary>
        public bool Prepare()
        {
            bool all = true;
            for (int i = 0, n = PlayCount; i < n; i++)
            {
                all &= Clip(i) != null;
            }

            if (!all)
            {
                SayNothingPlays();
            }

            return all;
        }

        /// <summary>
        /// A new order for the variants a play chooses from (PlayCount of them; it keeps "the last N are not chosen").
        /// seed 0: another one every time it is asked for - two players that start in the same moment
        /// do not play alike.
        /// </summary>
        public SfxShuffle NewOrder(uint seed = 0)
        {
            if (seed == 0)
            {
                seed = SfxRandom.Mix(
                    unchecked((uint)System.Environment.TickCount * 2654435761u) ^ unchecked((uint)GetInstanceID()),
                    ++orders
                );
            }

            return new SfxShuffle(PlayCount, noRepeat, seed);
        }

        /// <summary>
        /// The next play without its clip: which variant, and the pitch and the volume (0..1) it is played with. The
        /// pitch is 2^(cents / 1200) with cents anywhere in -pitchCents..+pitchCents, the volume 10^(-dB / 20) with dB
        /// anywhere in 0..volumeDb - never above 1, an AudioSource cannot be louder.
        /// </summary>
        public int NextIndex(SfxShuffle order, out float pitch, out float volume)
        {
            pitch = 1f;
            volume = 1f;
            if (order == null)
            {
                return -1;
            }

            int index = order.Next();
            float cents = (order.NextUnit() * 2f - 1f) * Mathf.Clamp(pitchCents, 0f, 1200f);
            float quieter = order.NextUnit() * Mathf.Clamp(volumeDb, 0f, 24f);
            pitch = Mathf.Pow(2f, cents / 1200f);
            volume = Mathf.Min(1f, Mathf.Pow(10f, -quieter / 20f));
            return index;
        }

        /// <summary>
        /// The next play: the clip, and the pitch and the volume (0..1) to play it with - AudioSource.pitch and the
        /// scale of PlayOneShot. order.Last is the variant chosen. Null: nothing can play.
        /// </summary>
        public AudioClip Next(SfxShuffle order, out float pitch, out float volume)
        {
            int index = NextIndex(order, out pitch, out volume);
            if (index < 0)
            {
                return null;
            }

            int n = PlayCount;

            // (an order made for more variants than there are now)
            if (index >= n)
            {
                index %= n;
            }

            AudioClip clip = Clip(index);
            if (clip == null)
            {
                SayNothingPlays();
            }
#if UNITY_EDITOR
            else if (!saidStale && Time.realtimeSinceStartupAsDouble - staleLooked > 1.0 && Looked() && IsStale)
            {
                // (looked at once a second at most: it packs the patch)
                saidStale = true;
                Debug.LogWarning(
                    "SFX Forge: variation set '"
                        + name
                        + "' is out of date - the preset or the set changed since BAKE: the clips are the old sound. "
                        + "BAKE again.",
                    this
                );
            }
#endif
            return clip;
        }

#if UNITY_EDITOR
        bool Looked()
        {
            staleLooked = Time.realtimeSinceStartupAsDouble;
            return true;
        }
#endif

        void SayNothingPlays()
        {
            if (saidNothing)
            {
                return;
            }

            saidNothing = true;
            if (preset == null)
            {
                Debug.LogWarning(
                    "SFX Forge: variation set '"
                        + name
                        + "' has no preset - nothing plays. Choose the sound it varies.",
                    this
                );
            }
            else
            {
                Debug.LogWarning(
                    "SFX Forge: variation set '"
                        + name
                        + "' has no baked clips and the native library is not available on this platform - nothing "
                        + "plays. BAKE it in the editor.",
                    this
                );
            }
        }

        /// <summary>
        /// Destroys the clips that were made here (not the baked ones). Also done when the asset unloads.
        /// </summary>
        public void ReleaseMadeClips()
        {
            if (made == null)
            {
                return;
            }

            foreach (AudioClip clip in made)
            {
                if (clip == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(clip);
                }
                else
                {
                    DestroyImmediate(clip);
                }
            }

            made = null;
            refused = null;
        }
    }
}
