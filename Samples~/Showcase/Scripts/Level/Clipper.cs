using SfxShowcase.Core;
using UnityEngine;

namespace SfxShowcase.Level
{
    /// <summary>
    /// A Clipper: the walking enemy. It patrols a short flat stretch around the point it was placed at. Touching it
    /// while tucked (rolling, or in the air after a jump) pops it; touching it any other way hurts the player. A popped
    /// Clipper stays gone until a new run starts.
    /// </summary>
    public sealed class Clipper : MonoBehaviour
    {
        [Tooltip("The art kit asset, asked for the shards when the Clipper is popped.")]
        [SerializeField]
        ArtKitBase art;

        [Tooltip("The Clipper's visual (facing its local +x); mirrored to face the way it walks, hidden when popped.")]
        [SerializeField]
        Transform visual;

        [Tooltip("The trigger the player touches, switched off when popped.")]
        [SerializeField]
        Collider2D bodyTrigger;

        [Tooltip("How far it walks to each side of where it was placed, metres.")]
        [SerializeField]
        float patrolHalfRange = 3f;

        [Tooltip("Walking speed, m/s.")]
        [SerializeField]
        float walkSpeed = 1.5f;

        [Tooltip("Where the shards appear, metres above the feet.")]
        [SerializeField]
        float burstHeight = 0.6f;

        Vector3 home;
        float offset;
        int direction = 1;
        bool defeated;

        /// <summary>Whether this Clipper has been popped in the current run.</summary>
        public bool IsDefeated => defeated;

        /// <summary>Wires the Clipper and sets its patrol; called by the level's construction code.</summary>
        public void Configure(ArtKitBase artKit, Transform clipperVisual, Collider2D trigger, float halfRange)
        {
            art = artKit;
            visual = clipperVisual;
            bodyTrigger = trigger;
            patrolHalfRange = halfRange;
        }

        void Awake() => home = transform.position;

        void OnEnable() => GameEvents.GameStarted += Restore;

        void OnDisable() => GameEvents.GameStarted -= Restore;

        void Update()
        {
            if (defeated)
            {
                return;
            }

            offset += direction * walkSpeed * Time.deltaTime;
            if (Mathf.Abs(offset) >= patrolHalfRange)
            {
                offset = Mathf.Clamp(offset, -patrolHalfRange, patrolHalfRange);
                direction = -direction;
            }

            transform.position = home + Vector3.right * offset;
            visual.localScale = new Vector3(direction, 1f, 1f);
        }

        void OnTriggerEnter2D(Collider2D other) => Touch(other);

        void OnTriggerStay2D(Collider2D other) => Touch(other);

        void Touch(Collider2D other)
        {
            if (defeated || !other.TryGetComponent(out PlayerBase player))
            {
                return;
            }

            if (!player.IsAttacking)
            {
                player.Hurt(transform.position);
                return;
            }

            SetDefeated(true);
            art.SpawnEffect(EffectKind.ClipperShards, (Vector2)transform.position + Vector2.up * burstHeight);
            player.Rebound();
            GameEvents.RaiseClipperDefeated();
        }

        void Restore()
        {
            offset = 0f;
            direction = 1;
            SetDefeated(false);
        }

        void SetDefeated(bool isDefeated)
        {
            defeated = isDefeated;
            visual.gameObject.SetActive(!isDefeated);
            bodyTrigger.enabled = !isDefeated;
        }
    }
}
