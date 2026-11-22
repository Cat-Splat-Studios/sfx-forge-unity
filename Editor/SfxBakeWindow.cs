// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace CatSplat.SfxForge.Editor
{
    /// <summary>
    /// Window > SFX Forge Bake (also Assets > SFX Forge > Bake to WAV...): the selected presets, variation sets and
    /// folders to .wav files, with options. It looks as the device's display does (SfxBakeView); this class is the
    /// AssetDatabase's side of it: it fills the view's model from the selection and does what the view asks for
    /// (SfxBatchBake). The options are remembered (EditorPrefs).
    /// </summary>
    public class SfxBakeWindow : EditorWindow
    {
        const string OptionsPref = "SfxForge.Bake.Options";
        const string ToPref = "SfxForge.Bake.To";
        const string FolderPref = "SfxForge.Bake.Folder";

        /// <summary>From this many files on a bake shows its progress, and can be cancelled.</summary>
        public const int ProgressFrom = 9;

        // the options as they are kept: JsonUtility has no word for "not
        // a number", so NORMALISE's OFF is a flag of its own
        [Serializable]
        sealed class Kept
        {
            public int sampleRate = 48000;
            public int midiNote;
            public bool mono;
            public bool normalise;
            public float semitones;
            public float velocity = 1f;
            public float trimDb;
            public float normaliseDb;
        }

        readonly SfxBakeModel model = new SfxBakeModel();
        SfxBakeView view;
        List<Object> assets = new List<Object>();

        [MenuItem("Window/SFX Forge Bake")]
        [MenuItem("Assets/SFX Forge/Bake to WAV...")]
        static void ShowWindow() => GetWindow<SfxBakeWindow>("SFX Forge Bake").minSize = new Vector2(340, 420);

        /// <summary>
        /// The whole window as one element: what CreateGUI shows, and what a test or a picture can
        /// build from any model.
        /// </summary>
        public static VisualElement Build(SfxBakeModel model) => new SfxBakeView(model);

        /// <summary>
        /// The options as the text that is kept, and back. A text that is none, or holds options out of
        /// range: the defaults.
        /// </summary>
        public static string ToJson(SfxBakeOptions o)
        {
            bool normalise = !float.IsNaN(o.normaliseDb);
            return JsonUtility.ToJson(
                new Kept
                {
                    sampleRate = o.sampleRate,
                    midiNote = o.midiNote,
                    mono = o.mono,
                    normalise = normalise,
                    semitones = o.semitones,
                    velocity = o.velocity,
                    trimDb = o.trimDb,
                    normaliseDb = normalise ? o.normaliseDb : 0f,
                }
            );
        }

        public static SfxBakeOptions FromJson(string json)
        {
            var options = new SfxBakeOptions();
            if (string.IsNullOrEmpty(json))
            {
                return options;
            }

            try
            {
                var kept = JsonUtility.FromJson<Kept>(json);
                if (kept == null)
                {
                    return options;
                }

                var read = new SfxBakeOptions
                {
                    sampleRate = kept.sampleRate,
                    midiNote = kept.midiNote,
                    mono = kept.mono,
                    semitones = kept.semitones,
                    velocity = kept.velocity,
                    trimDb = kept.trimDb,
                    normaliseDb = kept.normalise ? Mathf.Max(kept.normaliseDb, -24f) : float.NaN,
                };
                return read.Problem() == null ? read : options;
            }
            catch (Exception)
            {
                return options;
            }
        }

        void OnEnable()
        {
            model.options = FromJson(EditorPrefs.GetString(OptionsPref, ""));
            model.to = EditorPrefs.GetInt(ToPref, 0) == 1 ? SfxBakeTo.OneFolder : SfxBakeTo.NextToEachAsset;
            model.folder = EditorPrefs.GetString(FolderPref, SfxBakeModel.DefaultFolder);
            model.optionsChanged = () => EditorPrefs.SetString(OptionsPref, ToJson(model.options));
            model.setTo = to =>
            {
                EditorPrefs.SetInt(ToPref, (int)(model.to = to));
                ShowModel();
            };
            model.setFolder = folder =>
            {
                EditorPrefs.SetString(FolderPref, model.folder = folder ?? "");
                ShowModel();
            };
            model.bake = Bake;
            model.bakeSets = BakeSets;
            model.show = () =>
            {
                var file =
                    model.report != null && model.report.LastFile != null
                        ? AssetDatabase.LoadAssetAtPath<Object>(model.report.LastFile)
                        : null;
                if (file != null)
                {
                    EditorGUIUtility.PingObject(file);
                }
            };
            Selection.selectionChanged += OnSelection;
            Read();
        }

        void OnDisable() => Selection.selectionChanged -= OnSelection;

        void CreateGUI()
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.contentContainer.style.paddingLeft = 6;
            scroll.contentContainer.style.paddingRight = 6;
            view = (SfxBakeView)Build(model);
            scroll.Add(view);
            rootVisualElement.Add(scroll);
        }

        void OnSelection()
        {
            // (another selection: the last bake's lines are not about it)
            model.report = null;
            Read();
            ShowModel();
        }

        // what the selection holds now
        void Read()
        {
            assets = SfxBatchBake.Collect(Selection.objects);
            model.files = SfxBatchBake.FileCount(assets, out model.presets, out model.sets);
            model.nativeThere = SfxUI.NativeThere;
        }

        void ShowModel()
        {
            if (view != null)
            {
                view.Refresh();
            }
        }

        void Bake()
        {
            Read();
            bool bar = model.files >= ProgressFrom;
            SfxBakeReport report;
            try
            {
                report = SfxBatchBake.Run(
                    assets,
                    model.options,
                    model.to,
                    model.folder,
                    bar
                        ? (file, done) => EditorUtility.DisplayCancelableProgressBar("SFX Forge Bake", file, done)
                        : (Func<string, float, bool>)null
                );
            }
            finally
            {
                if (bar)
                {
                    EditorUtility.ClearProgressBar();
                }
            }

            report.ToConsole();
            model.report = report;
            ShowModel();
        }

        void BakeSets()
        {
            Read();
            SfxBakeReport report = SfxBatchBake.BakeSets(assets);
            report.ToConsole();
            model.report = report;
            Read();
            ShowModel();
        }
    }
}
