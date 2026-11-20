using System.Collections.Generic;
using SfxShowcase.Core;
using UnityEngine;

namespace SfxShowcase.Art
{
    /// <summary>
    /// Brio's speed trail: above 70 % of top speed a tapered Brio Yellow ribbon follows the path Brio took and fades to
    /// nothing over about two metres. It is a strip mesh rebuilt every frame from the last few positions – no texture,
    /// no particle system. Sits on the object <see cref="CharacterArt"/> makes for it (transparent material).
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    public sealed class CharacterTrail : MonoBehaviour
    {
        [SerializeField, Tooltip("The player the trail follows.")]
        PlayerBase player;
        [SerializeField, Tooltip("The trail appears above this fraction of top speed.")]
        float appearsAboveSpeed01 = 0.7f;
        [SerializeField, Tooltip("Length of the trail at full strength, metres.")]
        float length = 2f;
        [SerializeField, Tooltip("Width of the ribbon at Brio and at its tail, metres.")]
        Vector2 width = new Vector2(0.7f, 0.1f);
        [SerializeField, Tooltip("Opacity of the ribbon at Brio; it fades to 0 at the tail.")]
        float headAlpha = 0.7f;
        [SerializeField, Tooltip("How fast the trail grows in and shrinks away, per second.")]
        float fadeRate = 3f;
        [SerializeField, Tooltip("Distance between remembered positions, metres.")]
        float pointSpacing = 0.15f;
        [SerializeField, Tooltip("A jump in position longer than this (a respawn) starts a fresh trail, metres.")]
        float teleportDistance = 3f;

        /// <summary>Remembered world positions, newest first; the first one is always where Brio is now.</summary>
        readonly List<Vector2> points = new List<Vector2>();
        readonly MeshBuilder builder = new MeshBuilder();
        Mesh mesh;
        float strength;

        /// <summary>Sets the player to follow. Called by <see cref="CharacterArt"/>.</summary>
        public void Configure(PlayerBase followedPlayer)
        {
            player = followedPlayer;
        }

        void Awake()
        {
            mesh = new Mesh { name = "Brio Trail" };
            mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        void OnDestroy()
        {
            Destroy(mesh);
        }

        void LateUpdate()
        {
            float target = player.Speed01 >= appearsAboveSpeed01 ? 1f : 0f;
            strength = Mathf.MoveTowards(strength, target, fadeRate * Time.deltaTime);
            if (strength <= 0f)
            {
                if (points.Count > 0)
                {
                    points.Clear();
                    mesh.Clear();
                }
                return;
            }
            Remember(player.Position);
            Rebuild();
        }

        void Remember(Vector2 position)
        {
            if (points.Count > 0 && (position - points[0]).magnitude > teleportDistance) points.Clear();
            if (points.Count < 2)
            {
                points.Clear();
                points.Add(position);
                points.Add(position);
                return;
            }
            points[0] = position;
            if ((points[0] - points[1]).magnitude >= pointSpacing) points.Insert(0, position);
        }

        /// <summary>Draws the ribbon through the remembered points, thinner and fainter towards the tail, and forgets what lies beyond its length.</summary>
        void Rebuild()
        {
            float visibleLength = length * strength;
            builder.Clear();
            float travelled = 0f;
            Vector2 previousLeft = Vector2.zero, previousRight = Vector2.zero;
            Color previousColor = default;
            int kept = points.Count;
            for (int i = 0; i < points.Count; i++)
            {
                if (i > 0) travelled += (points[i] - points[i - 1]).magnitude;
                float along = Mathf.Clamp01(travelled / visibleLength);
                Vector2 newer = points[Mathf.Max(i - 1, 0)];
                Vector2 older = points[Mathf.Min(i + 1, points.Count - 1)];
                Vector2 direction = newer - older;
                direction = direction.sqrMagnitude > 1e-8f ? direction.normalized : Vector2.right;
                Vector2 side = new Vector2(-direction.y, direction.x) * (Mathf.Lerp(width.x, width.y, along) * 0.5f);
                // The points are world positions; the mesh lives in this object's space, which travels with Brio.
                Vector2 left = transform.InverseTransformPoint(points[i] + side);
                Vector2 right = transform.InverseTransformPoint(points[i] - side);
                Color color = Palette.WithAlpha(Palette.BrioYellow, headAlpha * (1f - along));
                if (i > 0) builder.AddQuad(previousLeft, previousRight, right, left, previousColor, previousColor, color, color);
                previousLeft = left;
                previousRight = right;
                previousColor = color;
                if (travelled >= visibleLength)
                {
                    kept = i + 1;
                    break;
                }
            }
            if (kept < points.Count) points.RemoveRange(kept, points.Count - kept);
            builder.ApplyTo(mesh);
        }
    }
}
