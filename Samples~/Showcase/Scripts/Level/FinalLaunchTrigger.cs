using SfxShowcase.Core;
using UnityEngine;

namespace SfxShowcase.Level
{
    /// <summary>
    /// The trigger at the lip of the last launch ramp. It raises <see cref="GameEvents.FinalLaunch"/> – the music's cue
    /// for its turnaround – the first time the player leaves the ramp towards the goal, and is armed again when a run
    /// starts or the player respawns.
    /// </summary>
    public sealed class FinalLaunchTrigger : MonoBehaviour
    {
        bool fired;

        void OnEnable()
        {
            GameEvents.GameStarted += Arm;
            GameEvents.Respawned += ArmAfterRespawn;
        }

        void OnDisable()
        {
            GameEvents.GameStarted -= Arm;
            GameEvents.Respawned -= ArmAfterRespawn;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (fired || !other.TryGetComponent(out PlayerBase player) || player.Velocity.x <= 0f) return;
            fired = true;
            GameEvents.RaiseFinalLaunch();
        }

        void Arm() => fired = false;

        void ArmAfterRespawn(SectionId section) => Arm();
    }
}
