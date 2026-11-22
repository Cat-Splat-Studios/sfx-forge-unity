using SfxShowcase.Core;
using UnityEngine;

namespace SfxShowcase.Level
{
    /// <summary>
    /// A Chime placed in the level: the collectible. Touching it gives the player one Chime (the player raises
    /// <see cref="GameEvents.ChimeCollected"/> itself, so the level never counts a combo), hides it and asks the art
    /// kit for the small ring. It comes back when a new run starts. The Chimes scattered by a hit are not these: they
    /// belong to the gameplay folder.
    /// </summary>
    public sealed class Chime : MonoBehaviour
    {
        [Tooltip("The art kit asset, asked for the collect effect.")]
        [SerializeField]
        ArtKitBase art;

        [Tooltip("The Chime's visual, hidden once collected.")]
        [SerializeField]
        GameObject visual;

        [Tooltip("The trigger the player touches, switched off once collected.")]
        [SerializeField]
        Collider2D pickupTrigger;

        bool collected;

        /// <summary>Whether this Chime has been collected in the current run.</summary>
        public bool IsCollected => collected;

        /// <summary>Wires the Chime; called by the level's construction code.</summary>
        public void Configure(ArtKitBase artKit, GameObject chimeVisual, Collider2D trigger)
        {
            art = artKit;
            visual = chimeVisual;
            pickupTrigger = trigger;
        }

        void OnEnable() => GameEvents.GameStarted += Restore;

        void OnDisable() => GameEvents.GameStarted -= Restore;

        void OnTriggerEnter2D(Collider2D other)
        {
            if (collected || !other.TryGetComponent(out PlayerBase player))
            {
                return;
            }

            SetCollected(true);
            player.AddChimes(1);
            art.SpawnEffect(EffectKind.ChimePop, transform.position);
        }

        void Restore() => SetCollected(false);

        void SetCollected(bool isCollected)
        {
            collected = isCollected;
            visual.SetActive(!isCollected);
            pickupTrigger.enabled = !isCollected;
        }
    }
}
