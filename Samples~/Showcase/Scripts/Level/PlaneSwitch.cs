using SfxShowcase.Core;
using UnityEngine;

namespace SfxShowcase.Level
{
    /// <summary>
    /// A loop's switch. A loop's path crosses itself, so its right half is solid only on terrain plane A and its left
    /// half only on plane B; this trigger moves the player from one plane to the other, by the direction it is
    /// travelling in when it enters and again when it leaves. One stands at the top of every loop (leftwards = B, rightwards = A) and one on the
    /// ground on each side (always A, the plane a loop is entered on).
    /// </summary>
    public sealed class PlaneSwitch : MonoBehaviour
    {
        [Tooltip("The plane a player moving towards +x is put on.")]
        [SerializeField] TerrainPlane whenMovingRight = TerrainPlane.A;
        [Tooltip("The plane a player moving towards -x is put on.")]
        [SerializeField] TerrainPlane whenMovingLeft = TerrainPlane.A;

        /// <summary>Sets the plane for each direction of travel; called by the level's construction code.</summary>
        public void Configure(TerrainPlane planeWhenMovingRight, TerrainPlane planeWhenMovingLeft)
        {
            whenMovingRight = planeWhenMovingRight;
            whenMovingLeft = planeWhenMovingLeft;
        }

        void OnTriggerEnter2D(Collider2D other) => Apply(other);

        /// <summary>Decided again on the way out: a player who turned round inside the box leaves on the plane of the way it goes.</summary>
        void OnTriggerExit2D(Collider2D other) => Apply(other);

        void Apply(Collider2D other)
        {
            if (!other.TryGetComponent(out PlayerBase player)) return;
            player.SetTerrainPlane(player.Velocity.x < 0f ? whenMovingLeft : whenMovingRight);
        }
    }
}
