using System;
using System.Collections;
using NUnit.Framework;
using SfxShowcase.Core;
using SfxShowcase.Gameplay;
using SfxShowcase.Level;
using UnityEngine;
using UnityEngine.TestTools;

namespace SfxShowcase.Tests
{
    /// <summary>
    /// The game in Play mode: does the player move as the design says, do the level's pieces raise their events, and
    /// can the level be finished? The sound has its own tests in <see cref="ShowcaseAudioTests"/>.
    /// </summary>
    public sealed class ShowcaseTests : ShowcaseTestBase
    {
        /// <summary>The whole-level run must leave Split Signal's launch ramp at least this fast (m/s) to land on the first platform.</summary>
        const float SplitLipMinimumSpeed = 14.5f;
        /// <summary>The whole-level run must leave Overdrive's last ramp at least this fast (m/s): the launch its Chime field is laid along.</summary>
        const float FinalLipMinimumSpeed = 16f;
        /// <summary>The fewest Chimes the whole-level run must pick up from Overdrive on (the final flight's field holds 15).</summary>
        const int OverdriveMinimumChimes = 4;

        // ---- The scene -------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TheSceneLoadsOnItsTitleCard()
        {
            Assert.AreEqual(GamePhase.Title, session.Phase, "the game waits on the title card");
            Assert.IsFalse(player.HasControl, "the player cannot move yet");
            Assert.AreEqual(level.ChimeCount, UnityEngine.Object.FindObjectsByType<Chime>(FindObjectsSortMode.None).Length, "every Chime the level counts is in the scene");
            Assert.IsNotNull(UnityEngine.Object.FindFirstObjectByType<FollowCamera>(), "the camera follows");

            input.MoveX = 1f;
            yield return UntilGameTime(() => false, 0.5f);
            Assert.AreEqual(level.SpawnPoint.x, player.Position.x, 0.01f, "holding right on the title card moves nothing");
        }

        [UnityTest]
        public IEnumerator ThePlayerStandsOnTheGroundAtTheSpawnPoint()
        {
            yield return UntilGameTime(() => false, 0.5f);
            Assert.IsTrue(player.IsGrounded, "the player has landed");
            Assert.AreEqual(level.SpawnPoint.x, player.Position.x, 0.05f, "where the level says a run starts");
            Assert.AreEqual(DesignTargets.StandingRadius, player.Position.y, 0.05f, "one radius above the ground at y = 0");
            Assert.AreEqual(0f, player.GroundSpeed, 0.01f, "at rest");
            Assert.AreEqual(0f, player.GroundAngle, 0.5f, "on flat ground");
            Assert.AreEqual(TerrainPlane.A, player.Plane, "on the plane loops are entered on");
        }

        // ---- Movement --------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator HoldingRightBuildsSpeedUpToCruise()
        {
            yield return StartRun();
            input.MoveX = 1f;
            yield return UntilGameTime(() => false, 1f);
            float afterOneSecond = player.GroundSpeed;
            Assert.Greater(afterOneSecond, 3f, "running builds speed");
            Assert.Less(afterOneSecond, DesignTargets.CruiseSpeed, "but not at once: momentum");

            yield return UntilGameTime(() => false, 1.5f);
            Assert.IsTrue(player.IsGrounded);
            Assert.AreEqual(DesignTargets.CruiseSpeed, player.GroundSpeed, 0.5f, "flat running tops out at cruise speed");
            Assert.AreEqual(1, player.Facing);

            input.Release();
            yield return UntilGameTime(() => player.GroundSpeed < 0.01f, 5f);
            Assert.Less(player.GroundSpeed, 0.01f, "letting go, friction brings the player to rest");
        }

        [UnityTest]
        public IEnumerator AJumpReachesTheDesignedHeightAndIsCutShortWhenLetGo()
        {
            yield return StartRun();
            float groundY = player.Position.y, highest = groundY;
            input.JumpHeld = true;
            yield return UntilGameTime(() => { highest = Mathf.Max(highest, player.Position.y); return !player.IsGrounded; }, 0.5f);
            Assert.AreEqual(1, events.Jumps, "Jump raises its event");
            yield return UntilGameTime(() => { highest = Mathf.Max(highest, player.Position.y); return player.IsGrounded; }, 3f);
            Assert.AreEqual(DesignTargets.JumpHeight, highest - groundY, 0.4f, "a held jump reaches the design's height");

            input.JumpHeld = false;
            yield return UntilGameTime(() => false, 0.2f);
            float tapped = player.Position.y;
            input.JumpHeld = true;
            yield return null;
            yield return null;
            input.JumpHeld = false;
            yield return UntilGameTime(() => { tapped = Mathf.Max(tapped, player.Position.y); return player.IsGrounded && events.Jumps == 2 && player.Velocity.y <= 0f; }, 3f);
            Assert.AreEqual(2, events.Jumps);
            Assert.Less(tapped - groundY, DesignTargets.JumpHeight * 0.6f, "a tapped jump is a short hop");
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator TheFirstSlopesAndTheLoopAreCleared()
        {
            yield return StartRun();
            LevelLayout.GetLoop(0, out Vector2 loopCentre, out float loopRadius);
            float highestInLoop = float.MinValue;
            bool wasOnPlaneB = false, wasUpsideDown = false;
            Action watchLoop = () =>
            {
                Vector2 at = player.Position;
                if (Mathf.Abs(at.x - loopCentre.x) > loopRadius) return;
                highestInLoop = Mathf.Max(highestInLoop, at.y);
                wasOnPlaneB |= player.Plane == TerrainPlane.B;
                wasUpsideDown |= player.IsGrounded && Mathf.Abs(player.GroundAngle) > 150f;
            };
            yield return Pilot(() => player.Position.x > 320f, 90f, watchLoop);

            Assert.Greater(player.Position.x, 320f, "the player ran from the start, over the three hills and through the loop");
            Assert.AreEqual(0, events.Respawns, "without falling out of the level");
            Assert.Greater(highestInLoop, loopCentre.y + loopRadius - 2f * DesignTargets.StandingRadius, "it passed the top of the ring");
            Assert.IsTrue(wasUpsideDown, "running on the ceiling");
            Assert.IsTrue(wasOnPlaneB, "the switch at the top put it on the loop's second half");
            Assert.AreEqual(TerrainPlane.A, player.Plane, "and the switch after the loop put it back");
            CollectionAssert.AreEqual(new[] { SectionId.SoftStart, SectionId.RollingSines, SectionId.FullCycle }, events.Sections, "three sections were announced on the way");
        }

        [UnityTest]
        public IEnumerator AWindUpReleasesIntoABurst()
        {
            yield return StartRun();
            float startX = player.Position.x;
            input.Down = true;
            yield return UntilGameTime(() => false, 0.1f);
            for (int tap = 0; tap < 6; tap++) yield return TapJump();
            Assert.AreEqual(6, events.WindUpTaps, "Down and Jump at rest winds up: one event a tap");
            Assert.AreEqual(0, events.Jumps, "and is not a jump");
            Assert.IsTrue(player.IsCharging);
            Assert.Greater(player.Charge01, 0.9f, "six taps fill the charge");
            Assert.AreEqual(startX, player.Position.x, 0.01f, "on the spot");

            input.Down = false;
            yield return UntilGameTime(() => events.Bursts > 0, 0.5f);
            yield return null;
            Assert.AreEqual(1, events.Bursts, "letting go of Down releases the Burst");
            Assert.Greater(events.LastBurstCharge, 0.9f);
            Assert.IsTrue(player.IsRolling, "the player shoots off tucked");
            Assert.Greater(player.GroundSpeed, 0.85f * DesignTargets.TopSpeed, "at nearly top speed, from a standstill");
        }

        [UnityTest]
        public IEnumerator ASteepSlopeCarriesAPlayerWhoLetsGoAndAGentleOneHoldsIt()
        {
            yield return StartRun();
            float steepest = player.Tuning.standFirmAngle;

            // The middle of Full Cycle's run-up: a downhill of about 29 degrees, steeper than the stand-firm angle.
            SectionInfo fullCycle = Sections.Get(SectionId.FullCycle);
            player.PlaceAt(new Vector2(fullCycle.StartX + 21f, 3f));
            yield return UntilGameTime(() => player.IsGrounded, 2f);
            Assert.Greater(Mathf.Abs(player.GroundAngle), steepest, "the player stands on ground steeper than the stand-firm angle");
            yield return UntilGameTime(() => false, 1f);
            Assert.Greater(player.GroundSpeed, 5f, "with no input at all, the slope carries the player downhill, faster and faster");

            // The rise after the loop: at most 12 degrees.
            player.PlaceAt(new Vector2(fullCycle.StartX + 91f, -2f));
            yield return UntilGameTime(() => player.IsGrounded, 2f);
            yield return UntilGameTime(() => false, 1.5f);
            Assert.Less(Mathf.Abs(player.GroundAngle), steepest, "the player stands on a gentle slope");
            Assert.Greater(Mathf.Abs(player.GroundAngle), 3f, "that is not flat");
            Assert.AreEqual(0f, player.GroundSpeed, 0.01f, "and stays where it is");
        }

        [UnityTest]
        public IEnumerator TurningRoundInsideALoopSwitchLeavesOnThePlaneOfTheWayOut()
        {
            yield return StartRun();
            LevelLayout.GetLoop(0, out Vector2 loopCentre, out float loopRadius);
            PlaneSwitch afterLoop = null;
            foreach (PlaneSwitch candidate in UnityEngine.Object.FindObjectsByType<PlaneSwitch>(FindObjectsSortMode.None))
            {
                float x = candidate.transform.position.x;
                bool isBeyondTheLoop = x > loopCentre.x + loopRadius;
                if (isBeyondTheLoop && (afterLoop == null || x < afterLoop.transform.position.x)) afterLoop = candidate;
            }
            Assert.IsNotNull(afterLoop, "a plane switch stands on the ground after the first loop");
            Vector2 foot = afterLoop.transform.position;

            player.PlaceAt(foot + new Vector2(2.5f, 1f));
            yield return UntilGameTime(() => player.IsGrounded, 2f);
            Assert.AreEqual(TerrainPlane.A, player.Plane);

            input.MoveX = -1f;
            yield return UntilGameTime(() => player.Plane == TerrainPlane.B, 3f);
            Assert.AreEqual(TerrainPlane.B, player.Plane, "walking back into the switch towards the loop gives the loop's second plane");

            input.MoveX = 1f;
            yield return UntilGameTime(() => player.Position.x > foot.x + 2.5f, 4f);
            Assert.Greater(player.Position.x, foot.x + 2f, "the player turned round inside the switch and walked out again");
            Assert.AreEqual(TerrainPlane.A, player.Plane, "and left on the plane of the direction it left in, the one loops are entered on");
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator WithoutRollingTheLowRouteLeadsOnToo()
        {
            yield return StartRun();
            SectionInfo split = Sections.Get(SectionId.SplitSignal);
            player.PlaceAt(new Vector2(split.StartX + 1f, 12.5f));
            float lowestUnderThePlatforms = float.MaxValue;
            Action watchRoute = () =>
            {
                Vector2 at = player.Position;
                if (at.x > 660f && at.x < 695f) lowestUnderThePlatforms = Mathf.Min(lowestUnderThePlatforms, at.y);
            };
            yield return Pilot(() => player.Position.x > split.EndX - 4f, 90f, watchRoute, rolling: false);

            Assert.Less(lowestUnderThePlatforms, 2f, "a runner who does not roll down the hill falls short of the platforms, into the trough");
            Assert.Greater(player.Position.x, split.EndX - 4f, "and still reaches the end of the section");
            Assert.AreEqual(0, events.Respawns, "without being sent back");
            CollectionAssert.Contains(events.Checkpoints, 2, "past the third Tuning Fork");
        }

        // ---- The level's pieces ----------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator AChimeIsCollectedAndRaisesTheEvent()
        {
            yield return StartRun();
            input.MoveX = 1f;
            yield return UntilGameTime(() => events.ChimesCollected >= 5, 6f);
            Assert.AreEqual(5, events.ChimesCollected, "the first line of five Chimes was collected, one event each");
            Assert.AreEqual(5, session.ChimeCount, "and counted by the session");
            Assert.AreEqual(5, events.HighestCombo, "collected in quick succession, the combo climbs: 1, 2, 3, 4, 5");

            int stillThere = 0;
            foreach (Chime chime in UnityEngine.Object.FindObjectsByType<Chime>(FindObjectsSortMode.None))
                if (!chime.IsCollected) stillThere++;
            Assert.AreEqual(level.ChimeCount - 5, stillThere, "collected Chimes are gone from the level");
        }

        [UnityTest]
        public IEnumerator AWooferThrowsThePlayerOntoItsTerrace()
        {
            yield return StartRun();
            Woofer woofer = Leftmost(UnityEngine.Object.FindObjectsByType<Woofer>(FindObjectsSortMode.None));
            Vector2 foot = woofer.transform.position;
            player.PlaceAt(foot + Vector2.up * 1.5f);
            yield return UntilGameTime(() => events.WooferBounces > 0, 1f);
            Assert.AreEqual(1, events.WooferBounces, "falling onto a Woofer fires it");
            Assert.AreEqual(woofer.LaunchVelocity.y, player.Velocity.y, 1.5f, "and replaces the player's velocity with the Woofer's own");
            Assert.IsFalse(player.IsGrounded);
            Assert.Less(player.Speed, 0.5f * DesignTargets.CruiseSpeed, "flying straight up is not \"speed\": the meter, the music and the wind count travel, not height");

            float rising = player.Velocity.y;
            player.Rebound();
            Assert.AreEqual(rising, player.Velocity.y, 0.001f, "a rebound never slows a player that is already rising faster");

            yield return UntilGameTime(() => player.IsGrounded, 3f);
            Assert.IsTrue(player.IsGrounded, "the flight ends on ground");
            Assert.Greater(player.Position.y, foot.y + 4f, "five metres up: on the terrace a jump cannot reach");
            Assert.Greater(player.Position.x, foot.x + 2f, "past its edge, with no input at all");
        }

        [UnityTest]
        public IEnumerator GettingHurtScattersTheChimes()
        {
            yield return StartRun();
            player.AddChimes(5);
            Assert.AreEqual(5, session.ChimeCount);
            StaticBurr burr = Leftmost(UnityEngine.Object.FindObjectsByType<StaticBurr>(FindObjectsSortMode.None));
            player.PlaceAt((Vector2)burr.transform.position + Vector2.up * DesignTargets.StandingRadius);
            yield return UntilGameTime(() => events.Hurts > 0, 1f);

            Assert.AreEqual(1, events.Hurts, "touching Static hurts");
            Assert.AreEqual(5, events.LastScatterCount, "the carried Chimes scatter, and the event says how many");
            Assert.AreEqual(0, session.ChimeCount, "none are carried any more");
            Assert.AreEqual(5, UnityEngine.Object.FindObjectsByType<ScatteredChime>(FindObjectsSortMode.None).Length, "five Chimes fly out");
            Assert.IsTrue(player.IsInvulnerable, "the player blinks for a moment");
            Assert.AreEqual(0, events.Respawns, "a hit with Chimes is not a respawn");

            yield return UntilGameTime(() => false, 1f);
            Assert.AreEqual(1, events.Hurts, "while invulnerable the burr does nothing more");
        }

        [UnityTest]
        public IEnumerator AHitWithoutChimesRespawnsAtTheLastTuningFork()
        {
            yield return StartRun();
            TuningFork fork = Leftmost(UnityEngine.Object.FindObjectsByType<TuningFork>(FindObjectsSortMode.None));
            player.PlaceAt((Vector2)fork.transform.position + Vector2.up);
            yield return UntilGameTime(() => events.Checkpoints.Count > 0, 1f);
            CollectionAssert.AreEqual(new[] { 0 }, events.Checkpoints, "passing a Tuning Fork lights it");
            Assert.IsTrue(fork.IsLit);

            StaticBurr burr = Leftmost(UnityEngine.Object.FindObjectsByType<StaticBurr>(FindObjectsSortMode.None));
            player.PlaceAt((Vector2)burr.transform.position + Vector2.up * DesignTargets.StandingRadius);
            yield return UntilGameTime(() => events.Respawns > 0, 3f);
            Assert.AreEqual(1, events.Respawns, "hit with no Chimes: a respawn");
            Assert.AreEqual(fork.transform.position.x, player.Position.x, 0.5f, "at the fork that was lit");
        }

        [UnityTest]
        public IEnumerator AFallIntoAPitRespawnsInTheSameSection()
        {
            yield return StartRun();
            TuningFork[] forks = UnityEngine.Object.FindObjectsByType<TuningFork>(FindObjectsSortMode.None);
            Array.Sort(forks, (a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
            SectionInfo staticField = Sections.Get(SectionId.StaticField);

            // every gap in the ground of the level (a pit) lies in Static Field, and the second fork stands before them
            TuningFork guard = forks[1];
            Assert.AreEqual(SectionId.StaticField, Sections.At(guard.transform.position.x), "the second Tuning Fork stands in Static Field");
            float firstPitX = FirstGapInTheGround(staticField.StartX, staticField.EndX);
            Assert.Less(guard.transform.position.x, firstPitX, "before its first pit");

            player.PlaceAt((Vector2)guard.transform.position + Vector2.up);
            yield return UntilGameTime(() => guard.IsLit, 1f);
            Assert.IsTrue(guard.IsLit);

            player.PlaceAt(new Vector2(firstPitX + 2f, guard.transform.position.y - 2f));
            yield return UntilGameTime(() => events.Respawns > 0, 8f);
            Assert.AreEqual(1, events.Respawns, "falling into the pit is a respawn");
            Assert.AreEqual(guard.transform.position.x, player.Position.x, 0.5f, "at the fork at the start of the same section, not a section back");
        }

        /// <summary>The first x between two bounds with no ground under it: probes downwards every half metre.</summary>
        static float FirstGapInTheGround(float fromX, float toX)
        {
            const float probeTop = 100f, probeLength = 300f, step = 0.5f;
            for (float x = fromX + step; x < toX; x += step)
            {
                bool ground = false;
                foreach (RaycastHit2D hit in Physics2D.RaycastAll(new Vector2(x, probeTop), Vector2.down, probeLength))
                    ground |= hit.collider.GetComponent<GroundSurface>() != null;
                if (!ground) return x;
            }
            Assert.Fail($"there is no pit between x = {fromX} and x = {toX}");
            return toX;
        }

        [UnityTest]
        public IEnumerator TheGoalIsReachedFromTheLastSection()
        {
            yield return StartRun();
            Chime firstChime = Leftmost(UnityEngine.Object.FindObjectsByType<Chime>(FindObjectsSortMode.None));
            player.PlaceAt(firstChime.transform.position);
            yield return UntilGameTime(() => firstChime.IsCollected, 1f);
            Assert.IsTrue(firstChime.IsCollected);
            player.PlaceAt(new Vector2(Sections.Get(SectionId.TheResonator).StartX + 4f, level.GoalPosition.y + 1f));
            input.MoveX = 1f;
            yield return UntilGameTime(() => events.GoalsReached > 0, 8f);
            Assert.AreEqual(1, events.GoalsReached, "running through the Resonator raises GoalReached");
            Assert.AreEqual(GamePhase.Finished, session.Phase, "and ends the run");
            Assert.IsFalse(player.HasControl, "the controls are taken away");

            float finishedAt = session.ElapsedSeconds;
            yield return UntilGameTime(() => player.Speed < 0.01f, 8f);
            Assert.Less(player.Speed, 0.01f, "the player brakes to a stop by itself");
            Assert.Less(player.Position.x, Sections.LevelEndX, "before the end of the level");
            Assert.AreEqual(finishedAt, session.ElapsedSeconds, 0.001f, "the clock has stopped");

            // Jump on the results panel plays again, in place: no scene reload.
            input.Release();
            yield return UntilGameTime(() => false, 1.5f);
            input.JumpHeld = true;
            yield return UntilGameTime(() => session.Phase == GamePhase.Playing, 1f);
            input.JumpHeld = false;
            yield return UntilGameTime(() => player.IsGrounded, 2f);
            Assert.AreEqual(GamePhase.Playing, session.Phase, "Jump on the results panel starts a new run");
            Assert.AreEqual(2, events.GameStarts, "GameStarted is raised again: the level's cue to put itself back");
            Assert.AreEqual(level.SpawnPoint.x, player.Position.x, 0.5f, "the player is back at the start");
            Assert.AreEqual(0, session.ChimeCount);
            Assert.IsFalse(firstChime.IsCollected, "and the collected Chime is back");
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator TheWholeLevelCanBeRunFromStartToGoal()
        {
            yield return StartRun();
            // What the layout's comments and Chime flights are built on, measured as the pilot passes:
            // the speed off each launch ramp's lip (the last 2 m of ground before it), whether the tucked launch of
            // Split Signal reached the first platform, and the Chimes picked up from Overdrive on.
            const float splitLipX = 650f, finalLipX = 930f, lipLength = 2f;
            const float firstPlatformStartX = 656f, firstPlatformEndX = 672f, firstPlatformY = 6.5f;
            float overdriveStartX = Sections.Get(SectionId.Overdrive).StartX;
            float splitLipSpeed = 0f, finalLipSpeed = 0f;
            Vector2 finalLipVelocity = Vector2.zero;
            bool landedOnFirstPlatform = false;
            int chimesBeforeOverdrive = -1;
            Action measure = () =>
            {
                Vector2 at = player.Position;
                if (chimesBeforeOverdrive < 0 && at.x >= overdriveStartX) chimesBeforeOverdrive = events.ChimesCollected;
                if (!player.IsGrounded) return;
                if (at.x > splitLipX - lipLength && at.x < splitLipX) splitLipSpeed = Mathf.Max(splitLipSpeed, player.GroundSpeed);
                if (at.x > finalLipX - lipLength && at.x < finalLipX && player.GroundSpeed > finalLipSpeed)
                {
                    finalLipSpeed = player.GroundSpeed;
                    finalLipVelocity = player.Velocity;
                }
                landedOnFirstPlatform |= at.x > firstPlatformStartX && at.x < firstPlatformEndX && at.y > firstPlatformY;
            };
            yield return Pilot(() => session.Phase == GamePhase.Finished, 240f, measure);

            int overdriveChimes = events.ChimesCollected - chimesBeforeOverdrive;
            Debug.Log($"SHOWCASE RUN  finished in {session.ElapsedSeconds:0.0} s with {session.ChimeCount} Chimes, top speed {session.TopSpeedReached:0.0} m/s, {events.Hurts} hits, {events.Respawns} respawns");
            Debug.Log($"SHOWCASE RUN  split lip {splitLipSpeed:0.0} m/s, final lip {finalLipSpeed:0.0} m/s as {finalLipVelocity}, {overdriveChimes} Chimes from Overdrive on, first platform {(landedOnFirstPlatform ? "reached" : "missed")}");
            Assert.AreEqual(GamePhase.Finished, session.Phase, $"the pilot reached the goal (it stopped at x = {player.Position.x:0.0}, y = {player.Position.y:0.0})");
            Assert.AreEqual(1, events.GoalsReached);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, events.Checkpoints, "all three Tuning Forks were lit, in order");
            Assert.AreEqual(1, events.FinalLaunches, "the last ramp announced the final launch");
            CollectionAssert.IsSubsetOf((SectionId[])Enum.GetValues(typeof(SectionId)), events.Sections, "every section of the level was entered");
            Assert.Greater(session.TopSpeedReached, 0.9f * DesignTargets.TopSpeed, "somewhere on the way the player was near top speed");
            Assert.LessOrEqual(session.TopSpeedReached, DesignTargets.TopSpeed + 0.001f, "and the top speed the results show is never above the speed cap, whatever the falls");
            Assert.GreaterOrEqual(splitLipSpeed, SplitLipMinimumSpeed, "a tuck down Split Signal's hill leaves the launch ramp fast enough for the high route");
            Assert.IsTrue(landedOnFirstPlatform, "and lands on its first platform");
            Assert.GreaterOrEqual(finalLipSpeed, FinalLipMinimumSpeed, "Overdrive's last ramp is left at the speed its Chime field is laid along");
            Assert.GreaterOrEqual(overdriveChimes, OverdriveMinimumChimes, "and the final flight passes through that field");
            Assert.Greater(events.ChimesCollected, 30, "and picked up Chimes along the route");
        }
    }
}
