using System;
using System.Collections.Generic;
using SfxShowcase.Core;
using UnityEngine;

namespace SfxShowcase.Level
{
    /// <summary>
    /// The level First Harmonic, and its installer. The first half of this file is the level as DATA – one method per
    /// section, every position in metres – and the second half turns that data into GameObjects: visuals from the art
    /// kit, 2D colliders and the small behaviours of this folder. Plain runtime code like the gameplay and audio
    /// installers (no editor calls). To change the level, change the data and rebuild the scene;
    /// <c>Docs/LEVEL-NOTES.md</c> walks through it section by section.
    /// </summary>
    public static class LevelLayout
    {
        // ---- Numbers the whole layout shares -------------------------------------------------------------------

        /// <summary>The player's centre is this far above the ground it stands on.</summary>
        const float PlayerRadius = DesignTargets.StandingRadius;
        /// <summary>Chimes "at running height" hang this far above the ground: a runner's centre passes through them.</summary>
        const float ChimeHeight = 0.8f;
        /// <summary>Distance between the points of a curved piece of ground, metres.</summary>
        const float CurveStep = 0.5f;
        /// <summary>Length of one straight piece of a loop's surface, metres.</summary>
        const float LoopStep = 0.4f;
        /// <summary>How far each half of a loop continues past the top, so the player has ground under it while the switch there changes its plane.</summary>
        const float LoopOverlapDegrees = 25f;
        /// <summary>The wall of a loop outside its running surface, metres (art only).</summary>
        const float LoopWallThickness = 1f;
        /// <summary>The body under a floating platform's surface, metres (art only).</summary>
        const float PlatformThickness = 0.8f;
        /// <summary>How high the middle of a floating platform rises above its ends: each one is half a sine wave.</summary>
        const float PlatformHump = 0.6f;
        /// <summary>The camera shows this much beyond each end of the level, where the boundary walls stand.</summary>
        const float BoundsMarginX = 4f;
        /// <summary>The camera can look this far above the highest ground (Woofer flights go up there).</summary>
        const float BoundsMarginAbove = 22f;
        /// <summary>The camera stops this far above the height at which the player is lost in a pit.</summary>
        const float BoundsMarginAboveKill = 1f;
        /// <summary>The ground's body is drawn down to this far below the kill height, out of the camera's sight.</summary>
        const float GroundDepthBelowKill = 1f;
        /// <summary>The camera's distance in front of the play plane (z = 0).</summary>
        const float CameraDistance = 10f;

        // ---- Trigger sizes: (width, height) in metres, in the object's own space ----------------------------------

        const float ChimeTriggerRadius = 0.45f;
        static readonly Vector2 WooferTriggerSize = new Vector2(1.6f, 0.9f);
        static readonly Vector2 StaticTriggerSize = new Vector2(1.7f, 0.8f);
        static readonly Vector2 ClipperTriggerSize = new Vector2(1.1f, 1.1f);
        static readonly Vector2 TuningForkTriggerSize = new Vector2(1.5f, 14f);
        static readonly Vector2 ResonatorTriggerSize = new Vector2(2f, 30f);
        static readonly Vector2 GroundSwitchSize = new Vector2(1f, 16f);
        static readonly Vector2 LoopTopSwitchSize = new Vector2(1f, 3f);
        static readonly Vector2 FinalLaunchTriggerSize = new Vector2(1.5f, 12f);

        // ---- Measured launches: the Chime flights are laid along these -----------------------------------------------
        // Read from the PlayMode run (Tests: the whole-level run asserts both lip speeds). Measure again after
        // changing the tuning or the ramps.

        /// <summary>The velocity a player tucked from the top of Split Signal's hill leaves the launch ramp with: 16.7 m/s at 38°.</summary>
        static readonly Vector2 SplitLaunchVelocity = new Vector2(13.2f, 10.3f);
        /// <summary>The velocity a runner leaves Overdrive's last ramp with after the valley's Woofer: 18.6 m/s at 29°.</summary>
        static readonly Vector2 FinalLaunchVelocity = new Vector2(16.3f, 8.9f);
        /// <summary>Where the valley's Woofer stands, on the flat before the last ramp.</summary>
        const float FinalWooferX = 904f;

        // =========================================================================================================
        //  THE LEVEL AS DATA
        //  x runs from 0 to 1000 m, y is height. Section boundaries are the ones in Core's Sections table.
        //  Speeds in the comments are for the shipped tuning (cruise 10 m/s, top speed 22 m/s, gravity 30 m/s², a full
        //  jump 3.5 m high: 0.97 s in the air, about 9.7 m long at cruise) and were read from the PlayMode run's log
        //  (Tests, "SHOWCASE RUN" lines). Re-read them there after changing the tuning or the terrain.
        // =========================================================================================================

        static LevelData Describe()
        {
            var level = new LevelData();
            SoftStart(level);
            RollingSines(level);
            FullCycle(level);
            WooferSteps(level);
            StaticField(level);
            SplitSignal(level);
            Overdrive(level);
            TheResonator(level);
            return level;
        }

        /// <summary>0 – 90 m. Running and jumping: flat ground, two low steps, Chime lines, one Chime arc that traces a jump.</summary>
        static void SoftStart(LevelData level)
        {
            level.AddGround("Soft Start", new SurfacePath(-8f, 12f)
                .FlatTo(0f).StepTo(0f)          // the wall that closes the level on the left
                .FlatTo(30f).StepTo(1f)         // low step 1: 1.0 m, must be jumped
                .FlatTo(55f).StepTo(2.5f)       // low step 2: 1.5 m
                .FlatTo(90f));

            level.Spawn = new Vector2(6f, 0f + PlayerRadius);

            level.ChimeLine(12f, 20f, 5);
            // The arc of a full jump at cruise speed, taken 5 m before step 1; it lands on the step near x = 34.
            level.ChimeFlight(new Vector2(25f, 0f + PlayerRadius), new Vector2(DesignTargets.CruiseSpeed, JumpSpeed), 0.15f, 0.75f, 5);
            level.ChimeLine(40f, 48f, 5);
            level.ChimeLine(64f, 76f, 7);
        }

        /// <summary>90 – 220 m. The first slopes: three sine hills, each taller than the last, Chimes in the valleys, one Clipper.</summary>
        static void RollingSines(LevelData level)
        {
            // Steepest slope 30°. A runner arriving at cruise crests every hill; tucked in a valley, the next hill is faster.
            level.AddGround("Rolling Sines", level.StartSection(SectionId.RollingSines)
                .FlatTo(94f)
                .CurveTo(108f, 5.5f)            // hill 1: 3 m up
                .CurveTo(124f, 1.5f)            // valley 1
                .CurveTo(142f, 7.5f)            // hill 2: 6 m up
                .FlatTo(156f)                   // its flat top, where the Clipper walks
                .CurveTo(174f, 1f)              // valley 2
                .CurveTo(206f, 9f)              // hill 3: 8 m up
                .FlatTo(220f));

            level.ChimeLine(116f, 132f, 9);     // through valley 1, along the line a tucked player rolls
            level.ChimeLine(164f, 184f, 11);    // through valley 2
            level.AddClipper(149f, 3f);         // reached at or below cruise speed, after the climb; easy to jump
        }

        /// <summary>220 – 340 m. The first loop: a 15 m downhill run-up, the loop, a gentle rise, Tuning Fork 1.</summary>
        static void FullCycle(LevelData level)
        {
            level.AddGround("Full Cycle", level.StartSection(SectionId.FullCycle)
                .CurveTo(262f, -6f)             // run-up, 15 m down: top speed at the foot when tucked from the top
                .FlatTo(292f)                   // the loop stands on this flat, its foot at x = 272
                .CurveTo(330f, -1f)             // 5 m rise, at most 12° steep: bleeds about 2 m/s
                .FlatTo(340f));

            // Radius 6 m: the centre climbs 11 m. TUCKED it needs about 17 m/s at the foot (a full Wind-Up Burst from
            // standing there, 21 m/s, clears it). RUNNING and holding forward it is far more forgiving: the run
            // acceleration works on walls too, so anything above about 3.5 m/s at the foot gets round. Only a tucked or
            // coasting player who enters too slowly falls off the wall.
            LoopPiece loop = level.AddLoop(272f, 6f);
            level.AddPlaneSwitch(262f, TerrainPlane.A, TerrainPlane.A);
            level.AddPlaneSwitch(281.5f, TerrainPlane.A, TerrainPlane.B);

            level.ChimeLine(224f, 248f, 9);     // down the run-up
            level.ChimeCircle(loop.Center, loop.Radius - ChimeHeight, 12);
            level.ChimeLine(300f, 320f, 6);     // up the rise
            level.AddTuningFork(335f);
        }

        /// <summary>340 – 460 m. Woofers: three terraces too tall to jump, a Woofer at the foot of each, a diagonal one over a dip.</summary>
        static void WooferSteps(LevelData level)
        {
            level.AddGround("Woofer Steps", level.StartSection(SectionId.WooferSteps)
                .FlatTo(362f).StepTo(4f)        // terrace 1: 5 m up (a jump is 3.5 m)
                .FlatTo(388f).StepTo(9f)        // terrace 2
                .FlatTo(414f).StepTo(14f)       // terrace 3, the highest ground of the level
                .FlatTo(446f).StepTo(8f)        // the dip the diagonal Woofer flies over
                .FlatTo(449f)
                .SlopeTo(460f, 11f));           // 15° way out of the dip for whoever walks off the edge instead

            // Each terrace Woofer stands 2 m before its wall and leans towards it: 7.3 m up, landing 2.4 m past the
            // edge with no input at all.
            var terraceLaunch = new Vector2(4f, 21f);
            level.ChimesAbove(level.AddWoofer(360f, terraceLaunch), 0.1f, 0.4f, 4);
            level.ChimesAbove(level.AddWoofer(386f, terraceLaunch), 0.1f, 0.4f, 4);
            level.ChimesAbove(level.AddWoofer(412f, terraceLaunch), 0.1f, 0.4f, 4);
            // The diagonal one: 1.2 s in the air, over the dip, landing near x = 461 at 16 m/s sideways.
            level.ChimesAbove(level.AddWoofer(443f, new Vector2(16f, 15f)), 0.15f, 0.9f, 6);
        }

        /// <summary>460 – 600 m. The hazard stretch, behind Tuning Fork 2: saw-tooth ridges with Static below their drops, four Clippers, two pits.</summary>
        static void StaticField(LevelData level)
        {
            // Tuning Fork 2 stands on the landing flat, BEFORE the hazards: every pit of the level and its densest
            // stretch of hazards lie behind it, so a fall costs this section and not Woofer Steps as well.
            // Every tooth is an 8 m ramp (14°) rising 2 m with a sheer 2 m drop back to the flat; the Static sits
            // below the drop, where a runner who simply runs off the edge at cruise comes down. The first one is 23 m
            // after the landing from Woofer Steps. Pits are 4 m wide: a jump clears them from 5 m/s.
            level.AddGround("Static Field A", level.StartSection(SectionId.StaticField)
                .FlatTo(474f).SlopeTo(482f, 13f).StepTo(11f)    // tooth 1, its drop at 482
                .FlatTo(504f),                                  // pit 1: 504 – 508
                wallAtStart: false, wallAtEnd: true);
            level.AddGround("Static Field B", new SurfacePath(508f, 11f)
                .FlatTo(516f).SlopeTo(524f, 13f).StepTo(11f)    // tooth 2, its drop at 524
                .FlatTo(529f).SlopeTo(537f, 13f).StepTo(11f)    // tooth 3 straight after it, its drop at 537
                .FlatTo(556f),                                  // pit 2: 556 – 560
                wallAtStart: true, wallAtEnd: true);
            level.AddGround("Static Field C", new SurfacePath(560f, 11f)
                .FlatTo(574f).SlopeTo(582f, 13f).StepTo(11f)    // tooth 4, its drop at 582
                .FlatTo(600f),
                wallAtStart: true, wallAtEnd: false);

            level.AddTuningFork(466f);

            level.AddStatic(484.5f);
            level.AddStatic(526.5f);
            level.AddStatic(539.5f);
            level.AddStatic(584.5f);

            level.AddClipper(495f, 3f);
            level.AddClipper(549f, 3f);
            level.AddClipper(567f, 3f);
            level.AddClipper(591f, 2f);         // the last guard of the section

            // The safe line: an arc over every drop and every pit. (The arcs over the drops are symmetric and only
            // roughly follow a jump taken on the ramp, which is thrown back by the slope: about one Chime in three is
            // on the pilot's line.)
            level.ChimeArcOverGround(480f, 488.5f, 3.5f, 3);
            level.ChimeArcOverGround(503f, 509f, 3f, 3);
            level.ChimeArcOverGround(522f, 530.5f, 3.5f, 3);
            level.ChimeArcOverGround(535f, 543.5f, 3.5f, 3);
            level.ChimeArcOverGround(555f, 561f, 3f, 3);
            level.ChimeArcOverGround(580f, 588.5f, 3.5f, 3);
        }

        /// <summary>600 – 800 m. Tuning Fork 3, then a launch ramp splits the level: floating platforms above (fast, no enemies), a trough below (busy, fair).</summary>
        static void SplitSignal(LevelData level)
        {
            level.AddGround("Split Signal", level.StartSection(SectionId.SplitSignal)
                .FlatTo(610f)
                .CurveTo(636f, 2f)              // 9 m down: tucked from the top it gives about 19 m/s at the bottom
                .FlatTo(642f)
                .RampTo(650f, 6f)               // launch ramp, 38° at the lip: about 16.5 m/s off it tucked, 10 m/s running
                .StepTo(3.5f).FlatTo(653.5f)    // a ledge under the lip with the second-chance Woofer
                .StepTo(0f)                     // the low route's floor
                .FlatTo(726f).StepTo(1.5f)      // a low terrace in the trough (jumpable)
                .FlatTo(746f).StepTo(0f)
                .FlatTo(770f)
                .CurveTo(788f, 3f)              // the routes rejoin
                .FlatTo(800f));

            // Tuning Fork 3 at the start, before the hill: whoever falls in the trough gets another try at the high route.
            level.AddTuningFork(604f);

            // HIGH ROUTE. Leaving the lip at about 14.5 m/s or more lands on the first platform – a tuck from the top
            // of the hill does it with 1.5 m/s to spare; slower falls short of it and past the ledge, into the low
            // route. Gaps are 5 – 6 m: a jump at cruise carries 9.7 m.
            PlatformPiece first = level.AddPlatform(656f, 672f, 6.5f);
            PlatformPiece second = level.AddPlatform(677f, 693f, 7f);
            PlatformPiece third = level.AddPlatform(699f, 715f, 6.5f);
            PlatformPiece fourth = level.AddPlatform(721f, 737f, 7f);
            PlatformPiece fifth = level.AddPlatform(743f, 759f, 6.5f);
            PlatformPiece last = level.AddPlatform(765f, 779f, 5.5f);    // running off its end lands on the rejoining slope

            // The flight of a tucked arrival, as measured: 16.7 m/s off the 38° lip.
            level.ChimeFlight(new Vector2(650f, 6f + PlayerRadius), SplitLaunchVelocity, 0.15f, 0.6f, 4);
            level.ChimeArcBetween(first, second, 2f, 3);
            level.ChimeArcBetween(second, third, 2f, 3);
            level.ChimeArcBetween(third, fourth, 2f, 3);
            level.ChimeArcBetween(fourth, fifth, 2f, 3);
            level.ChimeArcBetween(fifth, last, 2f, 3);

            // Whoever only runs off the lip (without the tuck) drops onto this Woofer: up to platform 1.
            level.AddWoofer(651.8f, new Vector2(9f, 16f));

            // LOW ROUTE. Arrivals land between x = 655 and 665 above cruise speed; the first hazard is 14 m further on.
            level.ChimeLine(660f, 668f, 3);
            level.AddClipper(682f, 3f);
            level.AddStatic(691f);
            // The second chance at x = 700: 7.7 m up, through the third platform from below and down onto it.
            level.ChimesAbove(level.AddWoofer(700f, new Vector2(4f, 21.5f)), 0.1f, 0.25f, 3);
            level.AddClipper(714f, 3f);
            level.AddStatic(736f);              // on the low terrace
            level.ChimeArcOverGround(733.5f, 738.5f, 2.2f, 3);
            level.AddClipper(758f, 3f);
        }

        /// <summary>800 – 960 m. The speed finale: a horizontal Woofer, a 12 m downhill, two loops back to back, a valley with a second horizontal Woofer and the launch ramp.</summary>
        static void Overdrive(LevelData level)
        {
            level.AddGround("Overdrive", level.StartSection(SectionId.Overdrive)
                .FlatTo(806f)
                .CurveTo(838f, -9f)             // 12 m down from 20 m/s: top speed before the bottom
                .FlatTo(888f)                   // both loops stand on this flat
                .CurveTo(900f, -12f)            // the final valley
                .FlatTo(910f)                   // the second horizontal Woofer stands here
                .RampTo(930f, -5f)              // launch ramp, 7 m up, 29° at the lip: about 18.5 m/s off it
                .StepTo(-10f)                   // the landing strip, 5 m below the lip
                .FlatTo(960f));

            // Fires along the ground: 20 m/s, so even a walker arrives at the loops with speed to spare.
            level.AddWoofer(new Vector2(803f, 3f + PlayerRadius), new Vector2(20f, 0f));

            // Both are entered near top speed; tucked, radius 6 m needs 17 m/s at the foot and radius 7 m about 18.5.
            level.AddLoop(850f, 6f);
            level.AddLoop(870f, 7f);
            level.AddPlaneSwitch(840f, TerrainPlane.A, TerrainPlane.A);
            level.AddPlaneSwitch(859.5f, TerrainPlane.A, TerrainPlane.B);     // after loop 1 = before loop 2
            level.AddPlaneSwitch(880.5f, TerrainPlane.A, TerrainPlane.B);

            // The loops and the drag above cruise speed leave about 15 m/s in the valley, and the ramp is 7 m tall:
            // without help the lip would be reached at cruise speed. This Woofer gives top speed again for the launch.
            level.AddWoofer(new Vector2(FinalWooferX, level.GroundAt(FinalWooferX) + PlayerRadius), new Vector2(DesignTargets.TopSpeed, 0f));

            // The field of Chimes under the last flight: three rows along the measured launch, which lands near
            // x = 945. The middle row is the flight itself, the lower one a slower launch's, the upper one a jump's
            // off the lip.
            level.FinalLaunchLip = new Vector2(930f, -5f);
            Vector2 lipCentre = level.FinalLaunchLip + Vector2.up * PlayerRadius;
            level.ChimeFlight(lipCentre + Vector2.down * 1.2f, FinalLaunchVelocity, 0.15f, 0.85f, 5);
            level.ChimeFlight(lipCentre, FinalLaunchVelocity, 0.15f, 0.85f, 5);
            level.ChimeFlight(lipCentre + Vector2.up * 1.2f, FinalLaunchVelocity, 0.15f, 0.85f, 5);
        }

        /// <summary>960 – 1000 m. The goal: a flat strip, the Resonator at x = 980, 20 m to stop in.</summary>
        static void TheResonator(LevelData level)
        {
            level.AddGround("The Resonator", level.StartSection(SectionId.TheResonator)
                .FlatTo(1000f).StepTo(2f)       // the wall that closes the level on the right
                .FlatTo(1008f));

            level.Goal = level.OnGround(980f);
        }

        /// <summary>The upward speed of a full jump, m/s.</summary>
        static float JumpSpeed => DesignTargets.LaunchSpeedForHeight(DesignTargets.JumpHeight);

        /// <summary>
        /// How many of each piece the layout places. The scene builder's self-test compares the built scene with this,
        /// so adding a Clipper or a platform to the data above needs no second number changed anywhere.
        /// </summary>
        public readonly struct PieceCounts
        {
            /// <summary>Chimes placed in the level.</summary>
            public readonly int Chimes;
            /// <summary>Woofers.</summary>
            public readonly int Woofers;
            /// <summary>Static burrs.</summary>
            public readonly int Statics;
            /// <summary>Clippers.</summary>
            public readonly int Clippers;
            /// <summary>Tuning Forks.</summary>
            public readonly int TuningForks;
            /// <summary>Loops; each has a half on plane A, a half on plane B and a switch at its top.</summary>
            public readonly int Loops;
            /// <summary>Floating one-way platforms.</summary>
            public readonly int Platforms;
            /// <summary>Plane switches: the ones standing on the ground plus one at the top of every loop.</summary>
            public readonly int PlaneSwitches;

            /// <summary>Creates the counts.</summary>
            public PieceCounts(int chimes, int woofers, int statics, int clippers, int tuningForks, int loops, int platforms, int planeSwitches)
            {
                Chimes = chimes; Woofers = woofers; Statics = statics; Clippers = clippers; TuningForks = tuningForks;
                Loops = loops; Platforms = platforms; PlaneSwitches = planeSwitches;
            }
        }

        /// <summary>Counts what the level's data places, without building anything.</summary>
        public static PieceCounts CountPieces()
        {
            LevelData level = Describe();
            return new PieceCounts(level.Chimes.Count, level.Woofers.Count, level.Statics.Count, level.Clippers.Count,
                level.TuningForks.Count, level.Loops.Count, level.Platforms.Count, level.PlaneSwitches.Count + level.Loops.Count);
        }

        /// <summary>The circle of a loop's running surface, by its order in the level (0 = the first): for tests that ask "did the player go over the top?".</summary>
        public static void GetLoop(int index, out Vector2 centre, out float radius)
        {
            LoopPiece loop = Describe().Loops[index];
            centre = loop.Center;
            radius = loop.Radius;
        }

        // =========================================================================================================
        //  BUILDING THE SCENE FROM THE DATA
        // =========================================================================================================

        /// <summary>
        /// Builds the level into the open scene: the <c>Level</c> root with the terrain (visual, collider and
        /// <see cref="GroundSurface"/>), every placed object, the loop switches, the section and launch triggers and a
        /// filled <see cref="LevelDescriptor"/>. Needs <see cref="ShowcaseContext.Art"/>; sets
        /// <see cref="ShowcaseContext.Level"/>.
        /// </summary>
        public static void Build(ShowcaseContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.Art == null) throw new InvalidOperationException("LevelLayout.Build needs context.Art: create the art kit first.");

            ArtKitBase art = context.Art;
            LevelData level = Describe();
            float killY = level.LowestGround - DesignTargets.KillDepth;
            var bounds = Rect.MinMaxRect(
                Sections.LevelStartX - BoundsMarginX, killY + BoundsMarginAboveKill,
                Sections.LevelEndX + BoundsMarginX, level.HighestGround + BoundsMarginAbove);

            Transform root = new GameObject("Level").transform;
            BuildTerrain(level, art, Group("Terrain", root), killY - GroundDepthBelowKill);
            BuildPlatforms(level, art, Group("Platforms", root));
            BuildLoops(level, art, Group("Loops", root));
            BuildPlaneSwitches(level, Group("Plane Switches", root));
            BuildChimes(level, art, Group("Chimes", root));
            BuildWoofers(level, art, Group("Woofers", root));
            BuildStatics(level, art, Group("Static", root));
            BuildClippers(level, art, Group("Clippers", root));
            Vector2[] checkpoints = BuildTuningForks(level, art, Group("Tuning Forks", root));
            BuildResonator(level, art, root);
            BuildSectionTrigger(level, bounds, root);
            BuildFinalLaunchTrigger(level, root);

            var descriptor = root.gameObject.AddComponent<LevelDescriptor>();
            descriptor.Configure(level.Spawn, bounds, killY, level.Goal, checkpoints, level.Chimes.Count);
            context.Level = descriptor;
        }

        /// <summary>
        /// Creates <c>Main Camera</c>: orthographic, tagged MainCamera, with the scene's AudioListener and the
        /// <see cref="FollowCamera"/>, looking at the spawn point. Needs <see cref="ShowcaseContext.Player"/> and
        /// <see cref="ShowcaseContext.Level"/>; sets <see cref="ShowcaseContext.Camera"/>.
        /// </summary>
        public static void BuildCamera(ShowcaseContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (context.Player == null || context.Level == null)
                throw new InvalidOperationException("LevelLayout.BuildCamera needs context.Player and context.Level: build the level and install the gameplay first.");

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            // The art kit's background covers the whole view; a solid clear keeps a skybox from ever showing through.
            camera.clearFlags = CameraClearFlags.SolidColor;
            cameraObject.AddComponent<AudioListener>();

            var follow = cameraObject.AddComponent<FollowCamera>();
            follow.Configure(context.Player, context.Level, camera);
            Vector2 centre = follow.RestingViewCentre(context.Level.SpawnPoint);
            cameraObject.transform.position = new Vector3(centre.x, centre.y, -CameraDistance);
            context.Camera = camera;
        }

        // ---- Collider conventions -------------------------------------------------------------------------------
        // The contract with the movement code (ARCHITECTURE.md 3.2) lives in these three methods and nowhere else:
        // ground is a non-trigger EdgeCollider2D with a GroundSurface beside it; everything else is a trigger.

        static void AddGroundCollider(GameObject target, IReadOnlyList<Vector2> surface, TerrainPlane plane, bool oneWay)
        {
            var points = new Vector2[surface.Count];
            for (int i = 0; i < points.Length; i++) points[i] = surface[i];
            target.AddComponent<EdgeCollider2D>().points = points;
            target.AddComponent<GroundSurface>().Configure(plane, oneWay);
        }

        /// <summary>A box trigger standing on the object's origin: it reaches from local y = 0 up to the box's height.</summary>
        static BoxCollider2D AddStandingTrigger(GameObject target, Vector2 size)
        {
            var box = target.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = size;
            box.offset = new Vector2(0f, size.y * 0.5f);
            return box;
        }

        static CircleCollider2D AddRoundTrigger(GameObject target, float radius)
        {
            var circle = target.AddComponent<CircleCollider2D>();
            circle.isTrigger = true;
            circle.radius = radius;
            return circle;
        }

        // ---- Terrain ---------------------------------------------------------------------------------------------

        static void BuildTerrain(LevelData level, ArtKitBase art, Transform parent, float bottomY)
        {
            foreach (GroundPiece piece in level.Ground)
            {
                GameObject ground = Piece(piece.Name, parent, Vector2.zero);
                Attach(art.CreateGround(piece.Surface, bottomY), ground.transform);
                AddGroundCollider(ground, WithPitWalls(piece, bottomY), TerrainPlane.Both, false);
            }
        }

        /// <summary>The collider's path: the surface, continued straight down at an end that borders a pit.</summary>
        static List<Vector2> WithPitWalls(GroundPiece piece, float bottomY)
        {
            var path = new List<Vector2>(piece.Surface.Count + 2);
            Vector2 start = piece.Surface[0], end = piece.Surface[piece.Surface.Count - 1];
            if (piece.WallAtStart) path.Add(new Vector2(start.x, bottomY));
            path.AddRange(piece.Surface);
            if (piece.WallAtEnd) path.Add(new Vector2(end.x, bottomY));
            return path;
        }

        static void BuildPlatforms(LevelData level, ArtKitBase art, Transform parent)
        {
            for (int i = 0; i < level.Platforms.Count; i++)
            {
                PlatformPiece piece = level.Platforms[i];
                GameObject platform = Piece($"Platform {i + 1}", parent, Vector2.zero);
                Attach(art.CreatePlatform(piece.Surface, PlatformThickness), platform.transform);
                AddGroundCollider(platform, piece.Surface, TerrainPlane.Both, true);
            }
        }

        /// <summary>
        /// A loop is two arcs of ground. The right half (foot, up the right side, over the top) is solid on plane A, the
        /// plane the player arrives on; the left half (top, down the left side, foot) on plane B. The switch at the top
        /// moves the player from one to the other. The flat ground runs on underneath, solid on both planes.
        /// </summary>
        static void BuildLoops(LevelData level, ArtKitBase art, Transform parent)
        {
            for (int i = 0; i < level.Loops.Count; i++)
            {
                LoopPiece piece = level.Loops[i];
                GameObject loop = Piece($"Loop {i + 1}", parent, Vector2.zero);
                Attach(art.CreateLoop(piece.Center, piece.Radius, LoopWallThickness), loop.transform);

                GameObject rightHalf = Piece("Right Half (plane A)", loop.transform, Vector2.zero);
                AddGroundCollider(rightHalf, Arc(piece, -90f, 90f + LoopOverlapDegrees), TerrainPlane.A, false);
                GameObject leftHalf = Piece("Left Half (plane B)", loop.transform, Vector2.zero);
                AddGroundCollider(leftHalf, Arc(piece, 90f - LoopOverlapDegrees, 270f), TerrainPlane.B, false);

                // Just under the top of the ring: the player's centre passes through it, going left on the way round.
                Vector2 top = piece.Center + Vector2.up * piece.Radius;
                GameObject topSwitch = Piece("Top Switch", loop.transform, top + Vector2.down * LoopTopSwitchSize.y);
                AddStandingTrigger(topSwitch, LoopTopSwitchSize);
                topSwitch.AddComponent<PlaneSwitch>().Configure(TerrainPlane.A, TerrainPlane.B);
            }
        }

        /// <summary>Points on a loop's running surface between two angles (degrees, counter-clockwise from +x).</summary>
        static List<Vector2> Arc(LoopPiece loop, float fromDegrees, float toDegrees)
        {
            float arcLength = Mathf.Abs(toDegrees - fromDegrees) * Mathf.Deg2Rad * loop.Radius;
            int steps = Mathf.CeilToInt(arcLength / LoopStep);
            var points = new List<Vector2>(steps + 1);
            for (int i = 0; i <= steps; i++)
            {
                float radians = Mathf.Lerp(fromDegrees, toDegrees, i / (float)steps) * Mathf.Deg2Rad;
                points.Add(loop.Center + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * loop.Radius);
            }
            return points;
        }

        static void BuildPlaneSwitches(LevelData level, Transform parent)
        {
            foreach (PlaneSwitchPlacement placement in level.PlaneSwitches)
            {
                GameObject planeSwitch = Piece($"Plane Switch x={placement.Position.x:0.#}", parent, placement.Position);
                AddStandingTrigger(planeSwitch, GroundSwitchSize);
                planeSwitch.AddComponent<PlaneSwitch>().Configure(placement.WhenMovingRight, placement.WhenMovingLeft);
            }
        }

        // ---- Placed objects --------------------------------------------------------------------------------------

        static void BuildChimes(LevelData level, ArtKitBase art, Transform parent)
        {
            for (int i = 0; i < level.Chimes.Count; i++)
            {
                GameObject chime = Piece($"Chime {i + 1}", parent, level.Chimes[i]);
                GameObject visual = Attach(art.CreateChime(), chime.transform);
                chime.AddComponent<Chime>().Configure(art, visual, AddRoundTrigger(chime, ChimeTriggerRadius));
            }
        }

        static void BuildWoofers(LevelData level, ArtKitBase art, Transform parent)
        {
            for (int i = 0; i < level.Woofers.Count; i++)
            {
                WooferPlacement placement = level.Woofers[i];
                GameObject woofer = Piece($"Woofer {i + 1}", parent, placement.BasePosition);
                // The art's Woofer fires along its local +y: turn the whole object so that is the launch direction.
                float angle = Vector2.SignedAngle(Vector2.up, placement.LaunchVelocity);
                woofer.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                PropVisual visual = art.CreateWoofer();
                Attach(visual.gameObject, woofer.transform);
                AddStandingTrigger(woofer, WooferTriggerSize);
                woofer.AddComponent<Woofer>().Configure(placement.LaunchVelocity, visual);
            }
        }

        static void BuildStatics(LevelData level, ArtKitBase art, Transform parent)
        {
            for (int i = 0; i < level.Statics.Count; i++)
            {
                GameObject burr = Piece($"Static {i + 1}", parent, level.Statics[i]);
                Attach(art.CreateStatic(i), burr.transform);
                AddStandingTrigger(burr, StaticTriggerSize);
                burr.AddComponent<StaticBurr>();
            }
        }

        static void BuildClippers(LevelData level, ArtKitBase art, Transform parent)
        {
            for (int i = 0; i < level.Clippers.Count; i++)
            {
                ClipperPlacement placement = level.Clippers[i];
                GameObject clipper = Piece($"Clipper {i + 1}", parent, placement.Position);
                GameObject visual = Attach(art.CreateClipper(), clipper.transform);
                // It moves, so it carries a kinematic body: a moving trigger without one is rebuilt by the physics every step.
                clipper.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
                BoxCollider2D trigger = AddStandingTrigger(clipper, ClipperTriggerSize);
                clipper.AddComponent<Clipper>().Configure(art, visual.transform, trigger, placement.PatrolHalfRange);
            }
        }

        /// <summary>Builds the forks in level order and returns their respawn points (the index is the fork's index).</summary>
        static Vector2[] BuildTuningForks(LevelData level, ArtKitBase art, Transform parent)
        {
            var respawnPoints = new Vector2[level.TuningForks.Count];
            for (int i = 0; i < respawnPoints.Length; i++)
            {
                Vector2 foot = level.TuningForks[i];
                respawnPoints[i] = foot + Vector2.up * PlayerRadius;
                GameObject fork = Piece($"Tuning Fork {i + 1}", parent, foot);
                PropVisual visual = art.CreateTuningFork();
                Attach(visual.gameObject, fork.transform);
                AddStandingTrigger(fork, TuningForkTriggerSize);
                fork.AddComponent<TuningFork>().Configure(i, respawnPoints[i], visual);
            }
            return respawnPoints;
        }

        static void BuildResonator(LevelData level, ArtKitBase art, Transform parent)
        {
            GameObject resonator = Piece("Resonator", parent, level.Goal);
            PropVisual visual = art.CreateResonator();
            Attach(visual.gameObject, resonator.transform);
            AddStandingTrigger(resonator, ResonatorTriggerSize);
            resonator.AddComponent<Resonator>().Configure(visual);
        }

        // ---- Triggers that only raise events ---------------------------------------------------------------------

        /// <summary>One volume over the whole level, taller and deeper than the camera's bounds so a falling player stays inside it.</summary>
        static void BuildSectionTrigger(LevelData level, Rect bounds, Transform parent)
        {
            GameObject sections = Piece("Section Trigger", parent, new Vector2(bounds.center.x, bounds.yMin - bounds.height));
            AddStandingTrigger(sections, new Vector2(bounds.width * 2f, bounds.height * 3f));
            sections.AddComponent<SectionTrigger>().Configure(Sections.At(level.Spawn.x));
        }

        /// <summary>A tall strip just past the lip of Overdrive's launch ramp: the player crosses it as it leaves the ground.</summary>
        static void BuildFinalLaunchTrigger(LevelData level, Transform parent)
        {
            Vector2 foot = level.FinalLaunchLip + new Vector2(FinalLaunchTriggerSize.x * 0.5f, -FinalLaunchTriggerSize.y * 0.25f);
            GameObject trigger = Piece("Final Launch Trigger", parent, foot);
            AddStandingTrigger(trigger, FinalLaunchTriggerSize);
            trigger.AddComponent<FinalLaunchTrigger>();
        }

        // ---- Small construction helpers ---------------------------------------------------------------------------

        static Transform Group(string name, Transform parent) => Piece(name, parent, Vector2.zero).transform;

        static GameObject Piece(string name, Transform parent, Vector2 position)
        {
            var piece = new GameObject(name);
            piece.transform.SetParent(parent, false);
            piece.transform.position = new Vector3(position.x, position.y, 0f);
            return piece;
        }

        /// <summary>Puts a visual from the art kit under the object it belongs to, at that object's origin.</summary>
        static GameObject Attach(GameObject visual, Transform parent)
        {
            visual.transform.SetParent(parent, false);
            return visual;
        }

        // =========================================================================================================
        //  THE DATA TYPES
        // =========================================================================================================

        /// <summary>
        /// A ground surface drawn from left to right like a pen line: every call continues from where the last one
        /// ended. Flat, Slope and Step are straight; Curve, Ramp and Hump are the level's waveforms.
        /// </summary>
        sealed class SurfacePath
        {
            readonly List<Vector2> points = new List<Vector2>();

            public SurfacePath(float x, float y)
            {
                points.Add(new Vector2(x, y));
            }

            public IReadOnlyList<Vector2> Points => points;

            Vector2 End => points[points.Count - 1];

            /// <summary>Level ground to x.</summary>
            public SurfacePath FlatTo(float x) => SlopeTo(x, End.y);

            /// <summary>A straight slope to (x, y): the flank of a saw-tooth.</summary>
            public SurfacePath SlopeTo(float x, float y)
            {
                RequireForward(x);
                points.Add(new Vector2(x, y));
                return this;
            }

            /// <summary>A sheer wall up or down to y: the edge of a square wave.</summary>
            public SurfacePath StepTo(float y)
            {
                points.Add(new Vector2(End.x, y));
                return this;
            }

            /// <summary>Half a sine wave to (x, y), level at both ends: the flank of a hill.</summary>
            public SurfacePath CurveTo(float x, float y) => Sample(x, t => Mathf.Lerp(End.y, y, (1f - Mathf.Cos(Mathf.PI * t)) * 0.5f));

            /// <summary>A quarter sine wave to (x, y), level at its start and steepest at its end: a launch ramp.</summary>
            public SurfacePath RampTo(float x, float y) => Sample(x, t => Mathf.Lerp(End.y, y, 1f - Mathf.Cos(Mathf.PI * 0.5f * t)));

            /// <summary>Half a sine wave that rises by height and comes back to the starting height at x: a floating platform.</summary>
            public SurfacePath HumpTo(float x, float height) => Sample(x, t => End.y + height * Mathf.Sin(Mathf.PI * t));

            SurfacePath Sample(float x, Func<float, float> heightAt)
            {
                RequireForward(x);
                Vector2 start = End;
                int steps = Mathf.CeilToInt((x - start.x) / CurveStep);
                var sampled = new Vector2[steps];
                // heightAt reads End, so every height is taken before the first new point is added.
                for (int i = 1; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    sampled[i - 1] = new Vector2(Mathf.Lerp(start.x, x, t), heightAt(t));
                }
                points.AddRange(sampled);
                return this;
            }

            void RequireForward(float x)
            {
                if (x <= End.x) throw new InvalidOperationException($"A surface runs left to right: x = {x} is not beyond {End.x}.");
            }
        }

        /// <summary>A strip of ground. A wall flag closes that end of the collider down the side of a pit.</summary>
        readonly struct GroundPiece
        {
            public readonly string Name;
            public readonly IReadOnlyList<Vector2> Surface;
            public readonly bool WallAtStart, WallAtEnd;

            public GroundPiece(string name, IReadOnlyList<Vector2> surface, bool wallAtStart, bool wallAtEnd)
            {
                Name = name; Surface = surface; WallAtStart = wallAtStart; WallAtEnd = wallAtEnd;
            }
        }

        /// <summary>A floating one-way platform.</summary>
        readonly struct PlatformPiece
        {
            public readonly IReadOnlyList<Vector2> Surface;

            public PlatformPiece(IReadOnlyList<Vector2> surface)
            {
                Surface = surface;
            }

            public Vector2 Start => Surface[0];
            public Vector2 End => Surface[Surface.Count - 1];
        }

        /// <summary>A loop: the circle of its running surface.</summary>
        readonly struct LoopPiece
        {
            public readonly Vector2 Center;
            public readonly float Radius;

            public LoopPiece(Vector2 center, float radius)
            {
                Center = center; Radius = radius;
            }
        }

        /// <summary>A Woofer: where its base is and the velocity it gives.</summary>
        readonly struct WooferPlacement
        {
            public readonly Vector2 BasePosition, LaunchVelocity;

            public WooferPlacement(Vector2 basePosition, Vector2 launchVelocity)
            {
                BasePosition = basePosition; LaunchVelocity = launchVelocity;
            }
        }

        /// <summary>A Clipper: where it stands and how far it walks to each side.</summary>
        readonly struct ClipperPlacement
        {
            public readonly Vector2 Position;
            public readonly float PatrolHalfRange;

            public ClipperPlacement(Vector2 position, float patrolHalfRange)
            {
                Position = position; PatrolHalfRange = patrolHalfRange;
            }
        }

        /// <summary>A loop switch standing on the ground: the plane it gives for each direction of travel.</summary>
        readonly struct PlaneSwitchPlacement
        {
            public readonly Vector2 Position;
            public readonly TerrainPlane WhenMovingRight, WhenMovingLeft;

            public PlaneSwitchPlacement(Vector2 position, TerrainPlane whenMovingRight, TerrainPlane whenMovingLeft)
            {
                Position = position; WhenMovingRight = whenMovingRight; WhenMovingLeft = whenMovingLeft;
            }
        }

        /// <summary>
        /// Everything in the level, as lists of positions. The Add methods take an x and stand the object on the ground
        /// there, so nothing floats or sinks when the terrain is edited; the Chime methods place whole lines and arcs.
        /// </summary>
        sealed class LevelData
        {
            public readonly List<GroundPiece> Ground = new List<GroundPiece>();
            public readonly List<PlatformPiece> Platforms = new List<PlatformPiece>();
            public readonly List<LoopPiece> Loops = new List<LoopPiece>();
            public readonly List<PlaneSwitchPlacement> PlaneSwitches = new List<PlaneSwitchPlacement>();
            public readonly List<Vector2> Chimes = new List<Vector2>();
            public readonly List<WooferPlacement> Woofers = new List<WooferPlacement>();
            public readonly List<Vector2> Statics = new List<Vector2>();
            public readonly List<ClipperPlacement> Clippers = new List<ClipperPlacement>();
            public readonly List<Vector2> TuningForks = new List<Vector2>();

            /// <summary>Where the player's centre is when a run starts.</summary>
            public Vector2 Spawn;
            /// <summary>The ground between the Resonator's prongs.</summary>
            public Vector2 Goal;
            /// <summary>The top of Overdrive's launch ramp.</summary>
            public Vector2 FinalLaunchLip;

            /// <summary>The height of the lowest point of the ground.</summary>
            public float LowestGround { get; private set; } = float.MaxValue;
            /// <summary>The height of the highest point of the ground.</summary>
            public float HighestGround { get; private set; } = float.MinValue;

            // ---- Terrain ----

            /// <summary>
            /// The pen for a section's ground: it starts where the ground so far ends, so consecutive sections always
            /// meet, and that must be the x the <see cref="Sections"/> table gives for the section's start.
            /// </summary>
            public SurfacePath StartSection(SectionId section)
            {
                Vector2 end = GroundEnd;
                float startX = Sections.Get(section).StartX;
                if (!Mathf.Approximately(end.x, startX))
                    throw new InvalidOperationException($"{section} starts at x = {startX} in the Sections table, but the ground before it ends at x = {end.x}.");
                return new SurfacePath(end.x, end.y);
            }

            /// <summary>The last point of the ground added so far.</summary>
            Vector2 GroundEnd
            {
                get
                {
                    IReadOnlyList<Vector2> last = Ground[Ground.Count - 1].Surface;
                    return last[last.Count - 1];
                }
            }

            /// <summary>
            /// Adds a strip of ground. A strip either starts exactly where the last one ended or, across a pit, has a
            /// wall at its start: a seam with neither is a gap the player would fall through unseen.
            /// </summary>
            public void AddGround(string name, SurfacePath path, bool wallAtStart = false, bool wallAtEnd = false)
            {
                if (Ground.Count > 0 && !wallAtStart && (path.Points[0] - GroundEnd).sqrMagnitude > 1e-6f)
                    throw new InvalidOperationException($"\"{name}\" starts at {path.Points[0]} but the ground before it ends at {GroundEnd}: pieces must meet, or have a wall at a pit.");
                Ground.Add(new GroundPiece(name, path.Points, wallAtStart, wallAtEnd));
                foreach (Vector2 point in path.Points)
                {
                    LowestGround = Mathf.Min(LowestGround, point.y);
                    HighestGround = Mathf.Max(HighestGround, point.y);
                }
            }

            /// <summary>A floating platform from startX to endX whose ends are at height y.</summary>
            public PlatformPiece AddPlatform(float startX, float endX, float y)
            {
                var platform = new PlatformPiece(new SurfacePath(startX, y).HumpTo(endX, PlatformHump).Points);
                Platforms.Add(platform);
                return platform;
            }

            /// <summary>A loop standing on the ground: its foot touches the ground at footX.</summary>
            public LoopPiece AddLoop(float footX, float radius)
            {
                var loop = new LoopPiece(OnGround(footX) + Vector2.up * radius, radius);
                Loops.Add(loop);
                return loop;
            }

            public void AddPlaneSwitch(float x, TerrainPlane whenMovingRight, TerrainPlane whenMovingLeft)
            {
                PlaneSwitches.Add(new PlaneSwitchPlacement(OnGround(x), whenMovingRight, whenMovingLeft));
            }

            /// <summary>The height of the ground at x. Not for use exactly at a step, where there are two.</summary>
            public float GroundAt(float x)
            {
                foreach (GroundPiece piece in Ground)
                {
                    IReadOnlyList<Vector2> surface = piece.Surface;
                    for (int i = 0; i < surface.Count - 1; i++)
                    {
                        Vector2 left = surface[i], right = surface[i + 1];
                        if (right.x > left.x && x >= left.x && x <= right.x)
                            return Mathf.Lerp(left.y, right.y, (x - left.x) / (right.x - left.x));
                    }
                }
                throw new InvalidOperationException($"There is no ground at x = {x} (a pit, or the section's ground is added after its objects).");
            }

            /// <summary>The point on the ground at x.</summary>
            public Vector2 OnGround(float x) => new Vector2(x, GroundAt(x));

            // ---- Objects ----

            /// <summary>A Woofer standing on the ground at x.</summary>
            public WooferPlacement AddWoofer(float x, Vector2 launchVelocity) => AddWoofer(OnGround(x), launchVelocity);

            /// <summary>A Woofer with its base at a point (for one that does not stand on the ground).</summary>
            public WooferPlacement AddWoofer(Vector2 basePosition, Vector2 launchVelocity)
            {
                var woofer = new WooferPlacement(basePosition, launchVelocity);
                Woofers.Add(woofer);
                return woofer;
            }

            public void AddStatic(float x) => Statics.Add(OnGround(x));

            public void AddClipper(float x, float patrolHalfRange) => Clippers.Add(new ClipperPlacement(OnGround(x), patrolHalfRange));

            public void AddTuningFork(float x) => TuningForks.Add(OnGround(x));

            // ---- Chimes ----

            /// <summary>Chimes evenly spaced from fromX to toX, following the ground at running height.</summary>
            public void ChimeLine(float fromX, float toX, int count)
            {
                for (int i = 0; i < count; i++)
                    Chimes.Add(OnGround(Mathf.Lerp(fromX, toX, i / (count - 1f))) + Vector2.up * ChimeHeight);
            }

            /// <summary>Chimes on a parabola between two points, its middle raised by apex metres.</summary>
            public void ChimeArc(Vector2 from, Vector2 to, float apex, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    float t = i / (count - 1f);
                    Chimes.Add(Vector2.Lerp(from, to, t) + Vector2.up * (apex * 4f * t * (1f - t)));
                }
            }

            /// <summary>Chimes evenly spaced round a circle, the first at its lowest point: the inside of a loop.</summary>
            public void ChimeCircle(Vector2 center, float radius, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    float radians = (-90f + 360f * i / count) * Mathf.Deg2Rad;
                    Chimes.Add(center + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius);
                }
            }

            /// <summary>An arc that starts and ends at running height above the ground at fromX and toX: over a notch or a pit.</summary>
            public void ChimeArcOverGround(float fromX, float toX, float apex, int count)
            {
                ChimeArc(OnGround(fromX) + Vector2.up * ChimeHeight, OnGround(toX) + Vector2.up * ChimeHeight, apex, count);
            }

            /// <summary>An arc from just before the end of one platform to just after the start of the next: the jump between them.</summary>
            public void ChimeArcBetween(PlatformPiece from, PlatformPiece to, float apex, int count)
            {
                var inset = new Vector2(0.5f, 0f);
                Vector2 lift = Vector2.up * (PlayerRadius + ChimeHeight * 0.5f);
                ChimeArc(from.End - inset + lift, to.Start + inset + lift, apex, count);
            }

            /// <summary>Chimes along the flight of a player thrown from a point with a velocity, evenly spaced in time.</summary>
            public void ChimeFlight(Vector2 start, Vector2 velocity, float firstTime, float lastTime, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    float time = Mathf.Lerp(firstTime, lastTime, i / (count - 1f));
                    Chimes.Add(start + velocity * time + Vector2.down * (0.5f * DesignTargets.Gravity * time * time));
                }
            }

            /// <summary>Chimes along the flight a Woofer gives, showing where it throws.</summary>
            public void ChimesAbove(WooferPlacement woofer, float firstTime, float lastTime, int count)
            {
                ChimeFlight(woofer.BasePosition + Vector2.up * PlayerRadius, woofer.LaunchVelocity, firstTime, lastTime, count);
            }
        }
    }
}
