using SfxShowcase.Core;
using UnityEngine;

namespace SfxShowcase.Level
{
    /// <summary>
    /// A Tuning Fork: the checkpoint. The first time the player passes it, it becomes the place later respawns happen,
    /// lights up and raises <see cref="GameEvents.CheckpointReached"/>. Passing it again does nothing until a new run
    /// starts.
    /// </summary>
    public sealed class TuningFork : MonoBehaviour
    {
        [Tooltip("Which fork this is: 0 = the first in the level.")]
        [SerializeField]
        int index;

        [Tooltip("Where the player's centre reappears, world space.")]
        [SerializeField]
        Vector2 respawnPoint;

        [Tooltip("The fork's visual, lit when passed.")]
        [SerializeField]
        PropVisual visual;

        bool lit;

        /// <summary>Whether the fork has been passed in the current run.</summary>
        public bool IsLit => lit;

        /// <summary>Sets the fork's index, respawn point and visual; called by the level's construction code.</summary>
        public void Configure(int forkIndex, Vector2 forkRespawnPoint, PropVisual forkVisual)
        {
            index = forkIndex;
            respawnPoint = forkRespawnPoint;
            visual = forkVisual;
        }

        void OnEnable() => GameEvents.GameStarted += Restore;

        void OnDisable() => GameEvents.GameStarted -= Restore;

        void OnTriggerEnter2D(Collider2D other)
        {
            if (lit || !other.TryGetComponent(out PlayerBase player))
            {
                return;
            }

            lit = true;
            player.SetCheckpoint(index, respawnPoint);
            visual.Activate();
            GameEvents.RaiseCheckpointReached(index);
        }

        void Restore()
        {
            lit = false;
            visual.ResetVisual();
        }
    }
}
