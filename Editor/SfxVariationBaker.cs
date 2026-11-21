// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CatSplat.SfxForge.Editor
{
    /// <summary>
    /// BAKE of a variation set: its variants as .wav files in a folder next to the asset ("NAME Variants"), and the
    /// set pointing at them - so it plays on every platform, through Unity's own importer (compression and load type
    /// are the clips' import settings).
    /// </summary>
    public static class SfxVariationBaker
    {
        /// <summary>What the set's own BAKE could not do, in words (also what its Inspector says).</summary>
        public const string NoPreset = "no preset: choose the sound this set varies";
        public const string NoLibrary = "the native library is not here (sfxforge is for Windows x64 only): no BAKE here";
        public static string NotInAssets(string name) => "SFX Forge: '" + name + "' is not inside Assets – its clips are written next to it. Save or move it there first.";

        /// <summary>Where a set's clips go: "(asset folder)/(asset name) Variants". Null: the set is no asset inside Assets.</summary>
        public static string FolderOf(SfxVariationSet set)
        {
            if (set == null) return null;
            string path = AssetDatabase.GetAssetPath(set);
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/")) return null;
            return Path.GetDirectoryName(path).Replace('\\', '/') + "/" + Path.GetFileNameWithoutExtension(path) + " Variants";
        }

        static string FileOf(string folder, string name, int index) => folder + "/" + name + "_" + (index + 1).ToString("00") + ".wav";

        /// <summary>
        /// Writes "(folder)/(name)_01.wav" ... (16 bit, 48 kHz, velocity 1, stereo or mono as the set says), and sets the
        /// set's clips and stamp. All or nothing: every variant is rendered first, and if one does not render or one of
        /// the files cannot be written (it is read-only, say) nothing is written and the set stays as it was. A file
        /// that is there is overwritten in place: its GUID, its import settings and what refers to it stay. Clips the
        /// set had before, in its own folder, with a number above the new count are deleted; no other file ever is - a
        /// set that was renamed or copied leaves the clips it had in their old folder, and the report says so (remarks).
        /// Not an undo step; and what was done to the set before can no longer be undone (Undo.ClearUndo: an undo would
        /// put back the set as it was before the bake, without its clips). The report says what happened.
        /// </summary>
        public static SfxBakeReport Bake(SfxVariationSet set)
        {
            var report = new SfxBakeReport();
            if (set == null) return report;
            string path = AssetDatabase.GetAssetPath(set);
            string source = string.IsNullOrEmpty(path) ? "'" + set.name + "'" : path;
            if (set.preset == null) { report.Fail(source, source + ": " + NoPreset); return report; }
            string folder = FolderOf(set);
            if (folder == null) { report.Fail(source, NotInAssets(set.name)); return report; }
            if (!SfxUI.NativeThere) { report.Fail(source, source + ": " + NoLibrary); return report; }

            string name = Path.GetFileNameWithoutExtension(path);
            int count = set.Count;
            report.Planned = count;
            report.Where = folder;
            int stamp = set.Stamp;

            var so = new SerializedObject(set);
            SerializedProperty clipsProp = so.FindProperty("clips");
            var before = new List<string>();                        // the clips it has now
            for (int i = 0; i < clipsProp.arraySize; i++)
            {
                UnityEngine.Object was = clipsProp.GetArrayElementAtIndex(i).objectReferenceValue;
                if (was != null) before.Add(AssetDatabase.GetAssetPath(was));
            }

            var files = new List<string>();
            bool all = true;
            try { if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder)); }
            catch (Exception e) { report.Fail(source, "SFX Forge: could not write into '" + folder + "': " + e.Message); report.WriteFailed = true; return report; }
            // every variant first, then every file: nothing is written unless all rendered and all files can be written (a failure while writing leaves the files already written)
            var options = new SfxBakeOptions { mono = set.mono };
            var rendered = new SfxBaked[count];
            for (int i = 0; i < count; i++)
            {
                rendered[i] = SfxBake.Render(set.VariantPatch(i), set.preset.holdSeconds, options);
                if (rendered[i] == null) { report.Fail(source, Path.GetFileName(FileOf(folder, name, i)) + ": the engine did not render this sound"); all = false; }
            }
            for (int i = 0; all && i < count; i++)
            {
                string file = FileOf(folder, name, i);
                string why = CannotWrite(file);
                if (why != null) { report.Fail(source, "SFX Forge: could not write '" + file + "': " + why, file); report.WriteFailed = true; all = false; }
            }
            if (!all) { report.Remark("nothing was written: the set and its clips are as they were"); return report; }

            AssetDatabase.StartAssetEditing();                      // (Unity takes the files in when all are written)
            try
            {
                for (int i = 0; i < count; i++)
                {
                    string file = FileOf(folder, name, i);
                    SfxBaked baked = rendered[i];
                    try { SfxWav.Write(file, baked.samples, baked.sampleRate, baked.channels); }
                    catch (Exception e) { report.Fail(source, "SFX Forge: could not write '" + file + "': " + e.Message, file); report.WriteFailed = true; all = false; continue; }
                    SfxBakeReport.Line line = report.Add(source, file, baked);
                    if (line.note != null) line.note = Path.GetFileName(file) + ": " + line.note;
                    files.Add(file);
                }
            }
            catch (Exception e) { report.Fail(source, "SFX Forge: could not write into '" + folder + "': " + e.Message); report.WriteFailed = true; all = false; }
            finally { AssetDatabase.StopAssetEditing(); }
            if (files.Count > 0) AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);      // new files are imported, rewritten ones again
            if (!all) return report;

            var clips = new AudioClip[count];
            for (int i = 0; i < count; i++)
            {
                clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(FileOf(folder, name, i));
                if (clips[i] == null) { report.Fail(source, "SFX Forge: '" + FileOf(folder, name, i) + "' was written but Unity did not import it as a clip"); return report; }
            }
            so.Update();
            clipsProp = so.FindProperty("clips");
            clipsProp.arraySize = count;
            for (int i = 0; i < count; i++) clipsProp.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            so.FindProperty("bakedStamp").intValue = stamp;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssetIfDirty(set);
            Undo.ClearUndo(set);                                    // (an undo of an earlier edit would put the whole set back as it was: without these clips)

            // its own files of before that it no longer has: variants above the new count
            var elsewhere = new List<string>();
            foreach (string was in before)
            {
                if (string.IsNullOrEmpty(was)) continue;
                string wasIn = Path.GetDirectoryName(was).Replace('\\', '/');
                if (wasIn != folder)                                // (the set was renamed, moved or copied: those clips may be another set's - they stay)
                {
                    if (!elsewhere.Contains(wasIn)) elsewhere.Add(wasIn);
                    continue;
                }
                string file = Path.GetFileName(was);
                if (!file.StartsWith(name + "_", StringComparison.Ordinal) || !file.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) continue;
                string digits = file.Substring(name.Length + 1, file.Length - name.Length - 5);
                if (int.TryParse(digits, out int number) && number > count) AssetDatabase.DeleteAsset(was);
            }
            foreach (string old in elsewhere)
                report.Remark("the clips '" + set.name + "' had before stay in '" + old + "': it was renamed, moved or copied since. Delete that folder if nothing else uses it");
            return report;
        }

        // null: the file can be written (or made). Else why not, in words.
        static string CannotWrite(string file)
        {
            try
            {
                if (!File.Exists(file)) return null;
                if ((File.GetAttributes(file) & FileAttributes.ReadOnly) != 0) return "the file is read-only";
                using (File.Open(file, FileMode.Open, FileAccess.Write, FileShare.Read)) { }
                return null;
            }
            catch (Exception e) { return e.Message; }
        }

        /// <summary>The size of the set's baked clips on disk, in bytes (0: none).</summary>
        public static long BakedBytes(SfxVariationSet set)
        {
            long bytes = 0;
            if (set == null || !set.IsBaked) return 0;
            for (int i = 0; i < set.BakedCount; i++)
            {
                string file = AssetDatabase.GetAssetPath(set.Clip(i));
                try { if (!string.IsNullOrEmpty(file) && File.Exists(file)) bytes += new FileInfo(file).Length; }
                catch (Exception) { }
            }
            return bytes;
        }
    }
}
