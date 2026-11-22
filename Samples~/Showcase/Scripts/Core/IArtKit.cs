using System.Collections.Generic;
using UnityEngine;

namespace SfxShowcase.Core
{
    /// <summary>
    /// Everything the level and the gameplay code ask the art for. Every Create method returns a new, unparented
    /// GameObject at the world origin that is ONLY a visual: generated meshes and, where it moves by itself, the art's
    /// own animation component. No colliders and no game behaviour – the caller adds those, names it, parents it and
    /// places it (z = 0; the visual orders its own depth). All colours, outlines and materials are decided inside.
    /// The methods work in the editor (the scene builder) and at runtime (scattered Chimes, effects).
    /// </summary>
    public interface IArtKit
    {
        /// <summary>
        /// A strip of ground: the surface polyline (world space, left to right, at least two points) with the cream top
        /// line, filled down to bottomY with the banded body.
        /// </summary>
        GameObject CreateGround(IReadOnlyList<Vector2> surface, float bottomY);

        /// <summary>
        /// A floating platform: the surface polyline (world space, left to right) with the cream top line and a body of
        /// the given thickness (metres) under it.
        /// </summary>
        GameObject CreatePlatform(IReadOnlyList<Vector2> surface, float thickness);

        /// <summary>
        /// A loop: a ring whose inner face (the surface the player runs on) has the given radius around the centre,
        /// both in world space, with a wall of the given thickness outside it. The right half (plane A, ridden first)
        /// draws behind the player, the left half (plane B) in front.
        /// </summary>
        GameObject CreateLoop(Vector2 center, float radius, float thickness);

        /// <summary>A Chime, 0.5 m tall, spinning by itself. Origin: its centre.</summary>
        GameObject CreateChime();

        /// <summary>
        /// A Woofer firing along its local +y. Origin: the centre of its base on the ground. Activate = the cone
        /// punches out and wobbles back.
        /// </summary>
        PropVisual CreateWoofer();

        /// <summary>
        /// A Static burr, 2 m wide and 1 m tall; the seed picks its spikes. Origin: the centre of its base.
        /// </summary>
        GameObject CreateStatic(int seed);

        /// <summary>
        /// A Clipper, 1.2 m wide, facing local +x, walking on the spot by itself. Origin: between its
        /// feet on the ground.
        /// </summary>
        GameObject CreateClipper();

        /// <summary>
        /// A Tuning Fork, 3 m tall, unlit. Origin: the foot of its stem. Activate = lit, and it stays lit.
        /// </summary>
        PropVisual CreateTuningFork();

        /// <summary>
        /// The Resonator arch, 12 m tall. Origin: the ground between its prongs. Activate = it rings.
        /// </summary>
        PropVisual CreateResonator();

        /// <summary>
        /// The player's character, animated from the player's state (crest lean, feet, tuck, wind-up spin, blink,
        /// trail). Origin: the player's centre – the caller parents it to the player at the local origin.
        /// </summary>
        GameObject CreateCharacter(PlayerBase player);

        /// <summary>
        /// The sky and the three parallax silhouette layers, following the camera by themselves; levelBounds is the
        /// level's extent in world space (x, y, width, height).
        /// </summary>
        GameObject CreateBackground(Camera camera, Rect levelBounds);

        /// <summary>
        /// The HUD (Chime count, time, speed meter, section name), the title card and the results panel, drawn in front
        /// of the camera and showing whatever the session's phase asks for.
        /// </summary>
        GameObject CreateHud(Camera camera, PlayerBase player, GameSessionBase session);

        /// <summary>
        /// Spawns a short burst at a world position; it removes itself when it has played. Runtime only.
        /// </summary>
        void SpawnEffect(EffectKind kind, Vector2 position);
    }
}
