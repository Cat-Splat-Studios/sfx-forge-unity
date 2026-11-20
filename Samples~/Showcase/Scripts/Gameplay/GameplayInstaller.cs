using UnityEngine;
using SfxShowcase.Core;

namespace SfxShowcase.Gameplay
{
    /// <summary>
    /// Assembles the gameplay objects: the player and the run. Plain runtime code (no editor calls), so it also shows how
    /// to put the same objects together by hand: a GameObject with a kinematic Rigidbody2D, a trigger CircleCollider2D
    /// and a <see cref="PlayerController"/>, and a second GameObject with a <see cref="GameSession"/>.
    /// </summary>
    public static class GameplayInstaller
    {
        const string PlayerObjectName = "Brio";
        const string SessionObjectName = "Game Session";

        /// <summary>
        /// Creates the player at the level's spawn point and the session, wires them to each other, and stores them in
        /// the context. Needs <see cref="ShowcaseContext.Art"/> and <see cref="ShowcaseContext.Level"/>.
        /// </summary>
        public static void Install(ShowcaseContext context)
        {
            var playerObject = new GameObject(PlayerObjectName);
            playerObject.transform.position = context.Level.SpawnPoint;

            // The movement is hand-written; the body is kinematic and only there so the level's triggers see the player.
            // Interpolation shows the fixed-step movement smoothly at any frame rate.
            var body = playerObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            var touchCollider = playerObject.AddComponent<CircleCollider2D>();
            touchCollider.isTrigger = true;
            touchCollider.radius = DesignTargets.StandingRadius;

            var player = playerObject.AddComponent<PlayerController>();
            var session = new GameObject(SessionObjectName).AddComponent<GameSession>();
            player.Wire(context.Art, context.Level, session, body, touchCollider);
            session.Wire(player);

            context.Player = player;
            context.Session = session;
        }
    }
}
