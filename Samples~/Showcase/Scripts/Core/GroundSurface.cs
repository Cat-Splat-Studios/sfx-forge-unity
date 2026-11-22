using UnityEngine;

namespace SfxShowcase.Core
{
    /// <summary>
    /// Marks a 2D collider as ground the player runs on. The contract between the level and the movement code:
    /// ground is every NON-trigger Collider2D that carries this component (an EdgeCollider2D along the surface, on the
    /// same GameObject); everything else in the level is a trigger. No Unity layers or tags are used.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GroundSurface : MonoBehaviour
    {
        [Tooltip("The collision plane this ground is solid on. Both for everything except the two halves of a loop.")]
        [SerializeField]
        TerrainPlane plane = TerrainPlane.Both;

        [Tooltip("A floating platform: solid only from above, the player passes through it from below.")]
        [SerializeField]
        bool oneWay;

        /// <summary>The collision plane this ground is solid on.</summary>
        public TerrainPlane Plane => plane;

        /// <summary>Solid only from above (a floating platform).</summary>
        public bool OneWay => oneWay;

        /// <summary>Sets what kind of ground this is; called by the level's construction code.</summary>
        public void Configure(TerrainPlane surfacePlane, bool surfaceOneWay)
        {
            plane = surfacePlane;
            oneWay = surfaceOneWay;
        }

        /// <summary>Whether a player on the given plane collides with this ground.</summary>
        public bool IsSolidFor(TerrainPlane playerPlane) => plane == TerrainPlane.Both || plane == playerPlane;
    }
}
