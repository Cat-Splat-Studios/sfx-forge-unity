using UnityEngine;

namespace SfxShowcase.Core
{
    /// <summary>
    /// The player as the other folders see it. Unity cannot serialize a reference to an interface, so components that
    /// need the player hold a serialized <see cref="PlayerBase"/> (wired by the scene builder); the gameplay folder's
    /// player component derives from this.
    /// It sits on the GameObject that carries the player's kinematic Rigidbody2D and its trigger CircleCollider2D, so a
    /// level piece finds it in OnTriggerEnter2D with <c>other.TryGetComponent(out PlayerBase player)</c>.
    /// </summary>
    public abstract class PlayerBase : MonoBehaviour, IPlayerState, IPlayerActor
    {
        /// <summary>When set, this replaces the keyboard and gamepad (tests, attract mode); null gives them back.</summary>
        public IPlayerInput InputOverride { get; set; }

        /// <inheritdoc/>
        public abstract Vector2 Position { get; }
        /// <inheritdoc/>
        public abstract Vector2 Velocity { get; }
        /// <inheritdoc/>
        public abstract float GroundSpeed { get; }
        /// <inheritdoc/>
        public abstract float TopSpeed { get; }
        /// <inheritdoc/>
        public abstract bool IsGrounded { get; }
        /// <inheritdoc/>
        public abstract float GroundAngle { get; }
        /// <inheritdoc/>
        public abstract bool IsRolling { get; }
        /// <inheritdoc/>
        public abstract bool IsCharging { get; }
        /// <inheritdoc/>
        public abstract float Charge01 { get; }
        /// <inheritdoc/>
        public abstract int Facing { get; }
        /// <inheritdoc/>
        public abstract bool IsAttacking { get; }
        /// <inheritdoc/>
        public abstract bool IsInvulnerable { get; }
        /// <inheritdoc/>
        public abstract bool HasControl { get; }
        /// <inheritdoc/>
        public abstract TerrainPlane Plane { get; }

        /// <inheritdoc/>
        public float Speed => Mathf.Min(IsGrounded ? Mathf.Abs(GroundSpeed) : Mathf.Abs(Velocity.x), TopSpeed);
        /// <inheritdoc/>
        public float Speed01 => TopSpeed > 0f ? Mathf.Clamp01(Speed / TopSpeed) : 0f;

        /// <inheritdoc/>
        public abstract void Bounce(Vector2 launchVelocity);
        /// <inheritdoc/>
        public abstract bool Hurt(Vector2 sourcePosition);
        /// <inheritdoc/>
        public abstract void Rebound();
        /// <inheritdoc/>
        public abstract void AddChimes(int count);
        /// <inheritdoc/>
        public abstract void SetCheckpoint(int index, Vector2 respawnPosition);
        /// <inheritdoc/>
        public abstract void Finish();
        /// <inheritdoc/>
        public abstract void SetTerrainPlane(TerrainPlane plane);

        /// <summary>Puts the player at a world position, at rest and airborne, on the default terrain plane (tests, debugging).</summary>
        public abstract void PlaceAt(Vector2 position);
    }
}
