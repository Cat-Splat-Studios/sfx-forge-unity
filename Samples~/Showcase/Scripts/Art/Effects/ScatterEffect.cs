using UnityEngine;

namespace SfxShowcase.Art
{
    /// <summary>
    /// A burst of pieces (Clipper shards, a Burst's puff, dust): each flies off with its own velocity, falls under the
    /// burst's gravity, tumbles, shrinks away over the second half of its life, and then the whole burst removes
    /// itself. Pieces shrink rather than fade, so everything stays solid, flat colour.
    /// </summary>
    public sealed class ScatterEffect : MonoBehaviour
    {
        Transform[] pieces;
        Vector2[] velocities;
        float gravity;
        float spin;
        float seconds;
        float age;

        /// <summary>Starts the burst. Called by <see cref="EffectArt"/> right after the pieces are made.</summary>
        /// <param name="flyingPieces">The pieces, children of this object.</param>
        /// <param name="startVelocities">The velocity of each piece, m/s.</param>
        /// <param name="downwardGravity">Downward acceleration, m/s².</param>
        /// <param name="spinDegreesPerSecond">How fast each piece tumbles; alternate pieces turn the other way.</param>
        /// <param name="lifeSeconds">How long the burst lasts.</param>
        public void Play(
            Transform[] flyingPieces,
            Vector2[] startVelocities,
            float downwardGravity,
            float spinDegreesPerSecond,
            float lifeSeconds
        )
        {
            pieces = flyingPieces;
            velocities = startVelocities;
            gravity = downwardGravity;
            spin = spinDegreesPerSecond;
            seconds = lifeSeconds;
        }

        void Update()
        {
            float deltaTime = Time.deltaTime;
            age += deltaTime;
            if (pieces == null || age >= seconds)
            {
                Destroy(gameObject);
                return;
            }

            float size = Mathf.Clamp01(2f * (1f - age / seconds));
            for (int i = 0; i < pieces.Length; i++)
            {
                velocities[i] += Vector2.down * (gravity * deltaTime);
                pieces[i].localPosition += (Vector3)(velocities[i] * deltaTime);
                pieces[i].Rotate(0f, 0f, (i % 2 == 0 ? spin : -spin) * deltaTime);
                pieces[i].localScale = Vector3.one * size;
            }
        }
    }
}
