using UnityEngine;

namespace SfxShowcase.Core
{
    /// <summary>
    /// What the level's pieces may DO to the player. The player raises the matching <see cref="GameEvents"/> for what
    /// happens to it (Hurt, ChimesScattered, ChimeCollected, Respawned, GoalReached); the piece raises the event that is
    /// about itself (WooferBounced, ClipperDefeated, CheckpointReached, SectionEntered, FinalLaunch).
    /// </summary>
    public interface IPlayerActor
    {
        /// <summary>A Woofer fires: the velocity is replaced by this one (m/s, world space), whatever the player was doing.</summary>
        void Bounce(Vector2 launchVelocity);

        /// <summary>
        /// A hazard at this world position touched the player: knock-back away from it, the Chimes scatter, or – with
        /// none – a respawn. Returns false when nothing happened (invulnerable, or the run is over).
        /// </summary>
        bool Hurt(Vector2 sourcePosition);

        /// <summary>The player just defeated an enemy: a small upward rebound if it was airborne.</summary>
        void Rebound();

        /// <summary>Chimes picked up from the level.</summary>
        void AddChimes(int count);

        /// <summary>A Tuning Fork was passed: later respawns happen here. The index is the fork's, 0 = the first.</summary>
        void SetCheckpoint(int index, Vector2 respawnPosition);

        /// <summary>The Resonator was passed: control is taken away, the player brakes and the run ends.</summary>
        void Finish();

        /// <summary>A loop's switch: which terrain plane the player collides with from now on.</summary>
        void SetTerrainPlane(TerrainPlane plane);
    }
}
