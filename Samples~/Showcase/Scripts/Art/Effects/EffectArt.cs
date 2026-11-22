using SfxShowcase.Core;
using UnityEngine;

namespace SfxShowcase.Art
{
    /// <summary>
    /// The short bursts of feedback: "local, never the whole screen" – a pop, a burst of shards, a puff. Each is a
    /// handful of generated meshes (twelve pieces at most) in palette colours that plays once and removes itself.
    /// Effects cannot be touched, so they have no outline.
    /// </summary>
    public static class EffectArt
    {
        /// <summary>Spawns one burst at a world position. Runtime only.</summary>
        public static void Spawn(ArtKit kit, EffectKind kind, Vector2 position)
        {
            switch (kind)
            {
                case EffectKind.ChimePop:
                    SpawnChimePop(kit, position);
                    break;
                case EffectKind.ClipperShards:
                    // Four square shards thrown to the four diagonals, tumbling and falling.
                    SpawnScatter(
                        kit,
                        "Clipper Shards",
                        position,
                        kit.SharedMesh("Shard", ShardMesh),
                        4,
                        45f,
                        315f,
                        speed: 5f,
                        lift: 4f,
                        gravity: 25f,
                        spin: 540f,
                        seconds: 0.6f
                    );
                    break;
                case EffectKind.BurstPuff:
                    // Six cream puffs blown out all round the spot the Burst left from.
                    SpawnScatter(
                        kit,
                        "Burst Puff",
                        position,
                        kit.SharedMesh("Puff", () => DiscMesh("Puff", 0.22f)),
                        6,
                        0f,
                        300f,
                        speed: 3.5f,
                        lift: 0.5f,
                        gravity: 0f,
                        spin: 0f,
                        seconds: 0.35f
                    );
                    break;
                case EffectKind.Dust:
                    // Five small specks kicked up in a fan above the ground.
                    SpawnScatter(
                        kit,
                        "Dust",
                        position,
                        kit.SharedMesh("Dust", () => DiscMesh("Dust", 0.12f)),
                        5,
                        20f,
                        160f,
                        speed: 2.5f,
                        lift: 1f,
                        gravity: 8f,
                        spin: 0f,
                        seconds: 0.3f
                    );
                    break;
            }
        }

        static void SpawnChimePop(ArtKit kit, Vector2 position)
        {
            GameObject pop = kit.CreatePart("Chime Pop", null, DepthLayer.Effects);
            pop.transform.position = position;
            pop.AddComponent<PopEffect>();
        }

        /// <summary>
        /// Pieces sharing one mesh, thrown outwards at evenly spaced angles between two directions (degrees,
        /// counter-clockwise from +x). Evenly spaced rather than random: the burst reads the same every time.
        /// </summary>
        static void SpawnScatter(
            ArtKit kit,
            string name,
            Vector2 position,
            Mesh pieceMesh,
            int count,
            float firstDegrees,
            float lastDegrees,
            float speed,
            float lift,
            float gravity,
            float spin,
            float seconds
        )
        {
            var burst = new GameObject(name);
            burst.transform.position = position;
            var pieces = new Transform[count];
            var velocities = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                float degrees = Mathf.Lerp(firstDegrees, lastDegrees, count > 1 ? i / (float)(count - 1) : 0f);
                pieces[i] = kit.CreatePart("Piece", pieceMesh, DepthLayer.Effects, 0, burst.transform).transform;
                velocities[i] = MeshBuilder.Direction(degrees) * speed + Vector2.up * lift;
            }

            burst.AddComponent<ScatterEffect>().Play(pieces, velocities, gravity, spin, seconds);
        }

        static Mesh ShardMesh()
        {
            var builder = new MeshBuilder();
            builder.AddRect(new Vector2(-0.17f, -0.17f), new Vector2(0.17f, 0.17f), Palette.DormantGrey);
            return builder.ToMesh("Shard");
        }

        static Mesh DiscMesh(string name, float radius)
        {
            var builder = new MeshBuilder();
            builder.AddPolygon(Shapes.Ellipse(Vector2.zero, radius, radius, 12), Palette.SignalCream);
            return builder.ToMesh(name);
        }
    }
}
