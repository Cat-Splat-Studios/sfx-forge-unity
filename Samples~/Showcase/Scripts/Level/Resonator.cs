using SfxShowcase.Core;
using UnityEngine;

namespace SfxShowcase.Level
{
    /// <summary>
    /// The Resonator: the goal. Running through it ends the run – the player takes it from there (it brakes, and raises
    /// <see cref="GameEvents.GoalReached"/> itself) – and the arch rings.
    /// </summary>
    public sealed class Resonator : MonoBehaviour
    {
        [Tooltip("The arch's visual, which rings when the player passes.")]
        [SerializeField]
        PropVisual visual;

        bool reached;

        /// <summary>Sets the visual; called by the level's construction code.</summary>
        public void Configure(PropVisual resonatorVisual)
        {
            visual = resonatorVisual;
        }

        void OnEnable() => GameEvents.GameStarted += Restore;

        void OnDisable() => GameEvents.GameStarted -= Restore;

        void OnTriggerEnter2D(Collider2D other)
        {
            if (reached || !other.TryGetComponent(out PlayerBase player))
            {
                return;
            }

            reached = true;
            player.Finish();
            visual.Activate();
        }

        void Restore()
        {
            reached = false;
            visual.ResetVisual();
        }
    }
}
