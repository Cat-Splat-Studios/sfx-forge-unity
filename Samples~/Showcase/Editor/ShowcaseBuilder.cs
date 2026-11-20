using System;
using System.Collections.Generic;
using System.IO;
using SfxShowcase.Art;
using SfxShowcase.Core;
using SfxShowcase.Gameplay;
using SfxShowcase.Level;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SfxShowcase.Editor
{
    /// <summary>
    /// Builds the showcase from nothing (SFX Forge > Rebuild Showcase Scene): the art kit asset, the generated meshes,
    /// one preset asset per sound effect, and the scene. <c>Showcase.unity</c> and everything under <c>Generated</c>
    /// are this script's output, so nothing there is edited by hand: change the level in <see cref="LevelLayout"/>,
    /// the sounds in <c>Scripts/Audio/SoundLibrary.cs</c>, the look in <c>Scripts/Art</c>, and rebuild.
    ///
    /// The build is six calls, one per folder of the project (ARCHITECTURE.md section 4). Each of them is ordinary
    /// runtime code that creates GameObjects and assigns references; only this file and <see cref="ShowcaseAudio"/>
    /// use the editor, to save what was made.
    ///
    /// Two headless entry points: <see cref="Verify"/> checks the scene as it is saved and writes nothing;
    /// <see cref="SelfTest"/> rebuilds first, which REWRITES <c>Showcase.unity</c> and <c>Generated/ShowcaseMeshes.asset</c>
    /// wholesale (a new scene has new file IDs), so run it when something changed, not to check.
    /// </summary>
    public static class ShowcaseBuilder
    {
        /// <summary>
        /// The showcase's own folder, wherever it is: <c>Assets/Showcase</c> in the demo project,
        /// <c>Assets/Samples/SFX Forge/&lt;version&gt;/Showcase</c> when it was imported as the package's sample, anywhere
        /// else after a move. It is found from this script's own assembly definition (<c>Editor/SfxShowcase.Editor.asmdef</c>,
        /// one folder below the root); no path of the showcase is written down anywhere.
        /// </summary>
        public static string Root
        {
            get
            {
                string assembly = typeof(ShowcaseBuilder).Assembly.GetName().Name;
                string definition = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assembly);
                if (string.IsNullOrEmpty(definition))
                    throw new InvalidOperationException($"the showcase cannot find its folder: the assembly {assembly} has no assembly definition file");
                return Path.GetDirectoryName(Path.GetDirectoryName(definition)).Replace('\\', '/');
            }
        }
        /// <summary>The generated scene.</summary>
        public static string ScenePath => Root + "/Showcase.unity";
        /// <summary>Everything else this script writes.</summary>
        public static string GeneratedFolder => Root + "/Generated";
        static string ArtKitPath => GeneratedFolder + "/ArtKit.asset";
        static string MeshesPath => GeneratedFolder + "/ShowcaseMeshes.asset";
        const string LogPrefix = "SHOWCASE: ";

        /// <summary>Serialized references that are empty on purpose, as "Type.field": the Resonator's fork is always lit and has no unlit look.</summary>
        static readonly HashSet<string> OptionalReferences = new HashSet<string> { "ForkVisual.unlitPart" };

        // ---- Build ----------------------------------------------------------------------------------------------

        /// <summary>
        /// Generates the scene and its assets. Callable from the menu and headless (-executeMethod). Besides the
        /// showcase's own folder it changes one project setting: the scene is added to the build settings' scene list.
        /// </summary>
        [MenuItem("SFX Forge/Rebuild Showcase Scene")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(GeneratedFolder)) AssetDatabase.CreateFolder(Root, "Generated");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // The old meshes go before anything is built, so the art kit never hands out a mesh that is about to vanish.
            AssetDatabase.DeleteAsset(MeshesPath);
            ArtKit art = LoadOrCreate<ArtKit>(ArtKitPath);
            var context = new ShowcaseContext { Art = art };

            // 1. the level: terrain, loops, every placed object, the triggers
            LevelLayout.Build(context);
            // 2. the player and the run
            GameplayInstaller.Install(context);
            // 3. the camera that follows the player
            LevelLayout.BuildCamera(context);
            context.Camera.backgroundColor = Palette.NightPlum;
            // 4. what the art kit draws around them: the character on the player, the background, the HUD
            GameObject character = art.CreateCharacter(context.Player);
            character.transform.SetParent(context.Player.transform, false);
            art.CreateBackground(context.Camera, context.Level.Bounds);
            art.CreateHud(context.Camera, context.Player, context.Session);
            // 5. THE SOUND: the one line that puts SFX FORGE into the game (ShowcaseAudio.cs: the song, the presets,
            //    AudioInstaller.Install). Without it the game plays the same, silently.
            ShowcaseAudio.Install(context.Player, context.Session);

            // 6. keep what was generated: the two materials inside the art kit asset, the meshes beside it, the scene
            StoreMaterial(art.OpaqueMaterial, art);
            StoreMaterial(art.TransparentMaterial, art);
            StoreMeshes(art.CollectGeneratedMeshes());
            EditorUtility.SetDirty(art);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log(LogPrefix + "scene built at " + ScenePath);
        }

        /// <summary>The asset at a path, created there if it does not exist yet (so its GUID and the references to it survive a rebuild).</summary>
        internal static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void StoreMaterial(Material material, ArtKit art)
        {
            if (!AssetDatabase.Contains(material)) AssetDatabase.AddObjectToAsset(material, art);
        }

        /// <summary>
        /// All generated meshes in one asset file beside the scene, instead of inside it. The file is made anew by every
        /// build (new GUID); the scene that refers to it is made anew by the same build.
        /// </summary>
        static void StoreMeshes(List<Mesh> meshes)
        {
            if (meshes.Count == 0) return;
            meshes.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            AssetDatabase.CreateAsset(meshes[0], MeshesPath);
            for (int i = 1; i < meshes.Count; i++) AssetDatabase.AddObjectToAsset(meshes[i], MeshesPath);
        }

        /// <summary>Adds the scene to the build settings and leaves the scenes already there (the SfxDemo scene) alone.</summary>
        static void AddToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(listed => IsScene(listed, scenePath))) return;
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>
        /// Whether an entry of the build settings is this scene: by GUID first, because the entry keeps the path it was
        /// added with when the showcase's folder is moved or copied into another project with its .meta files.
        /// </summary>
        static bool IsScene(EditorBuildSettingsScene listed, string scenePath)
        {
            return listed.path == scenePath || listed.guid.ToString() == AssetDatabase.AssetPathToGUID(scenePath);
        }

        // ---- SelfTest ---------------------------------------------------------------------------------------------

        /// <summary>
        /// Rebuilds everything (<see cref="Build"/>: the generated files are rewritten) and then checks it like
        /// <see cref="Verify"/>. Exits with 0 or 1
        /// (Unity -batchmode -executeMethod SfxShowcase.Editor.ShowcaseBuilder.SelfTest).
        /// </summary>
        public static void SelfTest() => RunChecks(rebuildFirst: true);

        /// <summary>
        /// Opens the saved scene and checks what can be checked without pressing Play: the level's numbers against
        /// <see cref="LevelLayout"/>, the collision conventions, every serialized reference, the presets, the proofs of
        /// DESIGN.md section 6. Writes nothing. Logs one "SHOWCASE: ok" or "SHOWCASE: FAIL" line per check and exits
        /// with 0 or 1 (Unity -batchmode -executeMethod SfxShowcase.Editor.ShowcaseBuilder.Verify).
        /// </summary>
        public static void Verify() => RunChecks(rebuildFirst: false);

        static void RunChecks(bool rebuildFirst)
        {
            int failed = 0;
            Action<bool, string> check = (passed, what) =>
            {
                Debug.Log(LogPrefix + (passed ? "ok    " : "FAIL  ") + what);
                if (!passed) failed++;
            };
            try
            {
                if (rebuildFirst) Build();
                Scene scene = EditorSceneManager.OpenScene(ScenePath);
                check(scene.IsValid(), "the saved scene opens");
                CheckLevel(check);
                CheckCollisionConventions(check);
                CheckSceneObjects(check);
                CheckSerializedReferences(check);
                ShowcaseAudio.Check(check);
                CheckProject(check);
            }
            catch (Exception exception)
            {
                Debug.LogError(LogPrefix + "FAIL  exception: " + exception);
                failed++;
            }
            Debug.Log(LogPrefix + (failed == 0 ? "PASSED" : $"FAILED ({failed})"));
            EditorApplication.Exit(failed == 0 ? 0 : 1);
        }

        /// <summary>The level describes itself sensibly, and every piece the layout's data places is in the scene.</summary>
        static void CheckLevel(Action<bool, string> check)
        {
            var level = UnityEngine.Object.FindFirstObjectByType<LevelDescriptor>();
            check(level != null, "the scene has a LevelDescriptor");
            if (level == null) return;

            check(Sections.At(level.SpawnPoint.x) == SectionId.SoftStart && StandsOverGround(level.SpawnPoint),
                  $"the spawn point {level.SpawnPoint} is in the first section, just above ground");
            check(Sections.At(level.GoalPosition.x) == SectionId.TheResonator && StandsOverGround(level.GoalPosition + Vector2.up * 0.5f),
                  $"the goal {level.GoalPosition} is in the last section, on the ground");
            check(level.Bounds.xMin <= Sections.LevelStartX && level.Bounds.xMax >= Sections.LevelEndX && level.Bounds.Contains(level.SpawnPoint) && level.Bounds.Contains(level.GoalPosition),
                  $"the bounds {level.Bounds} hold the whole level");
            check(level.KillY < level.Bounds.yMin, $"the kill height {level.KillY} is below what the camera shows");

            LevelLayout.PieceCounts placed = LevelLayout.CountPieces();
            bool checkpointsInOrder = level.Checkpoints.Length == placed.TuningForks;
            for (int i = 0; i < level.Checkpoints.Length; i++)
            {
                checkpointsInOrder &= StandsOverGround(level.Checkpoints[i]);
                if (i > 0) checkpointsInOrder &= level.Checkpoints[i].x > level.Checkpoints[i - 1].x;
            }
            check(checkpointsInOrder, $"{placed.TuningForks} checkpoints, left to right, each just above ground");

            int chimes = Count<Chime>();
            check(chimes == level.ChimeCount && chimes >= 145 && chimes <= 155, $"{chimes} Chimes placed, as the descriptor says (the design asks for 150 ± 5)");
            check(Count<Woofer>() == placed.Woofers && Count<StaticBurr>() == placed.Statics && Count<Clipper>() == placed.Clippers
                  && Count<TuningFork>() == placed.TuningForks && Count<Resonator>() == 1,
                  $"{placed.Woofers} Woofers, {placed.Statics} Static burrs, {placed.Clippers} Clippers, {placed.TuningForks} Tuning Forks and the Resonator, as the layout places them");
            check(Count<PlaneSwitch>() == placed.PlaneSwitches && Count<SectionTrigger>() == 1 && Count<FinalLaunchTrigger>() == 1,
                  $"{placed.Loops} loops with their {placed.PlaneSwitches} plane switches, the section trigger and the final launch trigger");
        }

        /// <summary>Whether ground lies within a metre below a point (the physics scene answers in the editor too).</summary>
        static bool StandsOverGround(Vector2 point)
        {
            Physics2D.SyncTransforms();
            foreach (RaycastHit2D hit in Physics2D.RaycastAll(point, Vector2.down, 1f))
                if (!hit.collider.isTrigger && hit.collider.GetComponent<GroundSurface>() != null) return true;
            return false;
        }

        /// <summary>ARCHITECTURE.md 3.2: ground is a solid EdgeCollider2D with a GroundSurface; every other collider is a trigger.</summary>
        static void CheckCollisionConventions(Action<bool, string> check)
        {
            GroundSurface[] surfaces = UnityEngine.Object.FindObjectsByType<GroundSurface>(FindObjectsSortMode.None);
            bool groundIsEdges = surfaces.Length > 0;
            int planeA = 0, planeB = 0, oneWay = 0;
            foreach (GroundSurface surface in surfaces)
            {
                var edge = surface.GetComponent<EdgeCollider2D>();
                groundIsEdges &= edge != null && !edge.isTrigger && edge.pointCount >= 2;
                if (surface.Plane == TerrainPlane.A) planeA++;
                if (surface.Plane == TerrainPlane.B) planeB++;
                if (surface.OneWay) oneWay++;
            }
            check(groundIsEdges, $"all {surfaces.Length} pieces of ground are solid EdgeCollider2Ds");
            LevelLayout.PieceCounts placed = LevelLayout.CountPieces();
            check(planeA == placed.Loops && planeB == placed.Loops && oneWay == placed.Platforms,
                  $"each of the {placed.Loops} loops has a half on plane A and a half on plane B; the {placed.Platforms} platforms are one-way");

            bool othersAreTriggers = true;
            foreach (Collider2D collider in UnityEngine.Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
                if (collider.GetComponent<GroundSurface>() == null) othersAreTriggers &= collider.isTrigger;
            check(othersAreTriggers, "every collider that is not ground is a trigger");
        }

        /// <summary>The objects the build steps make, with the components the contracts ask for.</summary>
        static void CheckSceneObjects(Action<bool, string> check)
        {
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            var body = player != null ? player.GetComponent<Rigidbody2D>() : null;
            var touch = player != null ? player.GetComponent<CircleCollider2D>() : null;
            check(body != null && body.bodyType == RigidbodyType2D.Kinematic && touch != null && touch.isTrigger,
                  "the player has a kinematic Rigidbody2D and a trigger CircleCollider2D");
            check(player != null && player.GetComponentInChildren<CharacterVisual>() != null, "the player carries its generated visual");
            check(Count<GameSession>() == 1, "the scene has its game session");

            var camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            check(camera != null && camera.orthographic && camera.CompareTag("MainCamera") && camera.GetComponent<FollowCamera>() != null && Count<AudioListener>() == 1,
                  "one orthographic main camera with the follow camera and the only AudioListener");
            check(Count<HudView>() == 1 && Count<ParallaxBackground>() == 1, "the HUD and the parallax background are in the scene");

            bool rendererHasAssets = true;
            int renderers = 0;
            foreach (MeshRenderer renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                renderers++;
                rendererHasAssets &= renderer.sharedMaterial != null && AssetDatabase.GetAssetPath(renderer.sharedMaterial) == ArtKitPath;
            }
            check(renderers > 0 && rendererHasAssets, $"all {renderers} renderers use one of the art kit's two materials");
            check(AssetDatabase.LoadAssetAtPath<Mesh>(MeshesPath) != null, "the generated meshes are saved in " + MeshesPath);
        }

        /// <summary>
        /// Every object reference on every component of the showcase is assigned: nothing is left to be found at
        /// runtime. Walks the serialized fields, so a reference added later is covered without touching this check.
        /// </summary>
        static void CheckSerializedReferences(Action<bool, string> check)
        {
            int components = 0, references = 0;
            var missing = new List<string>();
            foreach (MonoBehaviour component in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string space = component.GetType().Namespace;
                if (space == null || !space.StartsWith("SfxShowcase", StringComparison.Ordinal)) continue;
                components++;
                SerializedProperty property = new SerializedObject(component).GetIterator();
                while (property.NextVisible(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference || property.name == "m_Script") continue;
                    if (OptionalReferences.Contains($"{component.GetType().Name}.{property.propertyPath}")) continue;
                    references++;
                    if (property.objectReferenceValue == null) missing.Add($"{component.name} ({component.GetType().Name}).{property.propertyPath}");
                }
            }
            string detail = missing.Count == 0 ? "" : ": missing " + string.Join(", ", missing.GetRange(0, Mathf.Min(missing.Count, 10)));
            check(components > 0 && missing.Count == 0, $"{references} serialized references on {components} showcase components are all assigned{detail}");
        }

        /// <summary>The proofs of DESIGN.md section 6 that are about files: no audio assets, one assembly knows SFX FORGE.</summary>
        static void CheckProject(Action<bool, string> check)
        {
            string root = Root;
            var audioFiles = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { root }))
                audioFiles.Add(AssetDatabase.GUIDToAssetPath(guid));
            check(audioFiles.Count == 0, "there is no audio file under " + root + (audioFiles.Count == 0 ? "" : ": " + string.Join(", ", audioFiles)));

            var textures = AssetDatabase.FindAssets("t:Texture t:Model t:Font", new[] { root });
            check(textures.Length == 0, "there is no texture, model or font under " + root);

            bool onlyAudioKnowsSfxForge = true;
            foreach (string folder in new[] { "Core", "Gameplay", "Level", "Art" })
                foreach (string file in Directory.GetFiles($"{root}/Scripts/{folder}", "*.*", SearchOption.AllDirectories))
                    if ((file.EndsWith(".cs") || file.EndsWith(".asmdef")) && File.ReadAllText(file).Contains("CatSplat")) onlyAudioKnowsSfxForge = false;
            check(onlyAudioKnowsSfxForge, "only Scripts/Audio references CatSplat.SfxForge");

            string scenePath = ScenePath;
            check(Array.Exists(EditorBuildSettings.scenes, listed => IsScene(listed, scenePath) && listed.enabled),
                  "the scene is in the build settings (if it is not: SFX Forge > Rebuild Showcase Scene adds it)");
        }

        static int Count<T>() where T : UnityEngine.Object
        {
            return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        }
    }
}
