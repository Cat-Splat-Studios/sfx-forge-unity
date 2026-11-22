using SfxShowcase.Core;
using UnityEngine;

namespace SfxShowcase.Level
{
    /// <summary>
    /// A Woofer: the spring. Whatever the player was doing, touching it replaces the player's velocity with this
    /// Woofer's fixed launch velocity, so the flight that follows is always the same and the level can be laid out
    /// around it. The object is rotated so that its local +y points along the launch.
    /// </summary>
    public sealed class Woofer : MonoBehaviour
    {
        [Tooltip("The velocity the player leaves with, m/s in world space.")]
        [SerializeField]
        Vector2 launchVelocity = new Vector2(0f, 21f);

        [Tooltip("The cone that punches out when the Woofer fires.")]
        [SerializeField]
        PropVisual visual;

        /// <summary>The velocity the player leaves with, m/s in world space.</summary>
        public Vector2 LaunchVelocity => launchVelocity;

        /// <summary>Sets the launch and the visual; called by the level's construction code.</summary>
        public void Configure(Vector2 wooferLaunchVelocity, PropVisual wooferVisual)
        {
            launchVelocity = wooferLaunchVelocity;
            visual = wooferVisual;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.TryGetComponent(out PlayerBase player))
            {
                return;
            }

            player.Bounce(launchVelocity);
            visual.Activate();
            GameEvents.RaiseWooferBounced();
        }
    }
}
