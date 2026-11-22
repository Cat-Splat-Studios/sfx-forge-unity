using SfxShowcase.Core;
using UnityEngine;

namespace SfxShowcase.Gameplay
{
    /// <summary>
    /// One of the Chimes that fly out of the player when it is hit. It falls, bounces on the ground, can be collected
    /// again after a moment, blinks near the end of its life and then vanishes. These are short-lived objects made at
    /// runtime; the Chimes placed in the level belong to the level folder.
    /// </summary>
    public sealed class ScatteredChime : MonoBehaviour
    {
        ArtKitBase art;
        PlayerController player;
        PlayerTuning tuning;
        GroundSensor sensor;
        GameObject visual;
        TerrainPlane plane;
        Vector2 velocity;
        float age;

        /// <summary>
        /// Throws <paramref name="count"/> Chimes out of a point in a fan centred on straight up, alternating a faster
        /// and a slower ring so the spill reads as a burst and not as a line.
        /// </summary>
        public static void SpawnFan(
            ArtKitBase art,
            PlayerController player,
            Vector2 origin,
            int count,
            TerrainPlane plane
        )
        {
            PlayerTuning tuning = player.Tuning;
            var sharedSensor = new GroundSensor();
            for (int i = 0; i < count; i++)
            {
                float across = count > 1 ? i / (count - 1f) : 0.5f;
                float angle = (90f + (across - 0.5f) * tuning.scatterFanAngle) * Mathf.Deg2Rad;
                float speed = i % 2 == 0 ? tuning.scatterFastSpeed : tuning.scatterSlowSpeed;
                Vector2 launch = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

                var chimeObject = new GameObject("Scattered Chime");
                chimeObject.transform.position = origin;
                GameObject chimeVisual = art.CreateChime();
                chimeVisual.transform.SetParent(chimeObject.transform, false);

                var chime = chimeObject.AddComponent<ScatteredChime>();
                chime.art = art;
                chime.player = player;
                chime.tuning = tuning;
                chime.sensor = sharedSensor;
                chime.visual = chimeVisual;
                chime.plane = plane;
                chime.velocity = launch;
            }
        }

        void Update()
        {
            float deltaTime = Time.deltaTime;
            age += deltaTime;
            if (age >= tuning.scatterLifetime)
            {
                Destroy(gameObject);
                return;
            }

            Fly(deltaTime);
            Blink();
            if (age >= tuning.scatterPickupDelay && IsTouchingPlayer())
            {
                Collect();
            }
        }

        /// <summary>
        /// Falls under the player's gravity and bounces off whatever ground lies in the way of this frame's move.
        /// </summary>
        void Fly(float deltaTime)
        {
            velocity.y -= tuning.gravity * deltaTime;

            Vector2 position = transform.position;
            Vector2 move = velocity * deltaTime;
            float reach = move.magnitude + tuning.scatterChimeRadius;
            bool blocked =
                sensor.CastSolid(position, move.normalized, reach, plane, out SensorHit ground)
                && Vector2.Dot(velocity, ground.Normal) < 0f;
            if (blocked)
            {
                velocity = Vector2.Reflect(velocity, ground.Normal) * tuning.scatterBounce;
                move = velocity * deltaTime;
            }

            position += move;
            transform.position = new Vector3(position.x, position.y, transform.position.z);
        }

        /// <summary>Near the end of its life the Chime flickers: the last chance to grab it.</summary>
        void Blink()
        {
            bool blinking = age >= tuning.scatterLifetime - tuning.scatterBlinkSeconds;
            bool shown = !blinking || Mathf.Repeat(age, tuning.scatterBlinkPeriod) < tuning.scatterBlinkPeriod * 0.5f;
            if (visual.activeSelf != shown)
            {
                visual.SetActive(shown);
            }
        }

        bool IsTouchingPlayer()
        {
            if (!player.HasControl)
            {
                return false;
            }

            float touchDistance = tuning.standingRadius + tuning.scatterChimeRadius;
            Vector2 toPlayer = player.Position - (Vector2)transform.position;
            return toPlayer.sqrMagnitude <= touchDistance * touchDistance;
        }

        void Collect()
        {
            player.AddChimes(1);
            art.SpawnEffect(EffectKind.ChimePop, transform.position);
            Destroy(gameObject);
        }
    }
}
