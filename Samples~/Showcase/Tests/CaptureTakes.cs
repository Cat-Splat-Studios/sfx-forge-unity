using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CatSplat.SfxForge;
using NUnit.Framework;
using SfxShowcase.Audio;
using SfxShowcase.Core;
using SfxShowcase.Level;
using UnityEngine;
using UnityEngine.TestTools;

namespace SfxShowcase.Tests
{
    /// <summary>
    /// The takes: each plays a scripted piece of the showcase and records it with <see cref="CaptureRecorder"/> into
    /// <c>out/unity-video/&lt;take&gt;/</c> (pictures f00000.png …, audio.wav, telemetry.json), for the videos and the
    /// manual's figures. They are not tests of the game: every one is <c>[Explicit]</c>, so the ordinary Play mode run
    /// skips them, and is run by name (see "Recording it" in the showcase's README):
    ///
    ///   -runTests -testPlatform PlayMode -testFilter "SfxShowcase.Tests.CaptureTakes.FullRun"
    ///
    /// The input goes through the same seam as in the tests (<see cref="PlayerBase.InputOverride"/>); a take that
    /// shows one technique moves the player with <see cref="PlayerBase.PlaceAt"/> where the level is too long to walk.
    /// A take asserts what it is meant to show, so a recording that missed its point fails instead of being used.
    /// </summary>
    [Category("Capture")]
    public sealed class CaptureTakes : ShowcaseTestBase
    {
        /// <summary>A take may write three thousand pictures: far longer than a test's usual three minutes.</summary>
        const int TakeTimeoutMilliseconds = 30 * 60 * 1000;

        /// <summary>Set this environment variable to record somewhere else than the usual folder (<see cref="CaptureFolder"/>).</summary>
        const string FolderVariable = "SFXFORGE_CAPTURE_DIR";

        /// <summary>How far above the ground a cut puts the player's centre: a hair more than standing.</summary>
        const float CutClearance = 0.05f;

        const int PatternE = 4;

        CaptureRecorder recorder;
        CaptureTelemetry telemetry;
        FollowCamera follow;

        /// <summary>
        /// The folder all takes share: out/unity-video at the repository's root when the project is the repository's
        /// unity/Demo, and Captures in the project's folder anywhere else (<see cref="ShowcaseFolders.CaptureFolder"/>).
        /// </summary>
        static string CaptureFolder => ShowcaseFolders.CaptureFolder(FolderVariable);

        /// <summary>A failed take leaves no recorder behind: the camera and the clock are given back.</summary>
        [TearDown]
        public void PutTheRigAway()
        {
            telemetry?.Dispose();
            telemetry = null;
            if (recorder != null) UnityEngine.Object.Destroy(recorder.gameObject);
            recorder = null;
        }

        // ---- the rig ----------------------------------------------------------------------------------------------------

        /// <summary>Puts the recorder on the game camera (16:9, 30 frames a second) and lets the view lay itself out.</summary>
        IEnumerator Rig(string take)
        {
            follow = UnityEngine.Object.FindFirstObjectByType<FollowCamera>();
            Assert.IsNotNull(follow, "the scene has its camera");
            recorder = CaptureRecorder.Create(follow.GetComponent<Camera>(), CaptureFolder, take);
            SnapCamera();
            yield return null;
            yield return null;
        }

        /// <summary>From this frame on, pictures, sound and telemetry are written.</summary>
        void Roll()
        {
            telemetry = new CaptureTelemetry(player, session);
            recorder.BeginRecording(telemetry);
        }

        /// <summary>Stops the take, writes telemetry.json and checks the take has sound, as much sound as picture, and a mix that stays under <see cref="CaptureRecorder.MixCeiling"/> at full volume.</summary>
        CaptureRecorder.Summary Wrap()
        {
            string folder = recorder.TakeFolder;
            CaptureRecorder.Summary summary = recorder.Finish();
            recorder = null;
            telemetry.Write(folder, summary);
            telemetry.Dispose();
            telemetry = null;

            Assert.Greater(summary.Frames, 0, "the take has pictures");
            Assert.AreEqual(summary.PictureSeconds, summary.AudioSeconds, 0.03f, "the wav is as long as the pictures, to within one audio block");
            if (SfxNative.Available) Assert.Greater(summary.Peak, 0.01f, "the wav is not silent");
            Assert.Less(summary.Peak / CaptureRecorder.ListenerVolume, CaptureRecorder.MixCeiling, "the game's mix, at full volume, stays under full scale");
            return summary;
        }

        static IEnumerator Seconds(float seconds) => UntilGameTime(() => false, seconds);

        /// <summary>A cut: the player stands at x on the ground, and the camera is there at once instead of gliding over.</summary>
        IEnumerator CutTo(float x) => CutTo(x, GroundAt(x));

        /// <summary>A cut to ground of a given height: for a place with a platform above it.</summary>
        IEnumerator CutTo(float x, float groundY)
        {
            input.Release();
            player.PlaceAt(new Vector2(x, groundY + DesignTargets.StandingRadius + CutClearance));
            SnapCamera();
            yield return UntilGameTime(() => player.IsGrounded, 1f);
            Assert.IsTrue(player.IsGrounded, $"after the cut the player stands on the ground at x = {x}");
        }

        /// <summary>The follow camera glides; a cut must not. It has a flag for that, set here the way a respawn sets it.</summary>
        void SnapCamera()
        {
            FieldInfo snap = typeof(FollowCamera).GetField("snapNextFrame", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(snap, "FollowCamera has its snapNextFrame (without it a cut would glide)");
            Assert.IsNotNull(follow, "the take has its camera");
            snap.SetValue(follow, true);
        }

        /// <summary>The height of the highest ground at x.</summary>
        static float GroundAt(float x)
        {
            const float probeTop = 100f, probeLength = 300f;
            float highest = float.NegativeInfinity;
            foreach (RaycastHit2D hit in Physics2D.RaycastAll(new Vector2(x, probeTop), Vector2.down, probeLength))
                if (hit.collider.GetComponent<GroundSurface>() != null) highest = Mathf.Max(highest, hit.point.y);
            Assert.IsFalse(float.IsNegativeInfinity(highest), $"there is ground at x = {x}");
            return highest;
        }

        /// <summary>
        /// Lets the pilot play until the condition holds, deciding once per physics step as it does in the tests (see
        /// <see cref="CapturePilot"/>); logs where the player is once a game second.
        /// </summary>
        IEnumerator PilotInStep(Func<bool> until, float seconds, Action eachFrame = null)
        {
            var driver = new GameObject("Capture Pilot").AddComponent<CapturePilot>();
            driver.Pilot = new RunPilot(player, input, Route("JumpStretches"), Route("TuckStretches"));
            float end = Time.time + seconds, nextReport = Time.time;
            while (Time.time < end && !until())
            {
                eachFrame?.Invoke();
                if (Time.time >= nextReport)
                {
                    nextReport += 1f;
                    Vector2 at = player.Position;
                    Debug.Log($"CAPTURE RUN  t={session.ElapsedSeconds,6:0.0}  x={at.x,7:0.0}  y={at.y,6:0.0}  speed={player.Speed,5:0.0}  {(player.IsGrounded ? "ground" : "air   ")}  {(player.IsRolling ? "tucked" : "      ")}  chimes {session.ChimeCount}  hurts {events.Hurts}  respawns {events.Respawns}");
                }
                yield return null;
            }
            driver.Pilot = null;
            UnityEngine.Object.Destroy(driver.gameObject);
            input.Release();
        }

        /// <summary>The pilot's route through the level: the tables the tests' own pilot uses, which the base class keeps to itself.</summary>
        static RunPilot.Stretch[] Route(string table)
        {
            FieldInfo field = typeof(ShowcaseTestBase).GetField(table, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"ShowcaseTestBase has its {table}");
            return (RunPilot.Stretch[])field.GetValue(null);
        }

        /// <summary>Holds Jump for a full jump and waits for the landing.</summary>
        IEnumerator FullJump()
        {
            input.JumpHeld = true;
            yield return Seconds(0.5f);
            input.JumpHeld = false;
            yield return UntilGameTime(() => player.IsGrounded, 3f);
        }

        /// <summary>Winds up with a number of taps, a pause after each so the rev is heard climbing.</summary>
        IEnumerator WindUp(int taps, float pauseSeconds)
        {
            input.Down = true;
            yield return Seconds(0.15f);
            for (int tap = 0; tap < taps; tap++)
            {
                yield return TapJump();
                yield return Seconds(pauseSeconds);
            }
        }

        // ---- the whole level ------------------------------------------------------------------------------------------

        /// <summary>The title card for three seconds, the whole level from start to goal, the results for six.</summary>
        [UnityTest, Explicit, Timeout(TakeTimeoutMilliseconds)]
        public IEnumerator FullRun()
        {
            yield return Rig("full-run");
            Roll();
            yield return Seconds(3f);

            input.JumpHeld = true;                                   // Jump on the title card, as a player starts it
            yield return UntilGameTime(() => session.Phase == GamePhase.Playing, 0.5f);
            Assert.AreEqual(GamePhase.Playing, session.Phase, "Jump leaves the title card");

            yield return PilotInStep(() => events.GoalsReached > 0, 240f);
            Assert.AreEqual(1, events.GoalsReached, "the pilot reached the goal");
            yield return Seconds(6f);
            Wrap();
        }

        // ---- one technique each ---------------------------------------------------------------------------------------

        /// <summary>One-shots: a jump and its landing, a hop, two rows of Chimes with the rising combo, a Woofer.</summary>
        [UnityTest, Explicit, Timeout(TakeTimeoutMilliseconds)]
        public IEnumerator OneShots()
        {
            yield return StartRun();
            yield return Rig("one-shots");
            yield return Seconds(0.5f);
            Roll();
            yield return Seconds(1f);

            yield return FullJump();
            yield return Seconds(0.6f);
            yield return TapJump();
            yield return Seconds(0.2f);
            yield return UntilGameTime(() => player.IsGrounded, 3f);
            yield return Seconds(0.6f);
            Assert.AreEqual(2, events.Jumps, "a full jump and a hop");

            // the first row of Chimes, the Chime arc over the low step, the second row
            yield return PilotInStep(() => player.Position.x > 48.6f, 12f);
            yield return UntilGameTime(() =>
            {
                input.MoveX = player.Velocity.x > 0.5f ? -1f : 0f;   // brake hard: the skid is a one-shot too
                return player.Speed < 0.1f && player.IsGrounded;
            }, 3f);
            input.Release();
            Assert.GreaterOrEqual(events.HighestCombo, 5, "a row of Chimes collected in one go: the combo climbs");
            yield return Seconds(0.6f);

            // a Woofer: run into it, be thrown onto the terrace
            Woofer woofer = Leftmost(UnityEngine.Object.FindObjectsByType<Woofer>(FindObjectsSortMode.None));
            yield return CutTo(woofer.transform.position.x - 7f);
            yield return Seconds(0.7f);
            input.MoveX = 1f;
            yield return UntilGameTime(() => events.WooferBounces > 0, 4f);
            input.Release();
            Assert.AreEqual(1, events.WooferBounces, "the Woofer fired");
            yield return Seconds(0.2f);
            yield return UntilGameTime(() => player.IsGrounded, 4f);
            yield return Seconds(1.5f);
            Wrap();
        }

        /// <summary>Held sounds: the Wind-Up rev climbing over six taps, the Burst, the roll hum on the flat, then a Tuck down the run-up and round the loop.</summary>
        [UnityTest, Explicit, Timeout(TakeTimeoutMilliseconds)]
        public IEnumerator HeldSounds()
        {
            yield return StartRun();
            yield return Rig("held-sounds");
            yield return Seconds(0.5f);
            Roll();
            yield return Seconds(1.2f);

            yield return WindUp(6, 0.22f);
            Assert.IsTrue(player.IsCharging, "Down and Jump at rest winds up");
            recorder.RequestStill("wind-up");
            yield return Seconds(0.3f);
            input.Down = false;
            yield return UntilGameTime(() => events.Bursts > 0, 0.5f);
            Assert.AreEqual(1, events.Bursts, "letting go of Down releases the Burst");
            yield return Seconds(0.3f);
            Assert.IsTrue(player.IsRolling, "the player rolls away");
            recorder.RequestStill("burst-roll");
            yield return UntilGameTime(() => !player.IsRolling, 5f);
            yield return Seconds(0.8f);

            // the top of the third hill: run, tuck down the 15 m run-up, round the loop, up the rise
            yield return CutTo(207f);
            yield return Seconds(0.6f);
            bool rolledDown = false;
            yield return PilotInStep(() => player.Position.x > 296f, 15f, () => rolledDown |= player.IsRolling && player.IsGrounded);
            Assert.Greater(player.Position.x, 296f, "down the run-up and round the loop");
            Assert.IsTrue(rolledDown, "tucked on the way down");

            // brake the roll on the rise: the hum sinks with the speed and is let go
            yield return UntilGameTime(() =>
            {
                input.MoveX = player.Velocity.x > 0.5f ? -1f : 0f;
                return player.Speed < 0.1f && player.IsGrounded;
            }, 5f);
            input.Release();
            yield return Seconds(1.2f);
            Wrap();
        }

        /// <summary>Adaptive music: stand (the rhythm section), walk, run, stop, then a Burst flat out (the whole band) and the layers falling away afterwards; the song walks through its own arrangement all the while.</summary>
        [UnityTest, Explicit, Timeout(TakeTimeoutMilliseconds)]
        public IEnumerator AdaptiveMusic()
        {
            const float walkSpeed = 4f;                              // Soft Start's floor 0.42 becomes about 0.53: the strings are in (0.47), the keys (0.57, at 5.6 m/s) wait for the run
            var director = UnityEngine.Object.FindFirstObjectByType<MusicDirector>();
            Assert.IsNotNull(director, "the scene has its music director");

            yield return StartRun();
            yield return Rig("adaptive-music");
            yield return Seconds(0.5f);
            Roll();
            float atRest = director.Intensity;
            yield return Seconds(3f);

            // walk: this pad has no half speed, so the script lets go whenever the player is fast enough
            float walkUntil = Time.time + 2.5f;
            while (Time.time < walkUntil)
            {
                input.MoveX = player.Speed < walkSpeed ? 1f : 0f;
                yield return null;
            }
            float walking = director.Intensity;

            // run at cruise speed, over the low step
            float running = 0f;
            yield return PilotInStep(() => player.Position.x > 40f, 8f, () => running = Mathf.Max(running, director.Intensity));
            yield return UntilGameTime(() => player.Speed < 0.1f && player.IsGrounded, 5f);

            // turn round, wind up, Burst: back along the flat at nearly top speed
            input.MoveX = -1f;
            yield return null;
            yield return null;
            input.Release();
            yield return UntilGameTime(() => player.Speed < 0.1f, 2f);
            Assert.AreEqual(-1, player.Facing, "the player faces back the way it came");
            yield return WindUp(6, 0.05f);
            input.Down = false;
            float flatOut = 0f;
            yield return UntilGameTime(() => { flatOut = Mathf.Max(flatOut, director.Intensity); return events.Bursts > 0 && player.Speed < 0.1f; }, 8f);
            yield return Seconds(4.5f);
            float afterwards = director.Intensity;

            Assert.Greater(walking, atRest + 0.03f, "walking brings a little more of the band in");
            Assert.Greater(running, walking + 0.1f, "running more");
            Assert.Greater(flatOut, 0.8f, "and a Burst nearly all of it");
            Assert.Less(afterwards, atRest + 0.05f, "standing still, the layers fall away again");
            Wrap();
        }

        /// <summary>Sections and cues: from Woofer Steps into Static Field (the song leaves its own order for the breakdown, pattern E, on the bar line), past its Tuning Fork (three bells answer).</summary>
        [UnityTest, Explicit, Timeout(TakeTimeoutMilliseconds)]
        public IEnumerator SectionsAndCues()
        {
            var music = UnityEngine.Object.FindFirstObjectByType<SfxMusicPlayer>();
            Assert.IsNotNull(music, "the scene has its music player");
            SectionInfo staticField = Sections.Get(SectionId.StaticField);

            // in the dip at the end of Woofer Steps, where the song plays in its own order; the take begins on a bar line
            yield return Rig("sections-and-cues");
            yield return StartRun();
            yield return CutTo(staticField.StartX - 12.5f);
            int barOfTheCut = music.Bar;
            yield return UntilGameTime(() => music.Bar != barOfTheCut, 6f);
            if (SfxNative.Available) Assert.AreNotEqual(PatternE, music.Pattern, "the song is not in its breakdown yet");
            Roll();
            yield return Seconds(3f);

            // up the slope, over the boundary, past the fork; let go there and brake only if the first ridge comes close
            input.MoveX = 1f;
            float forkX = level.Checkpoints[1].x;
            yield return UntilGameTime(() => player.Position.x > forkX + 0.5f, 6f);
            Assert.Contains(SectionId.StaticField, events.Sections, "the boundary was crossed");
            Assert.Contains(1, events.Checkpoints, "and the Tuning Fork lit");
            float crossedAt = Time.time;
            yield return UntilGameTime(() =>
            {
                input.MoveX = player.Position.x > forkX + 11f && player.Speed > 0.5f ? -1f : 0f;
                return player.Speed < 0.1f && player.IsGrounded;
            }, 5f);
            input.Release();
            yield return UntilGameTime(() => Time.time > crossedAt + 7f, 8f);
            if (SfxNative.Available) Assert.AreEqual(PatternE, music.Pattern, "Static Field plays pattern E");
            Assert.AreEqual(0, events.Hurts, "nobody was hurt in this take");
            Wrap();
        }

        /// <summary>
        /// Hurt and goal: run along Split Signal's low terrace into its Static with twelve Chimes (scatter, the band
        /// drops to the kick, the music dips), then run through the Resonator (fanfare, results).
        /// </summary>
        [UnityTest, Explicit, Timeout(TakeTimeoutMilliseconds)]
        public IEnumerator HurtAndGoal()
        {
            const int carried = 12;
            const float terraceStartX = 726f, terraceY = 1.5f;       // LevelLayout.SplitSignal: the low terrace, under a platform
            const float staticX = 736f;
            const float splitSignalFloor = 0.55f;                    // MusicDirector.sectionCues: the intensity there at a standstill
            var music = UnityEngine.Object.FindFirstObjectByType<SfxMusicPlayer>();
            var director = UnityEngine.Object.FindFirstObjectByType<MusicDirector>();
            Assert.IsNotNull(music, "the scene has its music player");
            Assert.IsNotNull(director, "the scene has its music director");

            yield return StartRun();
            yield return Rig("hurt-and-goal");
            yield return CutTo(terraceStartX + 0.8f, terraceY);
            Assert.AreEqual(terraceY + DesignTargets.StandingRadius, player.Position.y, 0.2f, "the player stands on the low terrace");
            player.AddChimes(carried);
            yield return Seconds(2f);                                // their ping has rung out before the take begins
            int barOfTheCut = music.Bar;
            yield return UntilGameTime(() => music.Bar != barOfTheCut, 6f);          // the take begins on a bar line
            Roll();
            yield return Seconds(2.2f);

            // along the terrace into the Static, at a run: the band is well above Split Signal's floor, so its drop to the kick is seen
            input.MoveX = 1f;
            float before = 0f;
            yield return UntilGameTime(() => { if (events.Hurts == 0) before = director.Intensity; return events.Hurts > 0; }, 8f);
            input.Release();
            Assert.AreEqual(1, events.Hurts, "the Static hurt");
            Assert.Less(Mathf.Abs(player.Position.x - staticX), 4f, "and it was the Static of the low terrace");
            Assert.GreaterOrEqual(before, splitSignalFloor + 0.1f, "the band was well above Split Signal's floor before the hit, so the drop is the hit's");
            Assert.GreaterOrEqual(events.LastScatterCount, carried, "and the carried Chimes scattered (with any picked up on the way)");
            yield return Seconds(0.25f);
            recorder.RequestStill("hurt-scatter");
            yield return Seconds(4.5f);

            // the last strip: run through the Resonator
            yield return CutTo(Sections.Get(SectionId.TheResonator).StartX + 4f);
            yield return Seconds(0.6f);
            input.MoveX = 1f;
            yield return UntilGameTime(() => events.GoalsReached > 0, 8f);
            input.Release();
            Assert.AreEqual(1, events.GoalsReached, "the goal was reached");
            Assert.AreEqual(GamePhase.Finished, session.Phase);
            yield return Seconds(6.5f);
            Wrap();
        }

        // ---- the sync measurement ---------------------------------------------------------------------------------------

        /// <summary>
        /// For measuring, not for showing: the song is stopped, then three jumps (a baked clip) and three Chimes (a live
        /// voice) are played a second apart into silence. <c>docs/video/check_unity_capture.py</c> compares the frame of
        /// each event in telemetry.json with where its sound starts in the wav.
        /// </summary>
        [UnityTest, Explicit, Timeout(TakeTimeoutMilliseconds)]
        public IEnumerator SyncCheck()
        {
            var director = UnityEngine.Object.FindFirstObjectByType<MusicDirector>();
            var music = UnityEngine.Object.FindFirstObjectByType<SfxMusicPlayer>();
            yield return StartRun();
            director.enabled = false;
            music.Stop();

            yield return Rig("sync-check");
            Roll();
            yield return Seconds(4f);                                // the song's echoes die away inside the take
            for (int jump = 0; jump < 3; jump++)
            {
                yield return FullJump();
                yield return Seconds(2.5f);
            }
            for (int chime = 0; chime < 3; chime++)
            {
                player.AddChimes(1);
                yield return Seconds(2.5f);
            }
            Assert.AreEqual(3, events.Jumps);
            Assert.AreEqual(3, events.ChimesCollected);
            Wrap();
        }

        // ---- stills for the manual --------------------------------------------------------------------------------------

        /// <summary>
        /// Twelve pictures of one run into <c>out/unity-video/stills/</c>: the title card, a moment of every section,
        /// the first loop upside down, a Woofer's throw, the last flight, the results. (Three more come out of the
        /// takes above: wind-up, burst-roll, hurt-scatter.) Nothing else is written.
        /// </summary>
        [UnityTest, Explicit, Timeout(TakeTimeoutMilliseconds)]
        public IEnumerator Stills()
        {
            yield return Rig("stills");
            yield return Seconds(1f);
            recorder.RequestStill("01-title");
            yield return null;

            input.JumpHeld = true;
            yield return UntilGameTime(() => session.Phase == GamePhase.Playing, 0.5f);
            Assert.AreEqual(GamePhase.Playing, session.Phase, "Jump leaves the title card");

            LevelLayout.GetLoop(0, out Vector2 loopCentre, out float loopRadius);
            float wooferAt = float.PositiveInfinity, launchAt = float.PositiveInfinity;
            Func<float, Func<bool>> pastX = x => () => player.Position.x >= x;
            var moments = new List<KeyValuePair<string, Func<bool>>>
            {
                new KeyValuePair<string, Func<bool>>("02-soft-start", pastX(17f)),
                new KeyValuePair<string, Func<bool>>("03-rolling-sines", pastX(121f)),
                new KeyValuePair<string, Func<bool>>("04-full-cycle-run-up", pastX(244f)),
                new KeyValuePair<string, Func<bool>>("05-loop", () => Mathf.Abs(player.Position.x - loopCentre.x) < loopRadius && player.IsGrounded && Mathf.Abs(player.GroundAngle) > 150f),
                new KeyValuePair<string, Func<bool>>("06-woofer-throw", () => Time.time > wooferAt + 0.35f),
                new KeyValuePair<string, Func<bool>>("07-static-field", pastX(521f)),
                new KeyValuePair<string, Func<bool>>("08-split-signal", pastX(686f)),
                new KeyValuePair<string, Func<bool>>("09-overdrive", pastX(860f)),
                new KeyValuePair<string, Func<bool>>("10-final-launch", () => Time.time > launchAt + 0.5f),
                new KeyValuePair<string, Func<bool>>("11-the-resonator", pastX(level.GoalPosition.x - 7f)),
            };
            int next = 0;
            Action watch = () =>
            {
                if (events.WooferBounces > 0 && float.IsPositiveInfinity(wooferAt)) wooferAt = Time.time;
                if (events.FinalLaunches > 0 && float.IsPositiveInfinity(launchAt)) launchAt = Time.time;
                if (next < moments.Count && moments[next].Value())
                {
                    recorder.RequestStill(moments[next].Key);
                    next++;
                }
            };
            yield return PilotInStep(() => events.GoalsReached > 0, 240f, watch);
            Assert.AreEqual(1, events.GoalsReached, "the pilot reached the goal");
            Assert.AreEqual(moments.Count, next, "every moment of the run was pictured");

            yield return Seconds(3f);
            recorder.RequestStill("12-results");
            yield return null;
            recorder.Finish();
            recorder = null;
        }
    }
}
