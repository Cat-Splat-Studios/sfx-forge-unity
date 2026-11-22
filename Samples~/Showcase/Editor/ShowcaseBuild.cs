using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SfxShowcase.Editor
{
    /// <summary>
    /// A standalone build of the showcase: one scene, Windows x64, no editor anywhere near it.
    ///
    ///     Unity -batchmode -quit -projectPath unity/Demo -executeMethod SfxShowcase.Editor.ShowcaseBuild.BuildWindows
    ///           [-sfxforge-build C:\somewhere\Brio.exe]      (default: see <see cref="DefaultPath"/>)
    ///
    /// Menu: SFX Forge > Showcase > Build for Windows. The build settings and the project's settings are left as they
    /// are (the scene is named here, the product's name is set for the build and put back). The built game checks
    /// itself when it is started with -sfxforge-probe (see <c>SfxShowcase.Audio.BuildProbe</c>).
    /// </summary>
    public static class ShowcaseBuild
    {
        const string PathArgument = "-sfxforge-build";
        const string ProductName = "Brio First Harmonic";
        const string CompanyName = "Cat Splat Studios";

        /// <summary>
        /// Where the build goes when nothing else is asked for. In the SFX FORGE repository (the project is its
        /// <c>unity/Demo</c>: two folders up there is the firmware's folder and the package's) that is
        /// <c>out/build/Brio/Brio.exe</c> at the repository's root, which git ignores. In any other project - the
        /// showcase imported as the package's sample, or copied - it is <c>Builds/Brio/Brio.exe</c> in the project's
        /// folder, beside <c>Assets</c>.
        /// </summary>
        static string DefaultPath
        {
            get
            {
                string repository = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
                bool isRepository =
                    Directory.Exists(Path.Combine(repository, "sfx-forge"))
                    && File.Exists(Path.Combine(repository, "unity", "com.catsplat.sfxforge", "package.json"));
                return isRepository
                    ? Path.Combine(repository, "out", "build", "Brio", "Brio.exe")
                    : Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Brio", "Brio.exe"));
            }
        }

        [MenuItem("SFX Forge/Showcase/Build for Windows")]
        public static void BuildWindows()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(arguments, PathArgument);
            string path = at >= 0 && at + 1 < arguments.Length ? Path.GetFullPath(arguments[at + 1]) : DefaultPath;

            string productBefore = PlayerSettings.productName;
            string companyBefore = PlayerSettings.companyName;
            bool ok = false;
            try
            {
                PlayerSettings.productName = ProductName;
                PlayerSettings.companyName = CompanyName;
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ShowcaseBuilder.ScenePath },
                    locationPathName = path,
                    target = BuildTarget.StandaloneWindows64,
                    targetGroup = BuildTargetGroup.Standalone,
                    options = BuildOptions.None,
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                BuildSummary summary = report.summary;
                ok = summary.result == BuildResult.Succeeded;
                Debug.Log(
                    ok
                        ? $"SHOWCASE BUILD: ok {summary.outputPath} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime.TotalSeconds:0} s, {summary.totalWarnings} warnings)"
                        : $"SHOWCASE BUILD: FAILED {summary.result}, {summary.totalErrors} errors"
                );
            }
            catch (Exception e)
            {
                Debug.LogError("SHOWCASE BUILD: FAILED " + e);
            }
            finally
            {
                PlayerSettings.productName = productBefore;
                PlayerSettings.companyName = companyBefore;
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(ok ? 0 : 1);
            }
        }
    }
}
