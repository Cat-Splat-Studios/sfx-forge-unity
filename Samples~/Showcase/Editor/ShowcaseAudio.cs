using System;
using CatSplat.SfxForge;
using SfxShowcase.Audio;
using SfxShowcase.Core;
using UnityEditor;
using UnityEngine;

namespace SfxShowcase.Editor
{
    /// <summary>
    /// The editor's side of the sound, kept in a file of its own so that the seam is as clean here as at runtime:
    /// <see cref="ShowcaseBuilder"/> calls <see cref="Install"/> while it builds the scene and <see cref="Check"/> while
    /// it verifies it, and knows nothing else about SFX FORGE. To take the sound out of the showcase, delete
    /// <c>Scripts/Audio</c>, this file, <c>Tests/ShowcaseAudioTests.cs</c> and those two calls.
    /// </summary>
    public static class ShowcaseAudio
    {
        const string SongFolderName = "Song";
        /// <summary>The showcase's own copy of the song, inside its folder so that the folder can be taken anywhere.</summary>
        public static string SongFolder => ShowcaseBuilder.Root + "/" + SongFolderName;
        /// <summary>The music: the song asset the scene's music player holds. Written from <see cref="SongBytesPath"/> by every rebuild.</summary>
        public static string SongPath => SongFolder + "/DemoSong.asset";
        /// <summary>The song as the device sent it: 16 sounds of <see cref="SfxPatch.Bytes"/> bytes, then the packed song.</summary>
        public static string SongBytesPath => SongFolder + "/DemoSong.bytes";

        const string PresetFolderName = "Presets";
        static string PresetFolder => ShowcaseBuilder.GeneratedFolder + "/" + PresetFolderName;

        /// <summary>The largest sample of a baked clip must be above this for the sound to count as audible.</summary>
        const float AudibleLevel = 0.01f;
        /// <summary>The sample rate the check bakes at, Hz.</summary>
        const int CheckSampleRate = 48000;

        /// <summary>
        /// THE SOUND: the one call that puts SFX FORGE into the game. It hands the audio installer the song and a
        /// factory that saves every patch of the sound library as a preset asset.
        /// </summary>
        public static void Install(PlayerBase player, GameSessionBase session)
        {
            if (!AssetDatabase.IsValidFolder(PresetFolder)) AssetDatabase.CreateFolder(ShowcaseBuilder.GeneratedFolder, PresetFolderName);
            AudioInstaller.Install(player, session, BuildSong(), CreatePresetAsset);
        }

        /// <summary>
        /// The song asset, written from the song file beside it (and created if it is not there, so its GUID and the
        /// scene's reference to it survive a rebuild). The file is a copy of the demo project's
        /// <c>Assets/SfxDemo/DemoSong.bytes</c>; the showcase keeps its own so that it needs nothing outside its folder.
        /// </summary>
        static SfxSong BuildSong()
        {
            if (!ReadSongFile(out SfxPatch[] sounds, out byte[] packed))
                throw new InvalidOperationException(SongBytesPath + " is missing or is not 16 sounds and a packed song");
            SfxSong song = ShowcaseBuilder.LoadOrCreate<SfxSong>(SongPath);
            song.Set(sounds, packed);
            EditorUtility.SetDirty(song);
            return song;
        }

        /// <summary>Reads <see cref="SongBytesPath"/>: 16 patches, then the rest of the file - the packed song. False when the file is not there or is not that.</summary>
        static bool ReadSongFile(out SfxPatch[] sounds, out byte[] packed)
        {
            sounds = new SfxPatch[SfxProtocol.Slots];
            packed = new byte[0];
            var file = AssetDatabase.LoadAssetAtPath<TextAsset>(SongBytesPath);
            byte[] raw = file != null ? file.bytes : null;
            int soundBytes = SfxProtocol.Slots * SfxPatch.Bytes;
            if (raw == null || raw.Length <= soundBytes) return false;
            for (int i = 0; i < sounds.Length; i++)
                if (!SfxPatch.TryUnpack(raw, i * SfxPatch.Bytes, out sounds[i])) return false;
            packed = new byte[raw.Length - soundBytes];              // (a packed song has no fixed size from format 6 on)
            Array.Copy(raw, soundBytes, packed, 0, packed.Length);
            return SfxPackedSong.IsValid(packed);
        }

        /// <summary>Whether a song asset holds exactly these sounds and this packed song.</summary>
        static bool Holds(SfxSong song, SfxPatch[] sounds, byte[] packed)
        {
            if (song == null || !song.IsValid || song.data.Length != packed.Length) return false;
            for (int i = 0; i < packed.Length; i++)
                if (song.data[i] != packed[i]) return false;
            for (int i = 0; i < sounds.Length; i++)
                if (!song.sounds[i].Equals(sounds[i])) return false;
            return true;
        }

        /// <summary>
        /// The <see cref="PresetFactory"/> handed to the audio installer: every sound of the library becomes (or
        /// updates) <c>Generated/Presets/&lt;name&gt;.asset</c>, so each one can be clicked, heard and tuned in the Inspector.
        /// </summary>
        static SfxPreset CreatePresetAsset(string name, SfxPatch patch, float holdSeconds)
        {
            SfxPreset preset = ShowcaseBuilder.LoadOrCreate<SfxPreset>($"{PresetFolder}/{name}.asset");
            preset.patch = patch;
            preset.holdSeconds = holdSeconds;
            EditorUtility.SetDirty(preset);
            return preset;
        }

        /// <summary>The sound of the open scene: the presets, the song, the players the installer made and what each one holds.</summary>
        public static void Check(Action<bool, string> check)
        {
            check(SfxNative.Available, "SFX FORGE's native library loads inside Unity");

            int presets = 0, audible = 0;
            foreach (SoundDefinition definition in SoundLibrary.All)
            {
                SfxPreset preset = PresetOf(definition);
                if (preset == null || !preset.patch.Equals(definition.Patch)) continue;
                presets++;
                // baked as the note the game plays it at – for held sounds too (ContinuousSounds holds them at that note)
                if (IsAudible(preset.Bake(CheckSampleRate, midiNote: definition.MidiNote))) audible++;
            }
            check(presets == SoundLibrary.All.Count, $"{presets} of {SoundLibrary.All.Count} sound effects are preset assets holding the library's patch");
            check(audible == presets || !SfxNative.Available, $"{audible} of them bake to a clip that is not silent");

            var song = AssetDatabase.LoadAssetAtPath<SfxSong>(SongPath);
            var music = UnityEngine.Object.FindFirstObjectByType<SfxMusicPlayer>();
            bool songIsItsFile = ReadSongFile(out SfxPatch[] fileSounds, out byte[] filePacked) && Holds(song, fileSounds, filePacked);
            check(song != null && song.IsValid && songIsItsFile && music != null && music.song == song && music.GetComponent<MusicDirector>() != null,
                  $"the music player holds the showcase's own song ({SongFolderName}/DemoSong.asset, which is what {SongFolderName}/DemoSong.bytes holds) and is driven by the MusicDirector");
            NoteOtherCopies();

            var pool = UnityEngine.Object.FindFirstObjectByType<LiveVoicePool>();
            int voices = pool != null ? pool.GetComponentsInChildren<SfxPlayer>().Length : 0;
            check(voices == AudioInstaller.LiveVoiceCount && Count<SoundEffects>() == 1,
                  $"the effects have their pool of {AudioInstaller.LiveVoiceCount} live voices");

            var continuous = UnityEngine.Object.FindFirstObjectByType<ContinuousSounds>();
            bool heldSoundsWired = continuous != null && Count<ContinuousSounds>() == 1;
            int held = 0;
            foreach (SoundDefinition definition in SoundLibrary.All)
            {
                if (definition.Route != SoundRoute.LiveHeld) continue;
                held++;
                heldSoundsWired = heldSoundsWired && HoldsAtItsNote(continuous, definition);
            }
            check(heldSoundsWired && Count<SfxPlayer>() == AudioInstaller.LiveVoiceCount + held,
                  $"each of the {held} held sounds has an SfxPlayer with its own preset, held at the library's note");

            bool sourcesAreBare = true;
            foreach (AudioSource source in UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                sourcesAreBare &= source.clip == null && !source.playOnAwake && source.spatialBlend == 0f;
            check(sourcesAreBare, "no AudioSource has a clip: every sound is made at runtime");
        }

        /// <summary>
        /// Not a check, a line in the log: any other file named DemoSong.bytes in the project (the demo project has the
        /// original in Assets/SfxDemo), and whether it still holds the same bytes as the showcase's copy. If not, one
        /// of the two was regenerated and the other was not.
        /// </summary>
        static void NoteOtherCopies()
        {
            var mine = AssetDatabase.LoadAssetAtPath<TextAsset>(SongBytesPath);
            if (mine == null) return;
            byte[] own = mine.bytes;
            foreach (string guid in AssetDatabase.FindAssets("DemoSong t:TextAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path == SongBytesPath || !path.EndsWith("/DemoSong.bytes", StringComparison.Ordinal)) continue;
                byte[] other = AssetDatabase.LoadAssetAtPath<TextAsset>(path).bytes;
                bool same = other.Length == own.Length;
                for (int i = 0; same && i < own.Length; i++) same = other[i] == own[i];
                if (same) Debug.Log($"SHOWCASE: note  {path} holds the same song as the showcase's copy");
                else Debug.LogWarning($"SHOWCASE: note  {path} DIFFERS from {SongBytesPath}: copy the newer file over the other and rebuild");
            }
        }

        static SfxPreset PresetOf(SoundDefinition definition)
        {
            return AssetDatabase.LoadAssetAtPath<SfxPreset>($"{PresetFolder}/{definition.Name}.asset");
        }

        /// <summary>
        /// Whether ContinuousSounds holds this sound's preset on one of its SfxPlayers together with its MIDI note.
        /// Read through the serialized fields ("rollHumPlayer" / "rollHumNote" …), the way the Inspector shows them.
        /// </summary>
        static bool HoldsAtItsNote(ContinuousSounds continuous, SoundDefinition definition)
        {
            SfxPreset preset = PresetOf(definition);
            var serialized = new SerializedObject(continuous);
            foreach (string sound in new[] { "rollHum", "wind", "windUp" })
            {
                var player = serialized.FindProperty(sound + "Player").objectReferenceValue as SfxPlayer;
                if (player == null || player.preset != preset) continue;
                return serialized.FindProperty(sound + "Note").intValue == definition.MidiNote;
            }
            return false;
        }

        static bool IsAudible(AudioClip clip)
        {
            if (clip == null) return false;
            var samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);
            UnityEngine.Object.DestroyImmediate(clip);
            foreach (float sample in samples)
                if (Mathf.Abs(sample) > AudibleLevel) return true;
            return false;
        }

        static int Count<T>() where T : UnityEngine.Object
        {
            return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        }
    }
}
