using System;
using System.Collections;
using NUnit.Framework;
using SfxShowcase.Core;
using SfxShowcase.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SfxShowcase.Tests
{
    /// <summary>
    /// What every Play mode test of the showcase starts from: the scene loaded, the player found and driven through the
    /// same seam a keyboard uses (<see cref="PlayerBase.InputOverride"/>), the game's events counted. Tests run on a
    /// fixed clock (every frame is one physics step), so they are repeatable and faster than real time; a test that
    /// listens to the audio thread switches to real time with <see cref="UseRealTime"/>.
    /// </summary>
    public abstract class ShowcaseTestBase
    {
        const string SceneName = "Showcase";

        /// <summary>Game seconds per frame on the fixed clock: one physics step.</summary>
        const float FrameSeconds = 0.02f;

        /// <summary>
        /// How long a scripted tap holds Jump, and how long it lets go: longer than a physics step on either clock.
        /// </summary>
        const float TapSeconds = 0.06f;

        /// <summary>
        /// Where the pilot presses Jump: the two low steps, the two pits, the ends of the high route's platforms,
        /// the low terrace.
        /// </summary>
        static readonly RunPilot.Stretch[] JumpStretches =
        {
            new RunPilot.Stretch(25.5f, 29f),
            new RunPilot.Stretch(50f, 54f),
            new RunPilot.Stretch(500f, 503.5f),
            new RunPilot.Stretch(552f, 555.5f),
            new RunPilot.Stretch(668.5f, 671.5f),
            new RunPilot.Stretch(689.5f, 692.5f),
            new RunPilot.Stretch(711.5f, 714.5f),
            new RunPilot.Stretch(733.5f, 736.5f),
            new RunPilot.Stretch(755.5f, 758.5f),
            new RunPilot.Stretch(721.5f, 725f, belowY: 3f),
        };

        /// <summary>
        /// Where the pilot holds Down to roll: the run-up to the first loop and the drop before the split.
        /// </summary>
        static readonly RunPilot.Stretch[] TuckStretches =
        {
            new RunPilot.Stretch(224f, 262f),
            new RunPilot.Stretch(612f, 640f),
        };

        /// <summary>The player of the loaded scene.</summary>
        protected PlayerController player;

        /// <summary>The run of the loaded scene.</summary>
        protected GameSession session;

        /// <summary>What the loaded level says about itself.</summary>
        protected LevelDescriptor level;

        /// <summary>The controls the test writes and the player reads.</summary>
        protected ScriptedInput input;

        /// <summary>The game's events since the scene loaded.</summary>
        protected GameEventLog events;

        /// <summary>Loads the scene and waits for the player to stand on the ground.</summary>
        [UnitySetUp]
        public IEnumerator Load()
        {
#if UNITY_EDITOR
            // By path, so the test does not depend on which scenes the build settings list at the moment; and the
            // path is found, not written down, so it does not depend on where the showcase's folder is either.
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ShowcaseFolders.ScenePath,
                new LoadSceneParameters(LoadSceneMode.Single)
            );
#else
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
#endif
            yield return null;
            player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            session = UnityEngine.Object.FindFirstObjectByType<GameSession>();
            level = UnityEngine.Object.FindFirstObjectByType<LevelDescriptor>();
            Assert.IsNotNull(player, "the scene has its player");
            Assert.IsNotNull(session, "the scene has its session");
            Assert.IsNotNull(level, "the scene has its level");

            input = new ScriptedInput();
            player.InputOverride = input;
            events = new GameEventLog();
            Time.captureDeltaTime = FrameSeconds;
            yield return UntilGameTime(() => player.IsGrounded, 2f);
        }

        /// <summary>Gives the clock back and stops counting events.</summary>
        [TearDown]
        public void Unload()
        {
            Time.captureDeltaTime = 0f;
            events?.Dispose();
        }

        /// <summary>Leaves the fixed clock: from here on a second is a second, as the audio thread needs.</summary>
        protected static void UseRealTime() => Time.captureDeltaTime = 0f;

        /// <summary>Waits until the condition holds, at most for some seconds of game time.</summary>
        protected static IEnumerator UntilGameTime(Func<bool> condition, float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end && !condition())
            {
                yield return null;
            }
        }

        /// <summary>
        /// Waits until the condition holds, at most for some seconds of real time (for what the audio thread does).
        /// </summary>
        protected static IEnumerator UntilRealTime(Func<bool> condition, float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end && !condition())
            {
                yield return null;
            }
        }

        /// <summary>Dismisses the title card.</summary>
        protected IEnumerator StartRun()
        {
            session.StartGame();
            yield return null;
            Assert.AreEqual(GamePhase.Playing, session.Phase, "StartGame leaves the title card");
            Assert.IsTrue(player.HasControl, "and gives the player the controls");
        }

        /// <summary>
        /// Presses Jump and lets it go again, each for long enough that a physics step sees it on either clock.
        /// </summary>
        protected IEnumerator TapJump()
        {
            input.JumpHeld = true;
            yield return UntilGameTime(() => false, TapSeconds);
            input.JumpHeld = false;
            yield return UntilGameTime(() => false, TapSeconds);
        }

        /// <summary>
        /// Lets the pilot play until the condition holds; logs where the player is once a game second.
        /// </summary>
        protected IEnumerator Pilot(Func<bool> until, float seconds, Action eachFrame = null, bool rolling = true)
        {
            var pilot = new RunPilot(player, input, JumpStretches, rolling ? TuckStretches : new RunPilot.Stretch[0]);
            float end = Time.time + seconds;
            float nextReport = Time.time;
            while (Time.time < end && !until())
            {
                pilot.Tick();
                eachFrame?.Invoke();
                if (Time.time >= nextReport)
                {
                    nextReport += 1f;
                    Vector2 at = player.Position;
                    Debug.Log(
                        $"SHOWCASE RUN  t={session.ElapsedSeconds, 6:0.0}  x={at.x, 7:0.0}  y={at.y, 6:0.0}  speed={player.Speed, 5:0.0}  {(player.IsGrounded ? "ground" : "air   ")}  {(player.IsRolling ? "tucked" : "      ")}  plane {player.Plane}  chimes {session.ChimeCount}  hurts {events.Hurts}  respawns {events.Respawns}"
                    );
                }

                yield return null;
            }

            input.Release();
        }

        /// <summary>The piece of a kind that stands furthest left in the level.</summary>
        protected static T Leftmost<T>(T[] pieces)
            where T : Component
        {
            Assert.IsNotEmpty(pieces, $"the level has a {typeof(T).Name}");
            T leftmost = pieces[0];
            foreach (T piece in pieces)
            {
                if (piece.transform.position.x < leftmost.transform.position.x)
                {
                    leftmost = piece;
                }
            }

            return leftmost;
        }
    }
}
