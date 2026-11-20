using UnityEngine;

namespace SfxShowcase.Core
{
    /// <summary>
    /// What the built level tells the rest of the game about itself. The level's construction code adds it to the level's
    /// root object and fills it in; the player, the camera, the background and the tests read it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelDescriptor : MonoBehaviour
    {
        [Tooltip("Where the player's centre is when a run starts.")]
        [SerializeField] Vector2 spawnPoint;

        [Tooltip("The level's extent in world space: the camera stays inside it.")]
        [SerializeField] Rect bounds;

        [Tooltip("Below this world y the player has fallen out and respawns.")]
        [SerializeField] float killY;

        [Tooltip("Where the Resonator stands (the ground between its prongs).")]
        [SerializeField] Vector2 goalPosition;

        [Tooltip("The respawn point of each Tuning Fork, in order.")]
        [SerializeField] Vector2[] checkpoints = new Vector2[0];

        [Tooltip("How many Chimes are placed in the level.")]
        [SerializeField] int chimeCount;

        /// <summary>Where the player's centre is when a run starts.</summary>
        public Vector2 SpawnPoint => spawnPoint;
        /// <summary>The level's extent in world space: the camera stays inside it.</summary>
        public Rect Bounds => bounds;
        /// <summary>Below this world y the player has fallen out and respawns.</summary>
        public float KillY => killY;
        /// <summary>Where the Resonator stands (the ground between its prongs).</summary>
        public Vector2 GoalPosition => goalPosition;
        /// <summary>The respawn point of each Tuning Fork, in order (the index a fork passes to SetCheckpoint).</summary>
        public Vector2[] Checkpoints => checkpoints;
        /// <summary>How many Chimes are placed in the level.</summary>
        public int ChimeCount => chimeCount;

        /// <summary>Fills in the description; called once by the level's construction code.</summary>
        public void Configure(Vector2 levelSpawnPoint, Rect levelBounds, float levelKillY, Vector2 levelGoalPosition,
                              Vector2[] levelCheckpoints, int levelChimeCount)
        {
            spawnPoint = levelSpawnPoint;
            bounds = levelBounds;
            killY = levelKillY;
            goalPosition = levelGoalPosition;
            checkpoints = levelCheckpoints;
            chimeCount = levelChimeCount;
        }
    }
}
