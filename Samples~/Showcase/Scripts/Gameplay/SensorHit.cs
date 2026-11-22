using SfxShowcase.Core;
using UnityEngine;

namespace SfxShowcase.Gameplay
{
    /// <summary>What a <see cref="GroundSensor"/> ray found: the nearest piece of ground along it.</summary>
    public readonly struct SensorHit
    {
        /// <summary>Where the ray met the ground, world space.</summary>
        public readonly Vector2 Point;

        /// <summary>The ground's normal at that point, facing the side the ray came from.</summary>
        public readonly Vector2 Normal;

        /// <summary>The distance from the ray's origin to the point, metres.</summary>
        public readonly float Distance;

        /// <summary>The ground that was hit.</summary>
        public readonly GroundSurface Surface;

        /// <summary>Creates the description of a hit.</summary>
        public SensorHit(Vector2 point, Vector2 normal, float distance, GroundSurface surface)
        {
            Point = point;
            Normal = normal;
            Distance = distance;
            Surface = surface;
        }
    }
}
