// SPDX-License-Identifier: MIT
using UnityEngine;

namespace CatSplat.SfxForge
{
    /// <summary>One SFX FORGE sound as an asset: made here, or captured from the device (Window > SFX Forge).</summary>
    [CreateAssetMenu(menuName = "SFX Forge/Preset", fileName = "New SFX", order = 220)]
    public class SfxPreset : ScriptableObject, ISerializationCallbackReceiver
    {
        /// <summary>The asset format this version of SFX Forge writes.</summary>
        public const int CurrentFormat = SfxAssetFormat.Current;

        // Which asset format wrote this file. A file without the line (every asset from before the number was kept) is format 1.
        [SerializeField, HideInInspector] int format = CurrentFormat;

        // Which asset format saved this file last: always this version's number when this version writes the file.
        // A newer SFX Forge that finds its own "format" with a lower number here knows the file went through an older
        // version, which wrote back only what it knew. (Nothing reads it in this version.)
        [SerializeField, HideInInspector] int savedBy = CurrentFormat;
        void ISerializationCallbackReceiver.OnBeforeSerialize() { savedBy = CurrentFormat; }
        void ISerializationCallbackReceiver.OnAfterDeserialize() { }

        public SfxPatch patch = SfxPatch.Default;

        [Tooltip("For a sustained sound: how long a baked clip holds it before letting go.")]
        [Min(0.05f)] public float holdSeconds = 1.5f;

        /// <summary>The asset format the file was written in: 1 today.</summary>
        public int Format => format;
        /// <summary>
        /// A newer SFX Forge made this asset: there may be more in its file than this version reads. What is read
        /// plays; saving the asset here writes only that, and what this version does not know is lost.
        /// </summary>
        public bool IsFromNewerVersion => format > CurrentFormat;

        void OnEnable() => SfxAssetFormat.WarnIfNewer(this, format);

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

    /// <summary>The number SFX Forge's assets keep of the asset format that wrote them, and what is said of a higher one.</summary>
    public static class SfxAssetFormat
    {
        /// <summary>What this version writes. An asset file without the number is this format too.</summary>
        public const int Current = 1;

        static readonly System.Collections.Generic.HashSet<string> said = new System.Collections.Generic.HashSet<string>();

        /// <summary>What an Inspector's banner and the Console say of an asset from a newer version.</summary>
        public static string NewerText(int format)
            => "made by a newer SFX Forge (asset format " + format + ", this version knows " + Current + "): what this version reads of it plays, the rest is left out. "
             + "Saving it here loses what this version does not know. Update the package to use all of it.";

        /// <summary>
        /// One warning for an asset from a newer version, when it is loaded. In the editor once in a session for each
        /// asset file (scripts reload often there, and every reload loads the assets again): two files of the same name
        /// in two folders are two assets. In a player, and for an object that is no file, once for each type and name.
        /// </summary>
        public static void WarnIfNewer(Object asset, int format)
        {
            if (format <= Current || asset == null) return;
            string key = "SfxForge.Newer." + asset.GetType().Name + "." + asset.name;
#if UNITY_EDITOR
            if (UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long local)) key = "SfxForge.Newer." + guid + "." + local;
#endif
            if (!said.Add(key)) return;
#if UNITY_EDITOR
            if (UnityEditor.SessionState.GetBool(key, false)) return;
            UnityEditor.SessionState.SetBool(key, true);
#endif
            Debug.LogWarning("SFX Forge: '" + asset.name + "' was " + NewerText(format), asset);
        }
    }
}
