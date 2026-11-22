using System.Collections.Generic;
using SfxShowcase.Core;
using UnityEngine;

namespace SfxShowcase.Art
{
    /// <summary>
    /// Brio, built from the kit's primitives (DESIGN.md, "How Brio is drawn"): a Brio Yellow circle 1 m across, one
    /// Ember Orange leaf-shaped crest that leans back with speed, two tall Ink eyes towards the facing side and two
    /// Ember Orange feet. Tucked, all of that hides and Brio is the circle with one orange stripe, so the spin shows.
    /// This class only builds the parts; <see cref="CharacterVisual"/> moves them.
    /// </summary>
    public static class CharacterArt
    {
        const float BodyRadius = 0.5f;
        const int TrailOrder = -3;
        const int BehindBody = -1;
        const int InFrontOfBody = 1;

        /// <summary>The character, animated from the player's state. Origin: the player's centre.</summary>
        public static GameObject Create(ArtKit kit, PlayerBase player)
        {
            var brio = new GameObject("Brio");

            GameObject trail = kit.CreatePart(
                "Trail",
                null,
                DepthLayer.Character,
                TrailOrder,
                brio.transform,
                transparent: true
            );
            trail.AddComponent<CharacterTrail>().Configure(player);

            Transform pose = NewGroup("Pose", brio.transform);
            Transform standing = NewGroup("Standing", pose);
            Transform crest = Part(kit, "Crest", CrestMesh, BehindBody, standing, new Vector2(-0.06f, 0.4f));
            Part(kit, "Body", BodyMesh, 0, standing, Vector2.zero);
            Transform eyes = Part(kit, "Eyes", EyesMesh, InFrontOfBody, standing, new Vector2(0.23f, 0.08f));
            Transform backFoot = Part(kit, "Back Foot", FootMesh, InFrontOfBody, standing, Vector2.zero);
            Transform frontFoot = Part(kit, "Front Foot", FootMesh, InFrontOfBody, standing, Vector2.zero);
            Transform footBlur = Part(kit, "Foot Blur", FootBlurMesh, InFrontOfBody, standing, Vector2.zero);

            Transform tucked = NewGroup("Tucked", pose);
            Transform halo = kit.CreatePart(
                "Charge Halo",
                kit.SharedMesh("Brio Charge Halo", HaloMesh),
                DepthLayer.Character,
                BehindBody,
                tucked,
                transparent: true
            ).transform;
            Transform ball = Part(kit, "Ball", BallMesh, 0, tucked, Vector2.zero);

            var visual = brio.AddComponent<CharacterVisual>();
            visual.Configure(player, pose, standing, tucked);
            visual.ConfigureStanding(crest, eyes, backFoot, frontFoot, footBlur);
            visual.ConfigureTucked(ball, halo);
            return brio;
        }

        static Transform NewGroup(string name, Transform parent)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        static Transform Part(
            ArtKit kit,
            string name,
            System.Func<Mesh> mesh,
            int order,
            Transform parent,
            Vector2 position
        )
        {
            Transform part = kit.CreatePart(
                name,
                kit.SharedMesh("Brio " + name, mesh),
                DepthLayer.Character,
                order,
                parent
            ).transform;
            part.localPosition = position;
            return part;
        }

        static Mesh BodyMesh()
        {
            return new InkedShape()
                .Add(Shapes.Ellipse(Vector2.zero, BodyRadius, BodyRadius), Palette.BrioYellow)
                .ToMesh("Brio Body");
        }

        /// <summary>
        /// The crest: a leaf standing on the head. Its origin is its round root, so it leans about that.
        /// </summary>
        static Mesh CrestMesh()
        {
            return new InkedShape().Add(Shapes.Teardrop(0.6f, 0.15f), Palette.EmberOrange).ToMesh("Brio Crest");
        }

        /// <summary>Two tall eyes side by side; the origin is between them so they squash to lines in place.</summary>
        static Mesh EyesMesh()
        {
            var builder = new MeshBuilder();
            builder.AddPolygon(Shapes.Ellipse(new Vector2(-0.09f, 0f), 0.055f, 0.14f, 16), Palette.Ink);
            builder.AddPolygon(Shapes.Ellipse(new Vector2(0.09f, 0f), 0.055f, 0.14f, 16), Palette.Ink);
            return builder.ToMesh("Brio Eyes");
        }

        static Mesh FootMesh()
        {
            return new InkedShape()
                .Add(Shapes.Ellipse(Vector2.zero, 0.17f, 0.09f, 16), Palette.EmberOrange)
                .ToMesh("Brio Foot");
        }

        /// <summary>What the feet become at top speed: one flattened oval ring where they were cycling.</summary>
        static Mesh FootBlurMesh()
        {
            List<Vector2> outer = Shapes.Ellipse(Vector2.zero, 0.36f, 0.13f);
            List<Vector2> inner = Shapes.Ellipse(Vector2.zero, 0.22f, 0.05f);
            var shape = new InkedShape();
            for (int i = 0; i < outer.Count; i++)
            {
                int next = (i + 1) % outer.Count;
                shape.Add(new[] { inner[i], outer[i], outer[next], inner[next] }, Palette.EmberOrange);
            }

            return shape.ToMesh("Brio Foot Blur");
        }

        /// <summary>
        /// Brio tucked: the circle with one stripe across it, off-centre, so every turn of the ball is visible.
        /// </summary>
        static Mesh BallMesh()
        {
            const float stripeLowDegrees = 11.5f;
            const float stripeHighDegrees = 39.8f;
            const int arcSteps = 6;
            var stripe = new List<Vector2>();
            for (int i = 0; i <= arcSteps; i++)
            {
                stripe.Add(
                    MeshBuilder.Direction(Mathf.Lerp(stripeLowDegrees, stripeHighDegrees, i / (float)arcSteps))
                        * BodyRadius
                );
            }

            for (int i = 0; i <= arcSteps; i++)
            {
                stripe.Add(
                    MeshBuilder.Direction(
                        Mathf.Lerp(180f - stripeHighDegrees, 180f - stripeLowDegrees, i / (float)arcSteps)
                    ) * BodyRadius
                );
            }

            return new InkedShape()
                .Add(Shapes.Ellipse(Vector2.zero, BodyRadius, BodyRadius), Palette.BrioYellow)
                .AddDetail(stripe, Palette.EmberOrange)
                .ToMesh("Brio Ball");
        }

        /// <summary>
        /// The ring that swells round Brio while a Wind-Up charges. Translucent: transparent material.
        /// </summary>
        static Mesh HaloMesh()
        {
            var builder = new MeshBuilder();
            builder.AddArc(
                Vector2.zero,
                0.6f,
                0.72f,
                0f,
                360f,
                ArtStyle.CircleSegments,
                Palette.WithAlpha(Palette.BrioYellow, 0.45f)
            );
            return builder.ToMesh("Brio Charge Halo");
        }
    }
}
