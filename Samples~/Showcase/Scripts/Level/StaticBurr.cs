using SfxShowcase.Core;
using UnityEngine;

namespace SfxShowcase.Level
{
    /// <summary>
    /// Static: a burr of noise that hurts on contact, however the player touches it. It never moves and cannot be
    /// defeated. The player decides what a hit means (knock-back and scattered Chimes, or a respawn) and ignores it
    /// while invulnerable; staying inside is asked again every physics step, so the burr hurts as soon as that ends.
    /// </summary>
    public sealed class StaticBurr : MonoBehaviour
    {
        void OnTriggerEnter2D(Collider2D other) => Touch(other);

        void OnTriggerStay2D(Collider2D other) => Touch(other);

        void Touch(Collider2D other)
        {
            if (other.TryGetComponent(out PlayerBase player)) player.Hurt(transform.position);
        }
    }
}
