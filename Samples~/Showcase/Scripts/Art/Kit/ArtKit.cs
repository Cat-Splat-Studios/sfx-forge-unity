using System;
using System.Collections.Generic;
using SfxShowcase.Core;
using UnityEngine;

namespace SfxShowcase.Art
{
    /// <summary>
    /// The art kit: the one place every visual of the game comes from. It is a thin front desk – each Create method
    /// hands over to the small generator class that knows that one thing (TerrainArt, ChimeArt, CharacterArt …), and
    /// the kit itself only owns what they share: the two materials, the cache of meshes that identical objects share,
    /// and the way a mesh becomes a GameObject.
    /// The scene builder creates one asset of this type and stores <see cref="OpaqueMaterial"/> and
    /// <see cref="TransparentMaterial"/> inside it; meshes made while the scene is built are stored in the scene
    /// (or, if the builder prefers, saved from <see cref="CollectGeneratedMeshes"/>).
    /// </summary>
    public sealed class ArtKit : ArtKitBase
    {
        [SerializeField, Tooltip("The unlit vertex-colour material of all solid geometry. Created on first use.")]
        Material opaqueMaterial;

        [SerializeField, Tooltip("The same kind of material, for geometry with alpha below 1. Created on first use.")]
        Material transparentMaterial;

        readonly Dictionary<string, Mesh> sharedMeshes = new Dictionary<string, Mesh>();
        readonly HashSet<Mesh> generatedMeshes = new HashSet<Mesh>();

        /// <inheritdoc/>
        public override Material OpaqueMaterial
        {
            get
            {
                if (opaqueMaterial == null)
                {
                    opaqueMaterial = ArtMaterials.CreateUnlit("Showcase Opaque");
                }

                return opaqueMaterial;
            }
        }

        /// <inheritdoc/>
        public override Material TransparentMaterial
        {
            get
            {
                if (transparentMaterial == null)
                {
                    transparentMaterial = ArtMaterials.CreateUnlit("Showcase Transparent");
                }

                return transparentMaterial;
            }
        }

        /// <summary>
        /// Every mesh the kit has put on an object since it was loaded and that still exists – the hook for a builder
        /// that wants to save generated meshes as assets instead of leaving them in the scene.
        /// </summary>
        public List<Mesh> CollectGeneratedMeshes()
        {
            var meshes = new List<Mesh>();
            foreach (Mesh mesh in generatedMeshes)
            {
                if (mesh != null)
                {
                    meshes.Add(mesh);
                }
            }

            return meshes;
        }

        /// <summary>
        /// The mesh shared by all objects of one kind (every Chime is the same mesh): made by <paramref name="create"/>
        /// the first time the key is asked for, and again if the mesh has been unloaded since.
        /// </summary>
        public Mesh SharedMesh(string key, Func<Mesh> create)
        {
            if (!sharedMeshes.TryGetValue(key, out Mesh mesh) || mesh == null)
            {
                mesh = create();
                sharedMeshes[key] = mesh;
            }

            return mesh;
        }

        /// <summary>
        /// A GameObject that draws one mesh: the only way a renderer is made in the game, so every renderer has one of
        /// the two materials, a place in the draw order and no lighting. The mesh may be null for objects that build
        /// their own mesh at runtime (the trail, HUD text).
        /// </summary>
        /// <param name="name">The object's name.</param>
        /// <param name="mesh">The mesh to draw, or null.</param>
        /// <param name="layer">Where it sits in the draw order.</param>
        /// <param name="orderOffset">Steps in front of (+) or behind (−) other parts on the same layer.</param>
        /// <param name="parent">The parent to attach to at the local origin, or null.</param>
        /// <param name="transparent">Whether any vertex of the mesh has alpha below 1.</param>
        public GameObject CreatePart(
            string name,
            Mesh mesh,
            DepthLayer layer,
            int orderOffset = 0,
            Transform parent = null,
            bool transparent = false
        )
        {
            var part = new GameObject(name);
            if (parent != null)
            {
                part.transform.SetParent(parent, false);
            }

            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = transparent ? TransparentMaterial : OpaqueMaterial;
            renderer.sortingOrder = (int)layer + orderOffset;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            if (mesh != null)
            {
                generatedMeshes.Add(mesh);
            }

            return part;
        }

        /// <inheritdoc/>
        public override GameObject CreateGround(IReadOnlyList<Vector2> surface, float bottomY) =>
            TerrainArt.CreateGround(this, surface, bottomY);

        /// <inheritdoc/>
        public override GameObject CreatePlatform(IReadOnlyList<Vector2> surface, float thickness) =>
            TerrainArt.CreatePlatform(this, surface, thickness);

        /// <inheritdoc/>
        public override GameObject CreateLoop(Vector2 center, float radius, float thickness) =>
            TerrainArt.CreateLoop(this, center, radius, thickness);

        /// <inheritdoc/>
        public override GameObject CreateChime() => ChimeArt.Create(this);

        /// <inheritdoc/>
        public override PropVisual CreateWoofer() => WooferArt.Create(this);

        /// <inheritdoc/>
        public override GameObject CreateStatic(int seed) => StaticArt.Create(this, seed);

        /// <inheritdoc/>
        public override GameObject CreateClipper() => ClipperArt.Create(this);

        /// <inheritdoc/>
        public override PropVisual CreateTuningFork() => ForkArt.CreateTuningFork(this);

        /// <inheritdoc/>
        public override PropVisual CreateResonator() => ForkArt.CreateResonator(this);

        /// <inheritdoc/>
        public override GameObject CreateCharacter(PlayerBase player) => CharacterArt.Create(this, player);

        /// <inheritdoc/>
        public override GameObject CreateBackground(Camera camera, Rect levelBounds) =>
            BackgroundArt.Create(this, camera, levelBounds);

        /// <inheritdoc/>
        public override GameObject CreateHud(Camera camera, PlayerBase player, GameSessionBase session) =>
            HudArt.Create(this, camera, player, session);

        /// <inheritdoc/>
        public override void SpawnEffect(EffectKind kind, Vector2 position) => EffectArt.Spawn(this, kind, position);
    }
}
