using SfxShowcase.Core;
using UnityEngine;

namespace SfxShowcase.Gameplay
{
    /// <summary>
    /// The one place that asks the physics engine "where is the ground?". A sensor is a ray; this class casts it
    /// and keeps only what the level contract calls ground: a non-trigger Collider2D that carries a
    /// <see cref="GroundSurface"/> which is solid on the asker's <see cref="TerrainPlane"/>. No layers and no
    /// tags are involved.
    /// </summary>
    public sealed class GroundSensor
    {
        /// <summary>
        /// The most colliders one ray is expected to cross (a loop's two halves, the ground under it, a platform …).
        /// </summary>
        const int MaximumHitsPerRay = 8;

        /// <summary>
        /// A one-way platform only counts when the ray meets a face pointing at least this much upward.
        /// </summary>
        const float OneWayMinimumNormalY = 0.5f;

        readonly RaycastHit2D[] hits = new RaycastHit2D[MaximumHitsPerRay];
        readonly ContactFilter2D groundFilter;

        /// <summary>
        /// Creates a sensor that ignores trigger colliders (everything in the level that is not ground is a trigger).
        /// </summary>
        public GroundSensor()
        {
            groundFilter = new ContactFilter2D { useTriggers = false };
        }

        /// <summary>
        /// A wall or ceiling sensor: the nearest ground along the ray, ignoring one-way platforms (they never block
        /// from the side or from below).
        /// </summary>
        public bool CastSolid(
            Vector2 origin,
            Vector2 direction,
            float length,
            TerrainPlane plane,
            out SensorHit nearest
        )
        {
            return Cast(origin, direction, length, plane, null, false, 0f, out nearest);
        }

        /// <summary>
        /// A floor sensor: the nearest ground along the ray, including one-way platforms met from above. A one-way
        /// platform nearer than <paramref name="oneWayMinimumDistance"/> is ignored: the asker is already passing
        /// through it. The surface in <paramref name="standingOn"/> (may be null) stays solid whatever its plane, so
        /// switching plane at the top of a loop never drops the player off the half it is still standing on.
        /// </summary>
        public bool CastFloor(
            Vector2 origin,
            Vector2 direction,
            float length,
            TerrainPlane plane,
            GroundSurface standingOn,
            float oneWayMinimumDistance,
            out SensorHit nearest
        )
        {
            return Cast(origin, direction, length, plane, standingOn, true, oneWayMinimumDistance, out nearest);
        }

        bool Cast(
            Vector2 origin,
            Vector2 direction,
            float length,
            TerrainPlane plane,
            GroundSurface standingOn,
            bool includeOneWay,
            float oneWayMinimumDistance,
            out SensorHit nearest
        )
        {
            nearest = default;
            bool found = false;
            int count = Physics2D.Raycast(origin, direction, groundFilter, hits, length);
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = hits[i];
                if (!hit.collider.TryGetComponent(out GroundSurface surface))
                {
                    continue;
                }

                if (!surface.IsSolidFor(plane) && surface != standingOn)
                {
                    continue;
                }

                if (surface.OneWay && !IsOneWayHitSolid(hit, includeOneWay, oneWayMinimumDistance))
                {
                    continue;
                }

                if (found && hit.distance >= nearest.Distance)
                {
                    continue;
                }

                nearest = new SensorHit(hit.point, hit.normal, hit.distance, surface);
                found = true;
            }

            return found;
        }

        static bool IsOneWayHitSolid(RaycastHit2D hit, bool includeOneWay, float oneWayMinimumDistance)
        {
            bool metFromAbove = hit.normal.y > OneWayMinimumNormalY;
            return includeOneWay && metFromAbove && hit.distance >= oneWayMinimumDistance;
        }
    }
}
