// SPDX-License-Identifier: GPL-3.0-or-later
//
// Many sounds to .wav files in one go: the work behind Window > SFX Forge Bake.
//   SfxBakeReport  what a bake did, file by file
//   SfxBakeTo      where the files go
//   SfxBatchBake   finds the presets and variation sets of a selection, names their files, bakes them
// Read SfxBatchBake.Run first: everything else serves it.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CatSplat.SfxForge.Editor
{
    /// <summary>
    /// What a bake did, file by file: for the window's list, a variation set's footer and the Console.
    /// </summary>
    public sealed class SfxBakeReport
    {
        /// <summary>One file that was written, or one thing that could not be.</summary>
        public sealed class Line
        {
            /// <summary>
            /// The asset it was made from (its path), the file written (null: none), and what there is to say of it
            /// (null: nothing).
            /// </summary>
            public string source;
            public string file;
            public string note;
            public float seconds;
            public float peakDb;
            public float rmsDb;
            public float gainDb;

            /// <summary>
            /// clips: over full scale, the file is clamped. cut: it still rang at 12 s. failed: no file.
            /// </summary>
            public bool clips;
            public bool cut;
            public bool failed;

            /// <summary>A line that should be looked at: it failed, or it has a note.</summary>
            public bool Warns => failed || !string.IsNullOrEmpty(note);

            /// <summary>
            /// "Laser.wav 0.42s peak -3.1dB rms -14.8dB", with its note behind it - or, failed, what went wrong.
            /// </summary>
            public string Text
            {
                get
                {
                    if (failed)
                    {
                        return note ?? "failed";
                    }

                    string text =
                        Path.GetFileName(file)
                        + "  "
                        + seconds.ToString("0.00", CultureInfo.InvariantCulture)
                        + "s  peak "
                        + SfxUnits.DbText(peakDb)
                        + "  rms "
                        + SfxUnits.DbText(rmsDb);
                    return string.IsNullOrEmpty(note) ? text : text + "  " + note;
                }
            }
        }

        public readonly List<Line> lines = new List<Line>();

        /// <summary>
        /// What there is to say that is not about one file: clips left in another folder, nothing written
        /// after a failure.
        /// </summary>
        public readonly List<string> remarks = new List<string>();

        public void Remark(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                remarks.Add(text);
            }
        }

        /// <summary>The run was stopped before its last file.</summary>
        public bool Cancelled;

        /// <summary>How many files the run set out to write, and where they went, in words.</summary>
        public int Planned;
        public string Where = "";

        /// <summary>A file could not be written (the Console gets an error, not a warning).</summary>
        public bool WriteFailed;

        public int Written
        {
            get
            {
                int n = 0;
                foreach (Line l in lines)
                {
                    if (!l.failed && l.file != null)
                    {
                        n++;
                    }
                }

                return n;
            }
        }

        public int Failed
        {
            get
            {
                int n = 0;
                foreach (Line l in lines)
                {
                    if (l.failed)
                    {
                        n++;
                    }
                }

                return n;
            }
        }

        public bool Warns
        {
            get
            {
                if (remarks.Count > 0)
                {
                    return true;
                }

                foreach (Line l in lines)
                {
                    if (l.Warns)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>The file written last, null if none was.</summary>
        public string LastFile
        {
            get
            {
                for (int i = lines.Count - 1; i >= 0; i--)
                {
                    if (!lines[i].failed && lines[i].file != null)
                    {
                        return lines[i].file;
                    }
                }

                return null;
            }
        }

        /// <summary>"14 files written, 1 failed - Assets/Audio/Baked", or "cancelled after 5 of 14 files".</summary>
        public string Summary
        {
            get
            {
                int written = Written;
                int failed = Failed;
                if (Cancelled)
                {
                    return "cancelled after " + written + " of " + Planned + " files";
                }

                string text = written + (written == 1 ? " file written" : " files written");
                if (failed > 0)
                {
                    text += ", " + failed + " failed";
                }

                return string.IsNullOrEmpty(Where) ? text : text + " – " + Where;
            }
        }

        /// <summary>The summary and every line, for the Console.</summary>
        public string Text
        {
            get
            {
                var text = new StringBuilder("SFX Forge bake: " + Summary);
                foreach (Line l in lines)
                {
                    text.Append('\n').Append(l.Text);
                }

                foreach (string remark in remarks)
                {
                    text.Append('\n').Append(remark);
                }

                return text.ToString();
            }
        }

        public Line Fail(string source, string note, string file = null)
        {
            var line = new Line
            {
                source = source,
                file = file,
                note = note,
                failed = true,
                peakDb = SfxLevels.Floor,
                rmsDb = SfxLevels.Floor,
            };
            lines.Add(line);
            return line;
        }

        /// <summary>
        /// The whole report as one Console message: a warning when a line failed or has a note, an error when a file
        /// could not be written.
        /// </summary>
        public void ToConsole(Object context = null)
        {
            if (WriteFailed)
            {
                Debug.LogError(Text, context);
            }
            else if (Warns || Cancelled)
            {
                Debug.LogWarning(Text, context);
            }
            else
            {
                Debug.Log(Text, context);
            }
        }

        /// <summary>
        /// A baked sound's line: its file, its length and levels, and the notes it earns (cut at 12 s, clips, silent).
        /// </summary>
        public Line Add(string source, string file, SfxBaked baked)
        {
            var line = new Line
            {
                source = source,
                file = file,
                seconds = baked.Seconds,
                peakDb = baked.level.PeakDb,
                rmsDb = baked.level.RmsDb,
                gainDb = baked.gainDb,
                clips = baked.level.Clips,
                cut = baked.cutAtLimit,
            };
            var notes = new List<string>();
            if (baked.cutAtLimit)
            {
                notes.Add("cut at 12 s (the longest a bake renders)");
            }

            if (baked.level.Clips)
            {
                notes.Add(
                    "clips: "
                        + SfxUnits.DbText(baked.level.PeakDb)
                        + " over full scale, the file is clamped – lower LEVEL or NORMALISE"
                );
            }

            if (baked.level.Silent)
            {
                notes.Add("silent");
            }

            if (notes.Count > 0)
            {
                line.note = string.Join("; ", notes);
            }

            lines.Add(line);
            return line;
        }
    }

    /// <summary>Where a batch bake writes its files.</summary>
    public enum SfxBakeTo
    {
        NextToEachAsset,
        OneFolder,
    }

    /// <summary>
    /// Many sounds to .wav files at once, with options (SfxBakeOptions): the presets and variation sets of a selection,
    /// folders searched with their subfolders. A preset gives "name.wav" (the file BAKE WAV writes), a variation set
    /// "name_01.wav" ... one for each variant. Existing files are overwritten in place (their GUIDs and import settings
    /// stay); nothing is deleted; no asset is changed. The same assets and options write the same bytes. No two assets
    /// of a run write the same file: the later one (in path order) is numbered - "name 2.wav", "name 2_01.wav".
    /// </summary>
    public static class SfxBatchBake
    {
        /// <summary>
        /// The presets and variation sets among these objects and inside these folders: sorted by asset
        /// path, each once.
        /// </summary>
        public static List<Object> Collect(Object[] selection)
        {
            var found = new Dictionary<Object, string>();
            if (selection != null)
            {
                foreach (Object o in selection)
                {
                    if (o == null)
                    {
                        continue;
                    }

                    string path = AssetDatabase.GetAssetPath(o);
                    if (o is SfxPreset || o is SfxVariationSet)
                    {
                        found[o] = path ?? "";
                        continue;
                    }

                    if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path))
                    {
                        continue;
                    }

                    foreach (string kind in new[] { "t:SfxPreset", "t:SfxVariationSet" })
                    {
                        foreach (string guid in AssetDatabase.FindAssets(kind, new[] { path }))
                        {
                            string at = AssetDatabase.GUIDToAssetPath(guid);
                            Object asset = AssetDatabase.LoadAssetAtPath<SfxPreset>(at);
                            if (asset == null)
                            {
                                asset = AssetDatabase.LoadAssetAtPath<SfxVariationSet>(at);
                            }

                            if (asset != null)
                            {
                                found[asset] = at;
                            }
                        }
                    }
                }
            }

            var list = new List<Object>(found.Keys);
            list.Sort(
                (a, b) =>
                {
                    int byPath = string.CompareOrdinal(found[a], found[b]);
                    return byPath != 0 ? byPath : string.CompareOrdinal(a.name, b.name);
                }
            );
            return list;
        }

        /// <summary>
        /// How many files a run over these assets writes: one for a preset, one for each variant of a set
        /// that has a preset.
        /// </summary>
        public static int FileCount(IList<Object> assets, out int presets, out int sets)
        {
            presets = 0;
            sets = 0;
            int files = 0;
            if (assets == null)
            {
                return 0;
            }

            foreach (Object o in assets)
            {
                if (o is SfxPreset)
                {
                    presets++;
                    files++;
                }
                else if (o is SfxVariationSet set)
                {
                    sets++;
                    if (set.preset != null)
                    {
                        files += set.Count;
                    }
                }
            }

            return files;
        }

        static bool InAssets(string path) =>
            !string.IsNullOrEmpty(path) && (path == "Assets" || path.StartsWith("Assets/"));

        struct Job
        {
            public string source;
            public string file;
            public SfxPatch patch;
            public float hold;
        }

        /// <summary>
        /// Bakes the assets with the options. to: next to each asset, or all into one folder (inside Assets; it is made
        /// if it is not there). An asset whose files another asset of this run already writes - the same name in one
        /// folder, or a preset "Step_02" beside a set "Step" - gets "name 2" (then "name 3" ...). progress(file, 0..1)
        /// is called before each file - true stops the run there. The report says what was written and what was not;
        /// nothing is thrown.
        /// </summary>
        public static SfxBakeReport Run(
            IList<Object> assets,
            SfxBakeOptions options,
            SfxBakeTo to,
            string folder = null,
            Func<string, float, bool> progress = null
        )
        {
            // In this order:
            //   1  what stands against the whole run: no options or options that cannot be, no native library, a
            //      folder that is not inside Assets
            //   2  the jobs: for every asset the files it will write (a sound one, a set one a variant), under its
            //      name - or "name 2", "name 3" when another asset of this run writes a file of that name
            //   3  job by job: progress is asked, the sound is rendered, the file is written; what fails is noted
            //      in the report and the run goes on
            //   4  Unity imports what was written
            var report = new SfxBakeReport();
            if (options == null)
            {
                options = new SfxBakeOptions();
            }

            string problem = options.Problem();
            if (problem != null)
            {
                report.Fail(null, "options: " + problem);
                return report;
            }

            if (!SfxUI.NativeThere)
            {
                report.Fail(
                    null,
                    "the native library is not here (sfxforge is for Windows x64 only): nothing can be baked"
                );
                return report;
            }

            if (to == SfxBakeTo.OneFolder)
            {
                folder = (folder ?? "").Replace('\\', '/').Trim().TrimEnd('/');
                if (!InAssets(folder) || folder.Contains(".."))
                {
                    report.Fail(null, "'" + folder + "' is not inside Assets");
                    return report;
                }

                report.Where = folder;
            }
            else
            {
                report.Where = "next to each asset";
            }

            // what is to be written
            var jobs = new List<Job>();

            // every file this run writes: none twice
            var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (assets != null)
            {
                foreach (Object asset in assets)
                {
                    if (asset == null)
                    {
                        continue;
                    }

                    string path = AssetDatabase.GetAssetPath(asset);
                    string source = string.IsNullOrEmpty(path) ? "'" + asset.name + "'" : path;
                    var preset = asset as SfxPreset;
                    var set = asset as SfxVariationSet;
                    if (preset == null && set == null)
                    {
                        continue;
                    }

                    if (set != null && set.preset == null)
                    {
                        report.Fail(source, source + ": no preset");
                        continue;
                    }

                    string dir;
                    if (to == SfxBakeTo.OneFolder)
                    {
                        dir = folder;
                    }
                    else
                    {
                        if (!InAssets(path) || path == "Assets")
                        {
                            report.Fail(source, source + ": not inside Assets – choose ONE FOLDER");
                            continue;
                        }

                        dir = Path.GetDirectoryName(path).Replace('\\', '/');
                    }

                    // its files, under its name - or under "name 2", "name 3" ...
                    // when another asset of this run writes one of them
                    string[] files = null;
                    for (int number = 1; files == null; number++)
                    {
                        string name = number == 1 ? asset.name : asset.name + " " + number;
                        var mine = new string[preset != null ? 1 : set.Count];
                        if (preset != null)
                        {
                            mine[0] = dir + "/" + name + ".wav";
                        }
                        else
                        {
                            for (int i = 0; i < mine.Length; i++)
                            {
                                mine[i] = dir + "/" + name + "_" + (i + 1).ToString("00") + ".wav";
                            }
                        }

                        bool free = true;
                        foreach (string file in mine)
                        {
                            free &= !taken.Contains(file);
                        }

                        if (free)
                        {
                            files = mine;
                        }
                    }

                    for (int i = 0; i < files.Length; i++)
                    {
                        taken.Add(files[i]);
                        jobs.Add(
                            new Job
                            {
                                source = source,
                                file = files[i],
                                patch = preset != null ? preset.patch : set.VariantPatch(i),
                                hold = preset != null ? preset.holdSeconds : set.preset.holdSeconds,
                            }
                        );
                    }
                }
            }

            report.Planned = jobs.Count;
            if (jobs.Count == 0)
            {
                return report;
            }

            var written = new List<string>();
            AssetDatabase.StartAssetEditing(); // (Unity takes the files in when all are written)
            try
            {
                if (to == SfxBakeTo.OneFolder)
                {
                    Directory.CreateDirectory(folder);
                }

                for (int i = 0; i < jobs.Count; i++)
                {
                    Job job = jobs[i];
                    if (progress != null && progress(job.file, i / (float)jobs.Count))
                    {
                        report.Cancelled = true;
                        break;
                    }

                    SfxBaked baked = SfxBake.Render(job.patch, job.hold, options);
                    if (baked == null)
                    {
                        report.Fail(job.source, job.source + ": the engine did not render this sound");
                        continue;
                    }

                    try
                    {
                        SfxWav.Write(job.file, baked.samples, baked.sampleRate, baked.channels);
                    }
                    catch (Exception e)
                    {
                        report.Fail(job.source, "could not write: " + e.Message, job.file);
                        report.WriteFailed = true;
                        continue;
                    }

                    report.Add(job.source, job.file, baked);
                    written.Add(job.file);
                }
            }
            catch (Exception e)
            {
                report.Fail(null, "could not write: " + e.Message);
                report.WriteFailed = true;
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            // new files are imported, rewritten ones again
            if (written.Count > 0)
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }

            return report;
        }

        /// <summary>
        /// The own BAKE of every variation set among the assets (SfxVariationBaker.Bake): their clips and
        /// stamps. No options.
        /// </summary>
        public static SfxBakeReport BakeSets(IList<Object> assets)
        {
            var all = new SfxBakeReport { Where = "each set's own clips" };
            if (assets == null)
            {
                return all;
            }

            foreach (Object asset in assets)
            {
                if (!(asset is SfxVariationSet set))
                {
                    continue;
                }

                SfxBakeReport one = SfxVariationBaker.Bake(set);
                all.lines.AddRange(one.lines);
                all.remarks.AddRange(one.remarks);
                all.Planned += one.Planned;
                all.WriteFailed |= one.WriteFailed;
            }

            return all;
        }
    }
}
