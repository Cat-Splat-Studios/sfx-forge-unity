using System.Collections.Generic;
using UnityEngine;

namespace SfxShowcase.Core
{
    /// <summary>
    /// The art kit as an asset, so components can hold a serialized reference to it (an interface cannot be serialized)
    /// and nothing needs a singleton. The art folder's <c>ArtKit</c> derives from this; the scene builder creates the
    /// one asset, stores its two materials inside it and hands it to whoever spawns visuals.
    /// </summary>
    public abstract class ArtKitBase : ScriptableObject, IArtKit
    {
        /// <summary>The one unlit vertex-colour material of all opaque geometry; created on first use.</summary>
        public abstract Material OpaqueMaterial { get; }

        /// <summary>The same with alpha blending (trail, sparkles, fades, HUD panels); created on first use.</summary>
        public abstract Material TransparentMaterial { get; }

        /// <inheritdoc/>
        public abstract GameObject CreateGround(IReadOnlyList<Vector2> surface, float bottomY);
        /// <inheritdoc/>
        public abstract GameObject CreatePlatform(IReadOnlyList<Vector2> surface, float thickness);
        /// <inheritdoc/>
        public abstract GameObject CreateLoop(Vector2 center, float radius, float thickness);
        /// <inheritdoc/>
        public abstract GameObject CreateChime();
        /// <inheritdoc/>
        public abstract PropVisual CreateWoofer();
        /// <inheritdoc/>
        public abstract GameObject CreateStatic(int seed);
        /// <inheritdoc/>
        public abstract GameObject CreateClipper();
        /// <inheritdoc/>
        public abstract PropVisual CreateTuningFork();
        /// <inheritdoc/>
        public abstract PropVisual CreateResonator();
        /// <inheritdoc/>
        public abstract GameObject CreateCharacter(PlayerBase player);
        /// <inheritdoc/>
        public abstract GameObject CreateBackground(Camera camera, Rect levelBounds);
        /// <inheritdoc/>
        public abstract GameObject CreateHud(Camera camera, PlayerBase player, GameSessionBase session);
        /// <inheritdoc/>
        public abstract void SpawnEffect(EffectKind kind, Vector2 position);
    }
}
