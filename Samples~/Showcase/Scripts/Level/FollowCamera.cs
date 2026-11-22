using SfxShowcase.Core;
using UnityEngine;

namespace SfxShowcase.Level
{
    /// <summary>
    /// The game camera: an orthographic side view that follows the player. It looks ahead in the direction of travel,
    /// further the faster the player moves, so there is always more screen in front than behind; the vertical follow is
    /// damped so jumps and loops do not shake the view; and it never shows anything outside the level's bounds. It does
    /// not rotate, even in a loop.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class FollowCamera : MonoBehaviour
    {
        [Tooltip("The player to follow.")]
        [SerializeField]
        PlayerBase target;

        [Tooltip("The level, for the bounds the view stays inside.")]
        [SerializeField]
        LevelDescriptor level;

        [Tooltip("The camera this component moves.")]
        [SerializeField]
        Camera viewCamera;

        [Tooltip("Half the height of the view, metres.")]
        [SerializeField]
        float halfHeight = 9f;

        [Tooltip("How far ahead of the player the view is centred at top speed, metres.")]
        [SerializeField]
        float maxLookAhead = 6f;

        [Tooltip("Seconds the look-ahead takes to follow a change of speed or direction.")]
        [SerializeField]
        float lookAheadSmoothTime = 0.5f;

        [Tooltip("Seconds the view takes to follow the player up and down.")]
        [SerializeField]
        float verticalSmoothTime = 0.25f;

        [Tooltip("How far above the player the view is centred, metres: the ground is below, the interest above.")]
        [SerializeField]
        float verticalOffset = 1.5f;

        float lookAhead;
        float lookAheadVelocity;
        float verticalVelocity;
        bool snapNextFrame = true;

        /// <summary>
        /// Wires the camera to the player and the level and makes it orthographic at the view's height; called by the
        /// level's construction code.
        /// </summary>
        public void Configure(PlayerBase followTarget, LevelDescriptor levelDescriptor, Camera movedCamera)
        {
            target = followTarget;
            level = levelDescriptor;
            viewCamera = movedCamera;
            viewCamera.orthographic = true;
            viewCamera.orthographicSize = halfHeight;
        }

        /// <summary>
        /// Where the view is centred for a player standing still at a position, inside the level's bounds.
        /// </summary>
        public Vector2 RestingViewCentre(Vector2 playerPosition)
        {
            return ClampToBounds(new Vector2(playerPosition.x, playerPosition.y + verticalOffset));
        }

        void OnEnable()
        {
            GameEvents.GameStarted += Snap;
            GameEvents.Respawned += SnapAfterRespawn;
        }

        void OnDisable()
        {
            GameEvents.GameStarted -= Snap;
            GameEvents.Respawned -= SnapAfterRespawn;
        }

        void LateUpdate()
        {
            Vector2 position = target.Position;
            float wantedLookAhead = Mathf.Clamp(target.Velocity.x / target.TopSpeed, -1f, 1f) * maxLookAhead;
            float wantedY = position.y + verticalOffset;
            float y;
            if (snapNextFrame)
            {
                snapNextFrame = false;
                lookAhead = wantedLookAhead;
                lookAheadVelocity = 0f;
                verticalVelocity = 0f;
                y = wantedY;
            }
            else
            {
                lookAhead = Mathf.SmoothDamp(lookAhead, wantedLookAhead, ref lookAheadVelocity, lookAheadSmoothTime);
                y = Mathf.SmoothDamp(transform.position.y, wantedY, ref verticalVelocity, verticalSmoothTime);
            }

            Vector2 centre = ClampToBounds(new Vector2(position.x + lookAhead, y));
            transform.position = new Vector3(centre.x, centre.y, transform.position.z);
        }

        void Snap() => snapNextFrame = true;

        void SnapAfterRespawn(SectionId section) => Snap();

        Vector2 ClampToBounds(Vector2 centre)
        {
            Rect bounds = level.Bounds;
            float halfWidth = halfHeight * viewCamera.aspect;

            // A level narrower or lower than the view is centred instead of clamped.
            float x =
                bounds.width > 2f * halfWidth
                    ? Mathf.Clamp(centre.x, bounds.xMin + halfWidth, bounds.xMax - halfWidth)
                    : bounds.center.x;
            float y =
                bounds.height > 2f * halfHeight
                    ? Mathf.Clamp(centre.y, bounds.yMin + halfHeight, bounds.yMax - halfHeight)
                    : bounds.center.y;
            return new Vector2(x, y);
        }
    }
}
