using System;
using SfxShowcase.Core;
using UnityEngine;

namespace SfxShowcase.Gameplay
{
    /// <summary>
    /// Every number that shapes how the player moves, in one place. Units are metres, seconds and degrees.
    /// The values that the level is also built against (gravity, cruise and top speed, jump height, the two radii)
    /// start from <see cref="DesignTargets"/>; change those there, not here, or the level's jumps and Woofers stop
    /// fitting. Each value's comment says what it feels like, so it can be tuned in the Inspector on the player.
    /// </summary>
    [Serializable]
    public sealed class PlayerTuning
    {
        // ---- Running ---------------------------------------------------------------------------------------------

        /// <summary>
        /// How fast holding forward builds speed on flat ground. 5 = two seconds from rest to cruise:
        /// gentle, never twitchy.
        /// </summary>
        [Tooltip("Ground acceleration while holding forward, m/s². 5 = two seconds from rest to cruise.")]
        public float acceleration = 5f;

        /// <summary>How hard pressing against the motion brakes. High = a short, sharp skid.</summary>
        [Tooltip("Braking when pressing against the motion, m/s². High = a short skid.")]
        public float braking = 40f;

        /// <summary>How fast the player slows with no input. Low = a long, readable coast.</summary>
        [Tooltip("Slow-down with no input, m/s². Low = a long coast.")]
        public float friction = 4f;

        /// <summary>
        /// The speed flat running tops out at; anything above it has to be earned from slopes, Woofers and Bursts.
        /// </summary>
        [Tooltip("The speed holding forward reaches on flat ground, m/s.")]
        public float cruiseSpeed = DesignTargets.CruiseSpeed;

        /// <summary>The hard cap on ground speed, and what "speed01 = 1" means for the audio and the HUD.</summary>
        [Tooltip("The hard cap on ground speed, m/s.")]
        public float topSpeed = DesignTargets.TopSpeed;

        /// <summary>
        /// How fast earned speed above cruise leaks away while still holding forward. Low = a good line
        /// keeps its speed.
        /// </summary>
        [Tooltip("Slow-down above cruise speed while holding forward, m/s².")]
        public float overCruiseDrag = 1f;

        /// <summary>Braking from at least this speed counts as a skid (the scrape sound and the dust).</summary>
        [Tooltip("Braking from at least this speed is a skid, m/s.")]
        public float skidMinSpeed = 5f;

        /// <summary>Stick deflection below this is ignored, so a resting gamepad does not creep.</summary>
        [Tooltip("Left/right input below this is ignored (0..1).")]
        public float moveDeadZone = 0.3f;

        // ---- Slopes ----------------------------------------------------------------------------------------------

        /// <summary>
        /// How strongly a slope pulls a running player: downhill adds this times the sine of the slope, uphill takes
        /// it. Higher = hills matter more.
        /// </summary>
        [Tooltip("Slope pull while running, m/s² at a vertical wall (scaled by the sine of the slope).")]
        public float slopeFactorRunning = 7f;

        /// <summary>
        /// The slope's drag on a tucked player going uphill. Stronger than running: a Tuck gives up climbing power.
        /// </summary>
        [Tooltip("Slope pull while tucked and going uphill, m/s².")]
        public float slopeFactorTuckedUphill = 10f;

        /// <summary>
        /// The slope's push on a tucked player going downhill. The biggest number here: tucking down a hill is the
        /// fastest thing in the game.
        /// </summary>
        [Tooltip("Slope pull while tucked and going downhill, m/s².")]
        public float slopeFactorTuckedDownhill = 20f;

        /// <summary>
        /// On slopes gentler than this a player who is not steering stands firm and coasts to a stop (friction
        /// applies); on steeper ones there is no friction without input, so the slope carries a coasting runner and
        /// slides a standing one.
        /// </summary>
        [Tooltip(
            "Slopes gentler than this hold a player who is not steering; steeper ones carry it downhill. Degrees."
        )]
        public float standFirmAngle = 23f;

        /// <summary>
        /// Below this ground speed a steep surface no longer holds the player: speed is what keeps you on a wall.
        /// </summary>
        [Tooltip("Minimum ground speed to stay in control on a steep surface, m/s.")]
        public float slipSpeed = 3f;

        /// <summary>
        /// Surfaces steeper than this count as steep: too slow on them and the controls lock while the
        /// slope takes over.
        /// </summary>
        [Tooltip("Steeper than this is 'steep' for slipping, degrees.")]
        public float slipAngle = 45f;

        /// <summary>Surfaces steeper than this drop a too-slow player off altogether (walls and ceilings).</summary>
        [Tooltip("Steeper than this, a too-slow player falls off, degrees.")]
        public float detachAngle = 70f;

        /// <summary>
        /// How long left/right is ignored after slipping. Long enough that the player cannot fight the slope, short
        /// enough not to annoy.
        /// </summary>
        [Tooltip("Seconds of ignored left/right after slipping on a steep surface.")]
        public float slipControlLock = 0.5f;

        // ---- Tuck (roll) -----------------------------------------------------------------------------------------

        /// <summary>Pressing down at or above this ground speed tucks into a roll; slower, it is a crouch.</summary>
        [Tooltip("Minimum ground speed to start a Tuck, m/s.")]
        public float tuckStartSpeed = 1f;

        /// <summary>A roll slower than this untucks by itself.</summary>
        [Tooltip("A Tuck slower than this ends by itself, m/s.")]
        public float tuckEndSpeed = 0.5f;

        /// <summary>Slow-down while tucked on flat ground. Half the running friction or less: a roll carries.</summary>
        [Tooltip("Friction while tucked, m/s².")]
        public float tuckFriction = 1.5f;

        /// <summary>
        /// Braking while tucked when pressing against the motion. Much weaker than the running brake: a roll
        /// is a commitment.
        /// </summary>
        [Tooltip("Braking while tucked when pressing against the motion, m/s².")]
        public float tuckBraking = 10f;

        /// <summary>The collision radius while running or standing.</summary>
        [Tooltip("Collision radius standing, m.")]
        public float standingRadius = DesignTargets.StandingRadius;

        /// <summary>The collision radius while tucked: a smaller target.</summary>
        [Tooltip("Collision radius tucked, m.")]
        public float tuckedRadius = DesignTargets.TuckedRadius;

        // ---- Jump and air ----------------------------------------------------------------------------------------

        /// <summary>Downward acceleration in the air. Snappy rather than floaty.</summary>
        [Tooltip("Gravity in the air, m/s².")]
        public float gravity = DesignTargets.Gravity;

        /// <summary>How high a fully held jump rises from flat ground.</summary>
        [Tooltip("Height of a fully held jump, m.")]
        public float jumpHeight = DesignTargets.JumpHeight;

        /// <summary>
        /// Letting go of Jump while rising faster than this drops the upward speed to it at once: a tap is a hop.
        /// </summary>
        [Tooltip("Upward speed left when Jump is released early, m/s.")]
        public float jumpCutSpeed = 6f;

        /// <summary>
        /// How fast left/right steers in the air. Enough to place a landing, not enough to reverse instantly.
        /// </summary>
        [Tooltip("Air steering, m/s².")]
        public float airAcceleration = 12f;

        /// <summary>
        /// A gentle loss of sideways speed near the top of a rise, so floaty hops do not carry forever.
        /// </summary>
        [Tooltip("Fraction of sideways speed lost per second while rising slowly.")]
        public float airDrag = 0.35f;

        /// <summary>Air drag only applies while rising slower than this (near the top of the arc).</summary>
        [Tooltip("Air drag applies while rising slower than this, m/s.")]
        public float airDragMaxRiseSpeed = 6.5f;

        /// <summary>The fastest the player can fall.</summary>
        [Tooltip("Terminal fall speed, m/s.")]
        public float maxFallSpeed = 30f;

        /// <summary>The upward speed after defeating a Clipper in the air: a small, satisfying hop.</summary>
        [Tooltip("Upward speed of the rebound off a defeated Clipper, m/s.")]
        public float reboundSpeed = 9f;

        /// <summary>
        /// A Woofer whose push lifts a grounded player by less than this keeps them on the ground (a
        /// horizontal Woofer).
        /// </summary>
        [Tooltip("A Woofer pushing away from the ground slower than this leaves the player on the ground, m/s.")]
        public float groundedBounceLift = 1f;

        // ---- Wind-Up and Burst -----------------------------------------------------------------------------------

        /// <summary>The Wind-Up only starts when moving slower than this: it is a move from a standstill.</summary>
        [Tooltip("Maximum ground speed at which a Wind-Up can start, m/s.")]
        public float windUpMaxSpeed = 0.5f;

        /// <summary>How many taps of Jump fill the charge.</summary>
        [Tooltip("Taps of Jump for a full charge.")]
        public int windUpTaps = 6;

        /// <summary>
        /// The fraction of the charge that leaks away each second while holding. Slow: there is time to
        /// aim, not to wait.
        /// </summary>
        [Tooltip("Fraction of the charge lost per second while holding.")]
        public float windUpLeak = 0.25f;

        /// <summary>The Burst speed with no charge left: brisk, already above cruise.</summary>
        [Tooltip("Burst speed at zero charge, m/s.")]
        public float burstMinSpeed = 11f;

        /// <summary>
        /// The Burst speed at full charge: just under the speed cap, like letting go of a stretched rubber band.
        /// </summary>
        [Tooltip("Burst speed at full charge, m/s.")]
        public float burstMaxSpeed = 21f;

        // ---- Getting hit and coming back -------------------------------------------------------------------------

        /// <summary>The sideways speed of the knock-back, away from what hit the player.</summary>
        [Tooltip("Knock-back sideways speed, m/s.")]
        public float knockBackSpeed = 5f;

        /// <summary>The upward speed of the knock-back: a short arc, about a metre high.</summary>
        [Tooltip("Knock-back upward speed, m/s.")]
        public float knockBackLift = 8f;

        /// <summary>How long hazards do nothing after a hit, while the character blinks.</summary>
        [Tooltip("Seconds of invulnerability after a hit.")]
        public float invulnerableSeconds = 2f;

        /// <summary>
        /// The pause between being lost (a pit, or a hit with no Chimes) and reappearing; the time the fade has.
        /// </summary>
        [Tooltip("Seconds between being lost and reappearing.")]
        public float respawnDelay = 0.8f;

        /// <summary>A short grace after reappearing so a hazard next to the Tuning Fork cannot hit at once.</summary>
        [Tooltip("Seconds of invulnerability after reappearing.")]
        public float respawnInvulnerableSeconds = 1f;

        /// <summary>
        /// The player reappears this far above a Tuning Fork's respawn point and drops onto the ground.
        /// </summary>
        [Tooltip("Height above a Tuning Fork's respawn point at which the player reappears, m.")]
        public float respawnLift = 0.6f;

        // ---- Scattered Chimes ------------------------------------------------------------------------------------

        /// <summary>The launch speed of the slower half of the scattered Chimes.</summary>
        [Tooltip("Launch speed of the inner ring of scattered Chimes, m/s.")]
        public float scatterSlowSpeed = 6f;

        /// <summary>The launch speed of the faster half of the scattered Chimes.</summary>
        [Tooltip("Launch speed of the outer ring of scattered Chimes, m/s.")]
        public float scatterFastSpeed = 10f;

        /// <summary>How wide the fan of scattered Chimes opens, centred on straight up.</summary>
        [Tooltip("Width of the fan of scattered Chimes, degrees.")]
        public float scatterFanAngle = 140f;

        /// <summary>How much speed a scattered Chime keeps on each bounce.</summary>
        [Tooltip("Speed a scattered Chime keeps when it bounces (0..1).")]
        public float scatterBounce = 0.6f;

        /// <summary>How long scattered Chimes stay before they vanish.</summary>
        [Tooltip("Seconds a scattered Chime lives.")]
        public float scatterLifetime = 4f;

        /// <summary>For this long before vanishing a scattered Chime blinks: last chance.</summary>
        [Tooltip("Seconds of blinking before a scattered Chime vanishes.")]
        public float scatterBlinkSeconds = 1.5f;

        /// <summary>One full blink (shown, then hidden) of a scattered Chime.</summary>
        [Tooltip("Seconds per blink of a scattered Chime.")]
        public float scatterBlinkPeriod = 0.15f;

        /// <summary>Scattered Chimes cannot be picked up again for this long, so they visibly fly away first.</summary>
        [Tooltip("Seconds before a scattered Chime can be collected.")]
        public float scatterPickupDelay = 0.6f;

        /// <summary>The collision radius of a scattered Chime (it is 0.5 m tall).</summary>
        [Tooltip("Radius of a scattered Chime, m.")]
        public float scatterChimeRadius = 0.25f;

        // ---- Goal ------------------------------------------------------------------------------------------------

        /// <summary>After the Resonator the player brakes to a stop over about this distance.</summary>
        [Tooltip("Stopping distance after the goal, m.")]
        public float finishStopDistance = 15f;

        /// <summary>The gentlest braking used after the goal, so a slow arrival still stops promptly.</summary>
        [Tooltip("Minimum braking after the goal, m/s².")]
        public float finishMinBraking = 4f;

        // ---- Feedback --------------------------------------------------------------------------------------------

        /// <summary>No footsteps below this ground speed.</summary>
        [Tooltip("Minimum ground speed for footsteps, m/s.")]
        public float footstepMinSpeed = 0.5f;

        /// <summary>Landing at least this fast into the ground kicks up dust.</summary>
        [Tooltip("Impact speed from which a landing kicks up dust, m/s.")]
        public float dustImpactSpeed = 12f;

        // ---- Sensors (collision) ---------------------------------------------------------------------------------

        /// <summary>
        /// The longest distance moved between two collision checks. Smaller than the tucked radius, so nothing is
        /// tunnelled through at top speed.
        /// </summary>
        [Tooltip("Longest move between collision checks, m.")]
        public float maxStepDistance = 0.2f;

        /// <summary>
        /// How far the two floor sensors sit in front of and behind the centre. Wider = smoother over corners, but
        /// later to fall off ledges.
        /// </summary>
        [Tooltip("Distance of each floor sensor from the centre along the ground, m.")]
        public float footSpread = 0.3f;

        /// <summary>
        /// How far below the feet the ground may drop away between checks and still hold the player. Larger =
        /// stickier over crests.
        /// </summary>
        [Tooltip("How far below the feet the floor sensors reach while grounded, m.")]
        public float groundSnapDistance = 0.3f;

        /// <summary>
        /// The sharpest change of ground direction the player follows in one step; anything sharper is a
        /// wall or a ledge.
        /// </summary>
        [Tooltip("Sharpest corner the player runs around without stopping or leaving the ground, degrees.")]
        public float maxGroundTurnAngle = 50f;

        /// <summary>
        /// Ground ahead that is at least this much steeper than the ground underfoot stops the player like a wall.
        /// </summary>
        [Tooltip("Ground ahead this much steeper than underfoot is a wall, degrees.")]
        public float wallAngle = 60f;

        /// <summary>The steepest surface an airborne player can land on; steeper ones are slid along.</summary>
        [Tooltip("Steepest surface that can be landed on from the air, degrees.")]
        public float maxLandingAngle = 60f;

        /// <summary>How deep the player may already be inside a one-way platform and still be caught by it.</summary>
        [Tooltip("Depth inside a one-way platform within which it still catches the player, m.")]
        public float oneWayTolerance = 0.25f;

        /// <summary>The launch speed of a full jump, from <see cref="jumpHeight"/> and <see cref="gravity"/>.</summary>
        public float JumpSpeed => Mathf.Sqrt(2f * gravity * jumpHeight);

        /// <summary>The Burst speed for a charge of 0..1.</summary>
        public float BurstSpeed(float charge01) => Mathf.Lerp(burstMinSpeed, burstMaxSpeed, charge01);
    }
}
