using UnityEngine;

namespace SfxShowcase.Tests
{
    /// <summary>
    /// Lets a <see cref="RunPilot"/> set the controls once per PHYSICS step, just before the player reads them. The
    /// tests run one physics step per frame, so there the pilot is ticked once a frame; a take runs at 30 frames a
    /// second with one or two physics steps in each, and a pilot ticked per frame would decide less often than the one
    /// the level's route was written for (it then misses a jump). With this the pilot plays as it does in the tests.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class CapturePilot : MonoBehaviour
    {
        /// <summary>The pilot to tick; null lets go of the controls' timing (the take sets them itself).</summary>
        public RunPilot Pilot { get; set; }

        void FixedUpdate() => Pilot?.Tick();
    }
}
