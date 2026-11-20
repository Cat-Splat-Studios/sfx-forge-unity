# First Harmonic – level notes

A walkthrough of the one level, with positions, the speeds measured on it, and what the scene
builder has to call. The level itself is data in `Scripts/Level/LevelLayout.cs` (one method per section,
then the installer that builds it); the pieces that run in play mode are beside it in `Scripts/Level`.
`DESIGN.md` section 3 says what each section is for; this file says where everything is.

Units are metres and seconds. x runs from 0 to 1000, y is height (the spawn is at y = 0).

**Where the speeds come from.** Every figure marked **(speed)** was read from the PlayMode run with the
shipped `PlayerTuning` (gravity 30, cruise 10, top speed 22, jump 3.5 m; slope pull 7 m/s² × sin running,
10 uphill / 20 downhill tucked; acceleration 5; 1 m/s² of drag above cruise while running). The tests in
`Tests/ShowcaseTests.cs` run the whole level from the spawn to the Resonator through scripted input (high
route, 92 s, no hit, 112 of the 148 Chimes), log the pilot's position and speed every second
(`grep "SHOWCASE RUN"` in the PlayMode log) and assert the two launch speeds the Chime flights are laid
along. After changing the tuning or the terrain, read the figures there again.

## 1. What the builder calls

```csharp
var context = new ShowcaseContext { Art = artKitAsset };
LevelLayout.Build(context);          // needs context.Art; sets context.Level
GameplayInstaller.Install(context);  // (gameplay) sets context.Player, context.Session
LevelLayout.BuildCamera(context);    // needs context.Player and context.Level; sets context.Camera
```

`Build` creates this hierarchy, every object at z = 0:

| Object | Holds |
|---|---|
| `Level` | `LevelDescriptor` (spawn, bounds, kill height, goal, three checkpoints, Chime count) |
| `Level/Terrain/<name>` | 10 ground strips: art from `CreateGround`, an `EdgeCollider2D`, `GroundSurface(Both)` |
| `Level/Platforms/Platform n` | 6 floating platforms: `CreatePlatform`, `EdgeCollider2D`, `GroundSurface(Both, oneWay)` |
| `Level/Loops/Loop n` | 3 loops: `CreateLoop`, `Right Half (plane A)`, `Left Half (plane B)`, `Top Switch` |
| `Level/Plane Switches/…` | 5 `PlaneSwitch` triggers on the ground beside the loops |
| `Level/Chimes/Chime n` | 148 `Chime`s |
| `Level/Woofers/Woofer n` | 7 `Woofer`s |
| `Level/Static/Static n` | 6 `StaticBurr`s |
| `Level/Clippers/Clipper n` | 8 `Clipper`s (each with a kinematic `Rigidbody2D`) |
| `Level/Tuning Forks/Tuning Fork n` | 3 `TuningFork`s |
| `Level/Resonator` | the goal |
| `Level/Section Trigger` | one `SectionTrigger` volume over the whole level |
| `Level/Final Launch Trigger` | `FinalLaunchTrigger` at the last ramp's lip |

`BuildCamera` creates `Main Camera` (tag `MainCamera`, orthographic, half-height 9 m, `AudioListener`,
`FollowCamera`) centred on the spawn point at z = −10. Its clear flags are *solid colour*; `ShowcaseBuilder`
sets the colour to the palette's Night Plum, and the art kit's background covers the view.

Numbers the builder's self-test can check against `LevelDescriptor`: spawn (6, 0.5), goal (980, −10),
kill height −27, bounds x −4 … 1004 and y −26 … 36, checkpoints (335, −0.5), (466, 11.5), (604, 11.5),
Chime count 148.

## 2. Conventions used (and where to change them)

- **Ground** = a non-trigger `EdgeCollider2D` with a `GroundSurface` on the same object. **Everything
  else is a trigger.** No layers, no tags. All of it is in three methods of `LevelLayout`
  (`AddGroundCollider`, `AddStandingTrigger`, `AddRoundTrigger`).
- Curves are sampled every 0.5 m, loops every 0.4 m. Steps (terrace walls, the drop of a saw-tooth,
  ramp lips) are true vertical segments inside the same edge path.
- A ground strip that ends at a **pit** continues straight down the pit's side in the collider only
  (`wallAtStart` / `wallAtEnd`), so nobody slips under the neighbouring strip. Pits have no object:
  the player is lost below `KillY`.
- **Loops**: the right half (foot → right side → top, continued 25° past the top) is solid on plane A;
  the left half (from 25° before the top → left side → foot) on plane B; the flat ground runs on
  underneath on `Both`. The 25° overlap means the player always has ground under it while the top
  switch (a 1 × 3 m box just under the top) changes its plane. Ground switches: before a loop → A in
  both directions; after a loop → A when moving right, **B when moving left** (so a loop can be run
  backwards; `ARCHITECTURE.md` says "A" for both – the difference only shows when travelling left).
- Objects are placed by x and stood on the ground by the data code (`OnGround(x)`), so editing the
  terrain never leaves anything floating. Chimes are placed as lines along the ground, arcs, circles and
  *flights* (points along the parabola of a known launch).
- Every piece resets itself on `GameEvents.GameStarted` (Chimes and Clippers come back, forks go
  dark), so a restart works whether or not the scene is reloaded.

Events raised by the level: `WooferBounced`, `ClipperDefeated`, `CheckpointReached(index)`,
`SectionEntered(section)`, `FinalLaunch`. Everything else the player raises itself when the level
calls `AddChimes`, `Hurt`, `Finish`.

## 3. Walkthrough

### 1 · Soft Start (0 – 90) – run, jump

| x | y | What |
|---|---|---|
| −8 … 0 | 12 | Boundary wall (12 m, cannot be climbed) |
| 0 – 30 | 0 | Flat. **Spawn at x = 6.** Chime line 12 – 20 (5) |
| 30 | 0 → 1 | Low step 1 (1.0 m). A 5-Chime arc from x = 26.5 to 32.5 traces a full jump at cruise taken at x = 25; it lands near x = 34 |
| 30 – 55 | 1 | Flat. Chime line 40 – 48 (5) |
| 55 | 1 → 2.5 | Low step 2 (1.5 m) |
| 55 – 90 | 2.5 | Flat. Chime line 64 – 76 (7) |

No hazards. A step is a wall to the movement code, so the player has to jump – the arc says so.
Arrives at Rolling Sines at cruise, 10 m/s **(speed)**.

### 2 · Rolling Sines (90 – 220) – slopes

Three sine hills; all flanks are half sine waves, steepest slope 30°.

| x | y | What |
|---|---|---|
| 94 → 108 | 2.5 → 5.5 | Hill 1, 3 m |
| 108 → 124 | 5.5 → 1.5 | Down into valley 1. Chimes along the ground 116 – 132 (9) |
| 124 → 142 | 1.5 → 7.5 | Hill 2, 6 m |
| 142 – 156 | 7.5 | Flat top. **Clipper** at 149, walks 146 – 152 |
| 156 → 174 | 7.5 → 1 | Down into valley 2. Chimes 164 – 184 (11) |
| 174 → 206 | 1 → 9 | Hill 3, 8 m |
| 206 – 220 | 9 | Crest: the view opens onto the run-up |

A walker climbs every hill from a standstill (the slope pull at 30° is 4 m/s², the run
acceleration 5) **(speed)**. A runner who keeps holding right crosses the valleys at 10 – 12 m/s; tucking down into one is faster. The
Clipper is met after a 6 m climb, at or below cruise.

### 3 · Full Cycle (220 – 340) – the first loop

| x | y | What |
|---|---|---|
| 220 → 262 | 9 → −6 | Run-up, 15 m down, at most 29° steep. Chimes along it 224 – 248 (9) |
| 262 | −6 | Plane switch (→ A) |
| 262 – 292 | −6 | Flat under the loop |
| 272 | centre (272, 0) | **Loop, radius 6**. 12 Chimes in a circle of radius 5.2 |
| 281.5 | −6 | Plane switch (right → A, left → B) |
| 292 → 330 | −6 → −1 | Gentle rise, at most 12° |
| 330 – 340 | −1 | Flat. **Tuning Fork 1 at x = 335**, respawn (335, −0.5) |

**(speed)** Tucked from the top of the run-up, the foot is reached at the 22 m/s cap and the top of the
loop crossed at about 13 m/s. Tucked, the loop needs about 17 m/s at the foot; a full Wind-Up Burst from
standing there (21 m/s) goes round. Running and holding forward it is far more forgiving than that: the
run acceleration (5 m/s²) works on walls too and nearly cancels the running slope pull, so anything
above about 3.5 m/s at the foot gets round. Only a tucked or coasting player who enters too slowly loses
grip: it drops to the flat inside the loop, still on plane A, and can walk back out to the left for
another go.
No hazards in this section.

### 4 · Woofer Steps (340 – 460) – springs

| x | y | What |
|---|---|---|
| 340 – 362 | −1 | Flat. **Woofer** at 360, launch (4, 21) |
| 362 | −1 → 4 | Terrace wall, 5 m |
| 362 – 388 | 4 | Terrace 1. **Woofer** at 386, launch (4, 21) |
| 388 – 414 | 9 | Terrace 2. **Woofer** at 412, launch (4, 21) |
| 414 – 446 | 14 | Terrace 3 (highest ground). **Diagonal Woofer** at 443, launch (16, 15) |
| 446 – 449 | 8 | The dip under the diagonal flight |
| 449 → 460 | 8 → 11 | Straight 15° slope out of the dip |

The terrace Woofers lean 11° towards their wall and stand 2 m before it: the flight peaks 7.3 m up
and comes down 2.4 m past the edge with no input (the corner is cleared by 0.8 m). A 4-Chime column
shows each flight. The diagonal Woofer's flight lasts 1.2 s and lands near x = 461 on Static Field's
first flat, 16 m/s sideways; six Chimes follow the arc. Whoever walks off the edge at 446 instead lands
in the dip and walks out up the slope – no trap.

### 5 · Static Field (460 – 600) – hazards

Ground height 11. **Tuning Fork 2 stands at the start, on the landing flat**: both pits of the level and
its densest stretch of hazards are behind it, so a fall costs this section only. A *tooth* is an 8 m
ramp rising 2 m (14°) with a sheer 2 m drop back to the flat; the Static burr (2 m wide) sits 2.5 m
beyond the drop, where a runner who just runs off the edge comes down. Pits are 4 m wide.

| x | What |
|---|---|
| 460 – 474 | Landing flat – no hazard for 23 m after the landing point. **Tuning Fork 2 at x = 466**, respawn (466, 11.5) |
| 474 → 482 | Tooth 1, drop at 482. **Static at 484.5** |
| 482 – 504 | Flat. **Clipper** at 495 (492 – 498) |
| 504 – 508 | **Pit 1** |
| 508 – 516 | Flat |
| 516 → 524 | Tooth 2, drop at 524. **Static at 526.5** |
| 529 → 537 | Tooth 3 straight after, drop at 537. **Static at 539.5** |
| 537 – 556 | Flat. **Clipper** at 549 (546 – 552) |
| 556 – 560 | **Pit 2** |
| 560 – 574 | Flat. **Clipper** at 567 (564 – 570) |
| 574 → 582 | Tooth 4, drop at 582. **Static at 584.5** |
| 582 – 600 | Flat. **Clipper** at 591 (589 – 593) |

A 3-Chime arc over every drop and every pit marks the safe line (18 Chimes). **(speed)** A jump from
the top of a tooth at 9 m/s comes down about 6 m on, 2 m beyond the burr; a jump clears a pit from
5 m/s. The arcs over the pits are on a jump's path (the pilot takes 2 of 3); the arcs over the drops are
symmetric and a jump taken on the 14° ramp is thrown back and up by the slope, so it passes above the
middle Chime (1 of 3) – see "Known gaps". Landing on a Clipper from a jump pops it (the player is tucked in the air), so the Clippers
after the teeth reward the jump instead of punishing it.

### 6 · Split Signal (600 – 800) – high route, low route

| x | y | What |
|---|---|---|
| 600 – 610 | 11 | Flat. **Tuning Fork 3 at x = 604**, respawn (604, 11.5): before the hill, so a respawn gets another try at the high route |
| 610 → 636 | 11 → 2 | 9 m downhill |
| 636 – 642 | 2 | Flat |
| 642 → 650 | 2 → 6 | Launch ramp, a quarter sine, 38° at the lip (650, 6) |
| 650 – 653.5 | 3.5 | Ledge under the lip. **Woofer** at 651.8, launch (9, 16) |
| 653.5 – 726 | 0 | Low route floor |
| 726 – 746 | 1.5 | Low terrace (jumpable step) |
| 746 – 770 | 0 | Floor |
| 770 → 788 | 0 → 3 | The routes rejoin |
| 788 – 800 | 3 | Flat |

**High route** – six one-way platforms, each half a sine wave 0.6 m high, 0.8 m thick:

| Platform | x | Height of its ends |
|---|---|---|
| 1 | 656 – 672 | 6.5 |
| 2 | 677 – 693 | 7.0 |
| 3 | 699 – 715 | 6.5 |
| 4 | 721 – 737 | 7.0 |
| 5 | 743 – 759 | 6.5 |
| 6 | 765 – 779 | 5.5 |

Gaps are 5 – 6 m (a jump at cruise carries 9.7 m); a 3-Chime arc spans each gap and a 4-Chime flight
leads from the lip to platform 1 (19 Chimes). No enemies up here. Running off platform 6 lands on the
rejoining slope near x = 786.

**(speed)** Who gets the high route: leaving the lip at 16 m/s or more lands on platform 1. Tucking
down the hill gives about 19 m/s at the lip; a runner who only holds right arrives at about 13 m/s,
falls short of the platform and past the ledge, and takes the low route. Someone who merely rolls off
the lip (after a respawn at fork 2, say) drops onto the ledge Woofer, which throws to x ≈ 658 on
platform 1.

**Low route** – arrivals land between x = 655 and 665; the first hazard is 14 m beyond that.

| x | What |
|---|---|
| 660 – 668 | Chime line (3) |
| 682 | **Clipper** (679 – 685) |
| 691 | **Static** |
| 700 | **Second-chance Woofer**, launch (4, 21.5): peaks 7.7 m up, passes through platform 3 from below and lands on it near x = 704. Three Chimes above it |
| 714 | **Clipper** (711 – 717) |
| 736 | **Static** on the low terrace, with a 3-Chime arc over it |
| 758 | **Clipper** (755 – 761) |

### 7 · Overdrive (800 – 960) – the finale

| x | y | What |
|---|---|---|
| 800 – 806 | 3 | Flat. **Horizontal Woofer** at (803, 3.5), launch (20, 0) |
| 806 → 838 | 3 → −9 | 12 m downhill, at most 31° |
| 840 | −9 | Plane switch (→ A) |
| 850 | centre (850, −3) | **Loop, radius 6** |
| 859.5 | −9 | Plane switch (right → A, left → B) |
| 870 | centre (870, −2) | **Loop, radius 7** |
| 880.5 | −9 | Plane switch (right → A, left → B) |
| 888 → 900 | −9 → −12 | The final valley |
| 900 – 910 | −12 | Flat. **Horizontal Woofer** at (904, −11.5), launch (22, 0) |
| 910 → 930 | −12 → −5 | Launch ramp, a quarter sine, 29° at the lip (930, −5). **Final Launch trigger** 930 – 931.5 |
| 930 – 960 | −10 | Landing strip, 5 m below the lip |

No hazards. **(speed)** The first Woofer gives 20 m/s to anyone and the downhill takes that to the
22 m/s cap before the loops. The loops are entered at about 22 and 19 m/s, but the two of them and the
drag above cruise leave only about 15 m/s at x = 888 – and the ramp is 7 m tall. So a second horizontal
Woofer in the valley gives top speed again: the lip is left at 18.6 m/s, velocity (16.3, 8.9), for a
flight of just under a second that lands near x = 945. Fifteen Chimes hang in three rows along that
flight (the middle row is the flight itself; the pilot collects 7 from Overdrive on). A slow player
drops off the lip onto the strip 5 m below and walks on; a player stuck in the valley has the Woofer.

### 8 · The Resonator (960 – 1000) – the goal

Flat at y = −10. **The Resonator stands at x = 980**; the wall that closes the level is at x = 1000,
20 m of run-out for the 15 m the player needs to stop.

## 4. Counts

| | Count | Where |
|---|---|---|
| Chimes | 148 | 22 + 20 + 27 + 18 + 18 + 28 + 15 + 0 by section |
| Woofers | 8 | 360, 386, 412, 443 (diagonal), 651.8 (ledge), 700 (second chance), 803 and 904 (horizontal) |
| Static | 6 | 484.5, 526.5, 539.5, 584.5, 691, 736 |
| Clippers | 8 | 149, 495, 549, 567, 591, 682, 714, 758 |
| Loops | 3 | 272 (r 6), 850 (r 6), 870 (r 7) |
| Tuning Forks | 3 | 335 (end of Full Cycle), 466 (start of Static Field), 604 (start of Split Signal): each before what it guards. Overdrive and The Resonator have no hazard and need none |
| Pits | 2 | 504 – 508, 556 – 560 |

Ground height ranges from −12 (the final valley) to 14 (the third terrace). Measured time for a clean
run on the high route: 92 s, an average of about 11 m/s – most of the level is run at cruise, with the
speed in Full Cycle, down the split's hill and in Overdrive.

`ShowcaseBuilder`'s checks take these counts from `LevelLayout.CountPieces()`, so adding a piece to the
data needs no second number changed (this table is the only other copy).

## 5. What keeps the data consistent, and known gaps

Checked when the level is built (an exception names the problem):

- A section's ground starts where the previous ground ended (`level.StartSection(id)`), and that x must be
  the section's start in `Core/Sections.cs`.
- A strip of ground either continues from the last one or has a wall at its start (across a pit): a
  seam with neither would be a gap the player falls through.
- Objects are stood on the ground at their x (`GroundAt` throws over a pit).

Checked in play mode (`Tests/ShowcaseTests.cs`): the hand-over at the top of a loop and the switch after
it, the terrace Woofer's flight, the split (tucking reaches platform 1 at 16.7 m/s off the lip, plain
running does not and the low route still leads on), the speed off the last ramp, a pit's respawn at the
fork before it, the whole level from start to goal.

Known gaps, by hand for now:

- A loop does not add its own ground switches: `AddLoop` is followed by `AddPlaneSwitch` calls 4 m before
  and 3.5 m after it. Back-to-back loops share one (the "after" of the first is the "before" of the
  second), which is why it is not automatic.
- The Chime arcs over Static Field's four drops are not on the path of a jump taken on the ramp. Laying
  them along the slope-thrown jump needs the air steering in the layout's flight helper.
- A fall into a pit takes about 2.6 s: the kill height is one value for the whole level (15 m below
  the lowest ground, −27) while the pits are in ground at height 11.
- The pilot's jump and tuck stretches in `Tests/ShowcaseTestBase.cs` are x positions: move a pit or a
  step and they move with it.
