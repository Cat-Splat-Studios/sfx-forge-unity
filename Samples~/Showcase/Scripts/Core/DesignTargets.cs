using UnityEngine;

namespace SfxShowcase.Core
{
    /// <summary>
    /// The few numbers more than one discipline builds against (DESIGN.md section 2), so the level can be laid out
    /// while the movement is being written. The gameplay code keeps its own tunable values and must stay consistent
    /// with these; a change here is a change to the level.
    /// </summary>
    public static class DesignTargets
    {
        /// <summary>Downward acceleration in the air, m/s².</summary>
        public const float Gravity = 30f;

        /// <summary>The speed flat running tops out at, m/s.</summary>
        public const float CruiseSpeed = 10f;

        /// <summary>The hard speed cap, m/s: what "speed01 = 1" means.</summary>
        public const float TopSpeed = 22f;

        /// <summary>The height of a full jump from flat ground, metres.</summary>
        public const float JumpHeight = 3.5f;

        /// <summary>The player's collision radius standing, metres.</summary>
        public const float StandingRadius = 0.5f;

        /// <summary>The player's collision radius tucked, metres.</summary>
        public const float TuckedRadius = 0.4f;

        /// <summary>A Chime collected within this many seconds of the last one continues the combo.</summary>
        public const float ChimeComboWindow = 1.5f;

        /// <summary>The most Chimes that are spawned when the player is hit.</summary>
        public const int MaxScatteredChimes = 24;

        /// <summary>How far below the lowest ground the player respawns, metres.</summary>
        public const float KillDepth = 15f;

        /// <summary>The distance between two footfalls at walking pace, metres.</summary>
        public const float StrideAtRest = 0.9f;

        /// <summary>
        /// The distance between two footfalls at the speed cap, metres: longer, so the cadence rises with speed without
        /// turning into a buzz.
        /// </summary>
        public const float StrideAtTopSpeed = 2.6f;

        /// <summary>
        /// The distance between two footfalls (m) at a speed (speed ÷ top speed, 0..1). The footstep event and the
        /// character's legs both step by this, so what is heard is what is seen.
        /// </summary>
        public static float StrideLength(float speed01) =>
            Mathf.Lerp(StrideAtRest, StrideAtTopSpeed, Mathf.Clamp01(speed01));

        /// <summary>
        /// The upward launch speed (m/s) that reaches a height (m) under <see cref="Gravity"/> – for placing Woofers.
        /// </summary>
        public static float LaunchSpeedForHeight(float height) => Mathf.Sqrt(2f * Gravity * Mathf.Max(0f, height));
    }
}
