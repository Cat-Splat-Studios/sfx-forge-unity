// SPDX-License-Identifier: MIT
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CatSplat.SfxForge.Editor
{
    /// <summary>
    /// Keeps the link alive in the editor: polls it, closes the MIDI ports before scripts reload (the system would call
    /// into code that is gone) and opens them again afterwards. Also the editor's small helpers: preview, assets, WAV.
    /// </summary>
    [InitializeOnLoad]
    public static class SfxForgeEditorLink
    {
        const string ConnectedPref = "SfxForge.Connected";
        static AudioSource preview;

        static SfxForgeEditorLink()
        {
            EditorApplication.update += SfxLink.Shared.Poll;
            AssemblyReloadEvents.beforeAssemblyReload += SfxLink.Shared.Close;
            EditorApplication.quitting += SfxLink.Shared.Close;
            if (EditorPrefs.GetBool(ConnectedPref, false)) EditorApplication.delayCall += () => Connect(false);
        }

        public static string InputPort { get => PlayerPrefs.GetString(SfxForgeLink.InputPref, ""); set => PlayerPrefs.SetString(SfxForgeLink.InputPref, value ?? ""); }
        public static string OutputPort { get => PlayerPrefs.GetString(SfxForgeLink.OutputPref, ""); set => PlayerPrefs.SetString(SfxForgeLink.OutputPref, value ?? ""); }

        public static bool Connect(bool complain = true)
        {
            bool ok = SfxLink.Shared.Open(InputPort, OutputPort);
            if (!ok && complain) Debug.LogWarning($"SFX Forge: could not open every MIDI port (in '{InputPort}', out '{OutputPort}'). Is another program holding it?");
            EditorPrefs.SetBool(ConnectedPref, SfxLink.Shared.IsOpen);
            return ok;
        }

        public static void Disconnect()
        {
            SfxLink.Shared.Close();
            EditorPrefs.SetBool(ConnectedPref, false);
        }

        /// <summary>Plays a patch through the computer's speakers, with the device's engine.</summary>
        public static void Preview(SfxPatch patch, float holdSeconds = 0.8f)
        {
            float[] samples = SfxNative.BakeInterleaved(patch, 48000, 1f, 0, 0f, holdSeconds);
            if (samples == null || samples.Length == 0) { Debug.LogWarning("SFX Forge: the native library (sfxforge) is not available on this platform."); return; }
            PreviewSamples(samples, 48000);
        }

        /// <summary>Plays stereo samples (L R L R …) through the computer's speakers, in place of what the preview played.</summary>
        public static void PreviewSamples(float[] interleaved, int sampleRate)
        {
            if (interleaved == null || interleaved.Length < 2) return;
            if (preview == null)
            {
                var go = EditorUtility.CreateGameObjectWithHideFlags("SFX Forge Preview", HideFlags.HideAndDontSave, typeof(AudioSource));
                preview = go.GetComponent<AudioSource>();
            }
            var clip = AudioClip.Create("SFX Forge Preview", interleaved.Length / 2, 2, sampleRate, false);
            clip.hideFlags = HideFlags.HideAndDontSave;
            clip.SetData(interleaved, 0);
            preview.Stop();
            if (preview.clip != null) Object.DestroyImmediate(preview.clip);
            preview.clip = clip;
            preview.Play();
        }

        /// <summary>Silence: what the preview plays stops.</summary>
        public static void StopPreview()
        {
            if (preview != null) preview.Stop();
        }

        public static SfxPreset CreatePreset(SfxPatch patch, string folder, string name)
        {
            if (string.IsNullOrEmpty(folder) || !folder.StartsWith("Assets")) folder = "Assets/SfxForge";
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            var asset = ScriptableObject.CreateInstance<SfxPreset>();
            asset.patch = patch;
            AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath($"{folder}/{name}.asset"));
            AssetDatabase.SaveAssets();
            return asset;
        }

        public static SfxSong CreateSong(SfxPatch[] sounds, byte[] packedSong, string folder, string name)
        {
            if (string.IsNullOrEmpty(folder) || !folder.StartsWith("Assets")) folder = "Assets/SfxForge";
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            var asset = ScriptableObject.CreateInstance<SfxSong>();
            asset.Set(sounds, packedSong);
            AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath($"{folder}/{name}.asset"));
            AssetDatabase.SaveAssets();
            return asset;
        }

        /// <summary>The preset as a 16-bit WAV next to its asset; returns the imported clip.</summary>
        public static AudioClip BakeWav(SfxPreset preset)
        {
            float[] samples = SfxNative.BakeInterleaved(preset.patch, 48000, 1f, 0, 0f, preset.holdSeconds);
            if (samples == null || samples.Length == 0) { Debug.LogWarning("SFX Forge: nothing to bake (the native library is missing on this platform)."); return null; }
            string asset = AssetDatabase.GetAssetPath(preset);
            string path = string.IsNullOrEmpty(asset) ? $"Assets/{preset.name}.wav" : Path.ChangeExtension(asset, ".wav");
            SfxWav.Write(path, samples, 48000, 2);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}
