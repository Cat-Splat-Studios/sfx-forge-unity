// SPDX-License-Identifier: GPL-3.0-or-later
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
            // The editor polls the link itself, in Play mode too. A build has no editor: there an SfxForgeLink
            // component in the scene does it - which is why a link can work in the editor and not in a build.
            EditorApplication.update += SfxLink.Shared.Poll;
            AssemblyReloadEvents.beforeAssemblyReload += SfxLink.Shared.Close;
            EditorApplication.quitting += SfxLink.Shared.Close;
            if (EditorPrefs.GetBool(ConnectedPref, false))
            {
                EditorApplication.delayCall += () => Connect(false);
            }
        }

        public static string InputPort
        {
            get => PlayerPrefs.GetString(SfxForgeLink.InputPref, "");
            set => PlayerPrefs.SetString(SfxForgeLink.InputPref, value ?? "");
        }

        public static string OutputPort
        {
            get => PlayerPrefs.GetString(SfxForgeLink.OutputPref, "");
            set => PlayerPrefs.SetString(SfxForgeLink.OutputPref, value ?? "");
        }

        public static bool Connect(bool complain = true)
        {
            bool ok = SfxLink.Shared.Open(InputPort, OutputPort);
            if (!ok && complain)
            {
                Debug.LogWarning(
                    $"SFX Forge: could not open every MIDI port (in '{InputPort}', out '{OutputPort}'). "
                        + "Is another program holding it?"
                );
            }

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
            if (samples == null || samples.Length == 0)
            {
                Debug.LogWarning("SFX Forge: the native library (sfxforge) is not available on this platform.");
                return;
            }

            PreviewSamples(samples, 48000);
        }

        /// <summary>
        /// Plays stereo samples (L R L R …) through the computer's speakers, in place of what the preview played.
        /// pitch and volume are the preview AudioSource's: 1 and 1 play the samples as they are.
        /// </summary>
        public static void PreviewSamples(float[] interleaved, int sampleRate, float pitch = 1f, float volume = 1f) =>
            PreviewSamples(interleaved, sampleRate, 2, pitch, volume);

        /// <summary>As PreviewSamples, for samples of one channel or two.</summary>
        public static void PreviewSamples(float[] samples, int sampleRate, int channels, float pitch, float volume)
        {
            if (channels < 1 || samples == null || samples.Length < channels)
            {
                return;
            }

            if (preview == null)
            {
                var go = EditorUtility.CreateGameObjectWithHideFlags(
                    "SFX Forge Preview",
                    HideFlags.HideAndDontSave,
                    typeof(AudioSource)
                );
                preview = go.GetComponent<AudioSource>();
            }

            var clip = AudioClip.Create("SFX Forge Preview", samples.Length / channels, channels, sampleRate, false);
            clip.hideFlags = HideFlags.HideAndDontSave;
            clip.SetData(samples, 0);
            preview.Stop();
            if (preview.clip != null)
            {
                Object.DestroyImmediate(preview.clip);
            }

            preview.clip = clip;
            preview.pitch = pitch;
            preview.volume = Mathf.Clamp01(volume);
            preview.Play();
        }

        /// <summary>Silence: what the preview plays stops.</summary>
        public static void StopPreview()
        {
            if (preview != null)
            {
                preview.Stop();
            }
        }

        public static SfxPreset CreatePreset(SfxPatch patch, string folder, string name)
        {
            if (string.IsNullOrEmpty(folder) || !folder.StartsWith("Assets"))
            {
                folder = "Assets/SfxForge";
            }

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
            if (string.IsNullOrEmpty(folder) || !folder.StartsWith("Assets"))
            {
                folder = "Assets/SfxForge";
            }

            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            var asset = ScriptableObject.CreateInstance<SfxSong>();
            asset.Set(sounds, packedSong);
            AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath($"{folder}/{name}.asset"));
            AssetDatabase.SaveAssets();
            return asset;
        }

        /// <summary>
        /// The preset as a 16-bit WAV next to its asset (48 kHz, stereo, as designed); returns the imported clip.
        /// </summary>
        public static AudioClip BakeWav(SfxPreset preset) => BakeWav(preset, null);

        /// <summary>
        /// The preset as a 16-bit WAV, rendered with these options (null: the defaults). path null: next to its asset,
        /// under its name. Returns the imported clip; null where the native library is missing, the options are out of
        /// range or the engine gave no sound - the Console says which.
        /// </summary>
        public static AudioClip BakeWav(SfxPreset preset, SfxBakeOptions options, string path = null)
        {
            if (preset == null)
            {
                return null;
            }

            if (options == null)
            {
                options = new SfxBakeOptions();
            }

            string problem = options.Problem();
            if (problem != null)
            {
                Debug.LogWarning("SFX Forge: nothing baked - " + problem + ".");
                return null;
            }

            SfxBaked baked = SfxBake.Render(preset.patch, preset.holdSeconds, options);
            if (baked == null || baked.samples.Length == 0)
            {
                Debug.LogWarning(
                    SfxNative.Available
                        ? "SFX Forge: nothing baked - the engine did not render '" + preset.name + "'."
                        : "SFX Forge: nothing to bake (the native library is missing on this platform).",
                    preset
                );
                return null;
            }

            if (string.IsNullOrEmpty(path))
            {
                string asset = AssetDatabase.GetAssetPath(preset);
                path = string.IsNullOrEmpty(asset) ? $"Assets/{preset.name}.wav" : Path.ChangeExtension(asset, ".wav");
            }

            SfxWav.Write(path, baked.samples, baked.sampleRate, baked.channels);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        /// <summary>
        /// The patch as a .syx file: the load message for a pad (SfxPatchText.ToSyx). Any path, also
        /// outside the project.
        /// </summary>
        public static void ExportSyx(SfxPatch patch, int pad, string path) =>
            File.WriteAllBytes(path, SfxPatchText.ToSyx(patch, pad));

        /// <summary>Reads a .syx file (SfxPatchText.ParseSyx). A file that cannot be read: NoPatchInFile.</summary>
        public static SfxTextResult ImportSyx(string path, out SfxPatch patch, out int pad)
        {
            patch = default;
            pad = 0;
            byte[] file;
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    return SfxTextResult.NoPatchInFile;
                }

                if (new FileInfo(path).Length > SfxPatchText.MaxFile)
                {
                    return SfxTextResult.TooLarge;
                }

                file = File.ReadAllBytes(path);
            }
            catch (System.Exception)
            {
                return SfxTextResult.NoPatchInFile;
            }

            return SfxPatchText.ParseSyx(file, out patch, out pad);
        }
    }
}
