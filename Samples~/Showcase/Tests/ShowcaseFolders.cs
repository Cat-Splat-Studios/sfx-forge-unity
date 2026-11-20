using System;
using System.IO;
using UnityEngine;

namespace SfxShowcase.Tests
{
    /// <summary>
    /// Where the showcase is and where the tests write, without a path written down: the showcase may be
    /// <c>Assets/Showcase</c> of the demo project, the package's sample under <c>Assets/Samples</c>, or anywhere else.
    /// The root is found from this assembly's definition file (<c>Tests/SfxShowcase.Tests.asmdef</c>, one folder below
    /// the root), the same way <c>Editor/ShowcaseBuilder.cs</c> finds it.
    /// </summary>
    static class ShowcaseFolders
    {
        /// <summary>The showcase's own folder as an asset path. Known in the editor only: a player has no folders.</summary>
        public static string Root
        {
            get
            {
#if UNITY_EDITOR
                string assembly = typeof(ShowcaseFolders).Assembly.GetName().Name;
                string definition = UnityEditor.Compilation.CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assembly);
                if (string.IsNullOrEmpty(definition))
                    throw new InvalidOperationException($"the showcase's tests cannot find their folder: the assembly {assembly} has no assembly definition file");
                return Path.GetDirectoryName(Path.GetDirectoryName(definition)).Replace('\\', '/');
#else
                throw new InvalidOperationException("the showcase's folder is only known in the editor");
#endif
            }
        }

        /// <summary>The generated scene.</summary>
        public static string ScenePath => Root + "/Showcase.unity";

        /// <summary>The showcase's own song asset.</summary>
        public static string SongPath => Root + "/Song/DemoSong.asset";

        /// <summary>
        /// The folder all capture takes share: the folder the environment variable names; else, in the SFX FORGE
        /// repository (the project is its <c>unity/Demo</c>), <c>out/unity-video</c> at the repository's root; else
        /// <c>Captures</c> in the project's folder, beside <c>Assets</c>.
        /// </summary>
        public static string CaptureFolder(string environmentVariable)
        {
            string chosen = Environment.GetEnvironmentVariable(environmentVariable);
            if (!string.IsNullOrEmpty(chosen)) return Path.GetFullPath(chosen);
            string repository = Repository;
            return repository != null
                ? Path.Combine(repository, "out", "unity-video")
                : Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures"));
        }

        /// <summary>
        /// The root of the SFX FORGE repository when the project is its <c>unity/Demo</c> (two folders up there is the
        /// firmware's folder and the package's), otherwise null.
        /// </summary>
        static string Repository
        {
            get
            {
                string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
                bool isRepository = Directory.Exists(Path.Combine(root, "sfx-forge"))
                                    && File.Exists(Path.Combine(root, "unity", "com.catsplat.sfxforge", "package.json"));
                return isRepository ? root : null;
            }
        }
    }
}
