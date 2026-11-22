using UnityEngine;

namespace SfxShowcase.Core
{
    /// <summary>
    /// What the rest of the game may READ about the player: the audio (roll hum, wind, music intensity), the camera,
    /// the HUD, the character's visual and the level pieces. Metres, seconds, degrees.
    /// </summary>
    public interface IPlayerState
    {
        /// <summary>The centre of the player in world space.</summary>
        Vector2 Position { get; }

        /// <summary>The velocity in world space, m/s.</summary>
        Vector2 Velocity { get; }

        /// <summary>
        /// The signed speed along the ground, m/s (positive = towards +x on flat ground); 0 while airborne.
        /// </summary>
        float GroundSpeed { get; }

        /// <summary>
        /// How fast the player is travelling, m/s: the speed along the ground, or the sideways speed in the air, never
        /// above <see cref="TopSpeed"/>. Rising and falling do not count, so a fall or a Woofer's throw is not "fast":
        /// this is the speed the meter, the music, the wind and the results show.
        /// </summary>
        float Speed { get; }

        /// <summary>The speed cap, m/s: the value "speed01" is normalised by.</summary>
        float TopSpeed { get; }

        /// <summary><see cref="Speed"/> ÷ <see cref="TopSpeed"/>, clamped to 0..1.</summary>
        float Speed01 { get; }

        /// <summary>Standing or moving on the ground (walls and ceilings of a loop included).</summary>
        bool IsGrounded { get; }

        /// <summary>
        /// The angle of the ground under the player, degrees counter-clockwise from flat; 0 while airborne.
        /// </summary>
        float GroundAngle { get; }

        /// <summary>Tucked into a ball: rolling on the ground, or in the air after a jump or a Burst.</summary>
        bool IsRolling { get; }

        /// <summary>Winding up a Burst on the spot.</summary>
        bool IsCharging { get; }

        /// <summary>The Wind-Up's charge, 0..1; 0 when not charging.</summary>
        float Charge01 { get; }

        /// <summary>Which way the player faces: +1 right, -1 left.</summary>
        int Facing { get; }

        /// <summary>Whether touching a Clipper now defeats it (tucked) instead of hurting the player.</summary>
        bool IsAttacking { get; }

        /// <summary>Blinking after a hit: hazards do nothing.</summary>
        bool IsInvulnerable { get; }

        /// <summary>
        /// Whether the player's input moves the character (false on the title card, while respawning and
        /// after the goal).
        /// </summary>
        bool HasControl { get; }

        /// <summary>The terrain plane the player currently collides with (loops switch it).</summary>
        TerrainPlane Plane { get; }
    }
}
