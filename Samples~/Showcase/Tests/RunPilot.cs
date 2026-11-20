using System.Collections.Generic;
using SfxShowcase.Core;
using SfxShowcase.Level;
using UnityEngine;

namespace SfxShowcase.Tests
{
    /// <summary>
    /// Plays the level the way a person who knows it would, through the same three controls: it holds right, tucks
    /// where the route says "roll here", and jumps where the route says "jump here" and whenever a Clipper or a Static
    /// burr is close ahead. The route is data (stretches of x), so when the level changes only the two tables the test
    /// passes in change. Call <see cref="Tick"/> once a frame.
    /// </summary>
    public sealed class RunPilot
    {
        /// <summary>A stretch of the level, by x, optionally only below a height (for the low route under the platforms).</summary>
        public readonly struct Stretch
        {
            /// <summary>Where the stretch begins, world x.</summary>
            public readonly float FromX;
            /// <summary>Where the stretch ends, world x.</summary>
            public readonly float ToX;
            /// <summary>The stretch only counts while the player is below this height.</summary>
            public readonly float BelowY;

            /// <summary>Creates a stretch.</summary>
            public Stretch(float fromX, float toX, float belowY = float.PositiveInfinity)
            {
                FromX = fromX; ToX = toX; BelowY = belowY;
            }

            /// <summary>Whether a position lies in the stretch.</summary>
            public bool Contains(Vector2 position) => position.x >= FromX && position.x <= ToX && position.y < BelowY;
        }

        /// <summary>How long Jump is held, seconds: long enough for a full jump.</summary>
        const float JumpHoldSeconds = 0.5f;
        /// <summary>How long Jump is let go before the next press, seconds.</summary>
        const float JumpRestSeconds = 0.08f;
        /// <summary>A hazard nearer than this (metres ahead) is too close to jump.</summary>
        const float HazardNearest = 1.5f;
        /// <summary>A hazard this far ahead (metres) is jumped.</summary>
        const float HazardFarthest = 4.5f;
        /// <summary>Hazards more than this far above or below the feet are on another floor.</summary>
        const float HazardHeightBand = 2.5f;

        readonly PlayerBase player;
        readonly ScriptedInput input;
        readonly IReadOnlyList<Stretch> jumps, tucks;
        readonly Clipper[] clippers;
        readonly StaticBurr[] burrs;
        float holdUntil = float.NegativeInfinity, restUntil = float.NegativeInfinity;

        /// <summary>Creates a pilot for a player, writing to the input the player reads.</summary>
        public RunPilot(PlayerBase player, ScriptedInput input, IReadOnlyList<Stretch> jumps, IReadOnlyList<Stretch> tucks)
        {
            this.player = player;
            this.input = input;
            this.jumps = jumps;
            this.tucks = tucks;
            clippers = Object.FindObjectsByType<Clipper>(FindObjectsSortMode.None);
            burrs = Object.FindObjectsByType<StaticBurr>(FindObjectsSortMode.None);
        }

        /// <summary>Sets the controls for this frame.</summary>
        public void Tick()
        {
            Vector2 position = player.Position;
            input.MoveX = 1f;
            input.Down = In(tucks, position);

            bool wantsJump = player.IsGrounded && !input.Down && (In(jumps, position) || HazardAhead(position));
            if (wantsJump && Time.time >= restUntil)
            {
                holdUntil = Time.time + JumpHoldSeconds;
                restUntil = holdUntil + JumpRestSeconds;
            }
            input.JumpHeld = Time.time < holdUntil;
        }

        static bool In(IReadOnlyList<Stretch> stretches, Vector2 position)
        {
            foreach (Stretch stretch in stretches)
                if (stretch.Contains(position)) return true;
            return false;
        }

        bool HazardAhead(Vector2 position)
        {
            foreach (Clipper clipper in clippers)
                if (!clipper.IsDefeated && IsAhead(position, clipper.transform.position)) return true;
            foreach (StaticBurr burr in burrs)
                if (IsAhead(position, burr.transform.position)) return true;
            return false;
        }

        static bool IsAhead(Vector2 position, Vector2 hazardFoot)
        {
            float ahead = hazardFoot.x - position.x;
            float feetY = position.y - DesignTargets.StandingRadius;
            return ahead >= HazardNearest && ahead <= HazardFarthest && Mathf.Abs(hazardFoot.y - feetY) <= HazardHeightBand;
        }
    }
}
