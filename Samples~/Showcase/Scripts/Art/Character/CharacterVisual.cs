using SfxShowcase.Core;
using UnityEngine;

namespace SfxShowcase.Art
{
    /// <summary>
    /// Brio's animation. It only READS the player (<see cref="IPlayerState"/>) and only moves transforms – "squash and
    /// stretch is the animation system". What it shows:
    /// the body follows the slope and squashes on landing; the crest leans back with speed; the feet cycle with ground
    /// speed and blur into a ring near top speed; rolling or winding up swaps to the striped ball, which spins at the
    /// rate its speed implies; a Wind-Up stretches the ball and swells a halo; a hit flattens the eyes and blinks.
    /// </summary>
    public sealed class CharacterVisual : MonoBehaviour
    {
        [Header("Wiring (set by CharacterArt)")]
        [SerializeField, Tooltip("The player whose state is shown.")]
        PlayerBase player;

        [SerializeField, Tooltip("Everything that tilts with the ground, squashes and blinks.")]
        Transform pose;

        [SerializeField, Tooltip("The standing look: body, crest, eyes, feet. Mirrored to face left.")]
        Transform standing;

        [SerializeField, Tooltip("The tucked look: the striped ball and the charge halo.")]
        Transform tucked;

        [SerializeField]
        Transform crest;

        [SerializeField]
        Transform eyes;

        [SerializeField]
        Transform backFoot;

        [SerializeField]
        Transform frontFoot;

        [SerializeField]
        Transform footBlur;

        [SerializeField]
        Transform ball;

        [SerializeField]
        Transform halo;

        [Header("Body")]
        [SerializeField, Tooltip("How quickly the body turns to the slope under it, per second.")]
        float tiltSharpness = 20f;

        [SerializeField, Tooltip("Landing speed (m/s) that gives the full squash.")]
        float fullSquashSpeed = 20f;

        [SerializeField, Tooltip("The full squash: how much shorter and wider the body gets (0.3 = 30 %).")]
        float fullSquash = 0.3f;

        [SerializeField, Tooltip("Spring stiffness that pulls the squash back out.")]
        float squashStiffness = 320f;

        [SerializeField, Tooltip("Spring damping: lower wobbles longer.")]
        float squashDamping = 16f;

        [Header("Crest and eyes")]
        [SerializeField, Tooltip("Crest lean at top speed, degrees backwards.")]
        float crestLeanAtTopSpeed = 50f;

        [SerializeField, Tooltip("How quickly the crest follows the speed, per second.")]
        float crestSharpness = 8f;

        [SerializeField, Tooltip("Eye height while hurt, as a fraction: the eyes squash to flat lines.")]
        float hurtEyeHeight = 0.15f;

        [SerializeField, Tooltip("Blinks per second while invulnerable after a hit.")]
        float blinkRate = 12f;

        [Header("Feet")]
        [SerializeField, Tooltip("Centre of the ellipse the feet travel round, relative to the body's centre.")]
        Vector2 footOrbitCenter = new Vector2(0f, -0.44f);

        [SerializeField, Tooltip("Radii of the ellipse the feet travel round.")]
        Vector2 footOrbitRadius = new Vector2(0.22f, 0.09f);

        [SerializeField, Tooltip("Above this fraction of top speed the feet blur into one ring.")]
        float footBlurSpeed01 = 0.9f;

        [SerializeField, Tooltip("Below this ground speed (m/s) the feet return to standing.")]
        float standingSpeed = 0.3f;

        [Header("Tucked")]
        [SerializeField, Tooltip("Size of the ball relative to the standing body (tucked radius ÷ standing radius).")]
        float tuckedScale = DesignTargets.TuckedRadius / DesignTargets.StandingRadius;

        [SerializeField, Tooltip("The slowest the ball spins in the air, as if rolling at this speed (m/s).")]
        float slowestAirSpin = 8f;

        [SerializeField, Tooltip("Ball spin while winding up, degrees per second at no charge and at full charge.")]
        Vector2 windUpSpin = new Vector2(720f, 2160f);

        [SerializeField, Tooltip("How much wider the ball stretches at full charge (0.2 = 20 %).")]
        float windUpStretch = 0.2f;

        [SerializeField, Tooltip("Halo size at no charge and at full charge.")]
        Vector2 haloScale = new Vector2(0.9f, 1.5f);

        const float LongestStep = 1f / 30f;

        float tilt;
        float squash;
        float squashVelocity;
        float crestLean;
        float footPhase;
        float ballAngle;
        float fallSpeed;
        bool wasGrounded = true;

        /// <summary>Wires the player and the main groups. Called by <see cref="CharacterArt"/>.</summary>
        public void Configure(
            PlayerBase shownPlayer,
            Transform poseGroup,
            Transform standingGroup,
            Transform tuckedGroup
        )
        {
            player = shownPlayer;
            pose = poseGroup;
            standing = standingGroup;
            tucked = tuckedGroup;
            tucked.gameObject.SetActive(false);
        }

        /// <summary>Wires the parts of the standing look. Called by <see cref="CharacterArt"/>.</summary>
        public void ConfigureStanding(
            Transform crestPart,
            Transform eyesPart,
            Transform backFootPart,
            Transform frontFootPart,
            Transform footBlurPart
        )
        {
            crest = crestPart;
            eyes = eyesPart;
            backFoot = backFootPart;
            frontFoot = frontFootPart;
            footBlur = footBlurPart;
            footBlur.localPosition = footOrbitCenter;
            footBlur.gameObject.SetActive(false);
            PlaceFeet();
        }

        /// <summary>Wires the parts of the tucked look. Called by <see cref="CharacterArt"/>.</summary>
        public void ConfigureTucked(Transform ballPart, Transform haloPart)
        {
            ball = ballPart;
            halo = haloPart;
            halo.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            float deltaTime = Mathf.Min(Time.deltaTime, LongestStep);
            bool isTucked = player.IsRolling || player.IsCharging;

            SetActive(standing, !isTucked);
            SetActive(tucked, isTucked);
            UpdateBody(deltaTime, isTucked);
            if (isTucked)
            {
                UpdateTucked(deltaTime);
            }
            else
            {
                UpdateStanding(deltaTime);
            }

            bool blinkedOut = player.IsInvulnerable && Mathf.FloorToInt(Time.time * blinkRate) % 2 == 0;
            SetActive(pose, !blinkedOut);
        }

        /// <summary>
        /// Tilt to the slope, and the landing squash: a damped spring, so the body bounces back past round.
        /// </summary>
        void UpdateBody(float deltaTime, bool isTucked)
        {
            bool grounded = player.IsGrounded;
            if (grounded && !wasGrounded)
            {
                squash = fullSquash * Mathf.Clamp01(fallSpeed / fullSquashSpeed);
            }

            if (!grounded)
            {
                fallSpeed = Mathf.Max(0f, -player.Velocity.y);
            }

            wasGrounded = grounded;

            squashVelocity += (-squashStiffness * squash - squashDamping * squashVelocity) * deltaTime;
            squash += squashVelocity * deltaTime;

            // A ball has no "up", so only the standing body leans into the slope.
            float targetTilt = isTucked ? 0f : player.GroundAngle;
            tilt = Mathf.LerpAngle(tilt, targetTilt, 1f - Mathf.Exp(-tiltSharpness * deltaTime));
            pose.localRotation = Quaternion.Euler(0f, 0f, tilt);
            pose.localScale = new Vector3(1f + squash, 1f - squash, 1f);

            // Squashing about the centre would lift the feet; slide down so the bottom stays on the ground.
            pose.localPosition = pose.localRotation * new Vector3(0f, -squash * DesignTargets.StandingRadius, 0f);
        }

        void UpdateStanding(float deltaTime)
        {
            standing.localScale = new Vector3(player.Facing < 0 ? -1f : 1f, 1f, 1f);

            float targetLean = crestLeanAtTopSpeed * player.Speed01;
            crestLean = Mathf.Lerp(crestLean, targetLean, 1f - Mathf.Exp(-crestSharpness * deltaTime));
            crest.localRotation = Quaternion.Euler(0f, 0f, crestLean);

            eyes.localScale = new Vector3(1f, player.IsInvulnerable ? hurtEyeHeight : 1f, 1f);

            float groundSpeed = Mathf.Abs(player.GroundSpeed);

            // Half a cycle (one foot coming down) per stride: the same stride the Footstep event is counted by.
            float stride = DesignTargets.StrideLength(player.Speed01);
            if (groundSpeed > standingSpeed)
            {
                footPhase = Mathf.Repeat(footPhase + 180f * groundSpeed / stride * deltaTime, 360f);
            }
            else if (player.IsGrounded)
            {
                footPhase = Mathf.MoveTowardsAngle(footPhase, 0f, 720f * deltaTime);
            }

            bool blurred = player.IsGrounded && player.Speed01 >= footBlurSpeed01;
            SetActive(footBlur, blurred);
            SetActive(backFoot, !blurred);
            SetActive(frontFoot, !blurred);
            PlaceFeet();
        }

        void UpdateTucked(float deltaTime)
        {
            float charge = player.Charge01;
            bool charging = player.IsCharging;
            float facing = player.Facing < 0 ? -1f : 1f;

            float spin;
            if (charging)
            {
                spin = facing * Mathf.Lerp(windUpSpin.x, windUpSpin.y, charge);
            }
            else
            {
                // Rolling without slipping: angular speed = speed ÷ radius. In the air, keep at least a lively spin.
                float rollSpeed = player.IsGrounded
                    ? player.GroundSpeed
                    : facing * Mathf.Max(player.Speed, slowestAirSpin);
                spin = rollSpeed / DesignTargets.TuckedRadius * Mathf.Rad2Deg;
            }

            // Moving towards +x turns the ball clockwise, which is a negative angle.
            ballAngle = Mathf.Repeat(ballAngle - spin * deltaTime, 360f);
            ball.localRotation = Quaternion.Euler(0f, 0f, ballAngle);

            float stretch = charging ? windUpStretch * charge : 0f;
            tucked.localScale = new Vector3(tuckedScale * (1f + stretch), tuckedScale * (1f - stretch * 0.5f), 1f);

            SetActive(halo, charging);
            if (charging)
            {
                halo.localScale = Vector3.one * Mathf.Lerp(haloScale.x, haloScale.y, charge);
            }
        }

        /// <summary>The two feet, half a cycle apart, on their ellipse. Phase 0 is the standing pose.</summary>
        void PlaceFeet()
        {
            frontFoot.localPosition = FootPosition(footPhase);
            backFoot.localPosition = FootPosition(footPhase + 180f);
        }

        Vector2 FootPosition(float phaseDegrees)
        {
            // Clockwise, so a foot moves backwards along the bottom of the ellipse, where it is on the ground.
            Vector2 around = MeshBuilder.Direction(-phaseDegrees);
            return footOrbitCenter + new Vector2(around.x * footOrbitRadius.x, around.y * footOrbitRadius.y);
        }

        static void SetActive(Transform part, bool active)
        {
            if (part.gameObject.activeSelf != active)
            {
                part.gameObject.SetActive(active);
            }
        }
    }
}
