# BRIO: FIRST HARMONIC – gameplay notes

What the movement code in `Scripts/Gameplay` expects from the level, and what it gives back. Written by
the gameplay programmer for the level designer and the technical director. `ARCHITECTURE.md` holds the
contracts; this file holds the numbers and the fine print behind them.

The numbers in section 3 come from the tuning values and an energy estimate. The movement itself has since
been run in play mode: `Tests/ShowcaseTests.cs` checks cruise speed, jump height and the short hop, the
Wind-Up and Burst, hurt and respawn, and runs all three loops (the first tucked, the other two running)
and the whole level through scripted input. Feel – how it plays in the hand – has not been tuned by a person.

## 1. Files

| File | What it is |
|---|---|
| `PlayerController.cs` | The player (`PlayerBase`): ground-speed model, sensors, jump, Tuck, Wind-Up/Burst, hurt, respawn. |
| `PlayerTuning.cs` | Every movement number, one comment each. Serialized on the player; edit in the Inspector. |
| `GroundSensor.cs`, `SensorHit.cs` | The only code that queries physics: rays filtered to `GroundSurface` colliders. |
| `DeviceInput.cs` | Keyboard and gamepad through the legacy Input Manager. The only class that uses `Input`. |
| `GameSession.cs` | The run (`GameSessionBase`): phase, Chimes, combo, timer, top speed, start and restart. |
| `ScatteredChime.cs` | The Chimes that fly out on a hit. |
| `GameplayInstaller.cs` | `Install(ShowcaseContext)`: builds the player and the session. |

Controls: left/right = arrows, A/D or left stick; down = down arrow, S or stick down; Jump = Space, Z,
gamepad button 0 or 3.

## 2. What the level must provide

### Colliders

- **Ground = a non-trigger `EdgeCollider2D` with a `GroundSurface` on the same GameObject.** The
  sensors are raycasts, and they ignore every collider without a `GroundSurface` and every trigger.
  Use edge colliders only: a ray that starts inside a `BoxCollider2D` or `PolygonCollider2D` reports a
  hit at distance zero, which the movement code reads as a wall.
- **No Unity layers, no tags.** Any layer works; the layer collision matrix is not used.
- Edge colliders are two-sided. The underside of an edge blocks a jump from below unless the surface
  is `OneWay`.
- **Everything else is a trigger** and finds the player with `other.TryGetComponent(out PlayerBase p)`.
  The player is a kinematic `Rigidbody2D` with a trigger `CircleCollider2D` (radius 0.5 m standing,
  0.4 m tucked or jumping). It moves at most 0.44 m per physics step (22 m/s at 50 Hz), so a trigger
  should be at least 0.5 m thick along the direction of travel.
- Scattered Chimes are not colliders; they check their distance to the player themselves.

### Terrain shape

| Shape | What happens |
|---|---|
| Corner of up to 50° between two segments | Followed without leaving the ground, in either direction. |
| Sharper convex corner, or the end of an edge | The player leaves the ground with its velocity along the last tangent. A launch ramp must end like this. |
| Face at least 60° steeper than the ground underfoot | A wall: the player stops. |
| Step lower than about 0.4 m | Walked up. |
| Step of 0.5 m or more | A wall: must be jumped. |
| Curves (hills, loops) | Sample so that neighbouring segments differ by 10° or less; a loop of 36 segments or more is smooth. |

- Ground speed is capped at 22 m/s. The floor sensors reach 0.3 m below the feet, so the player stays
  on gentle crests at any speed and only leaves the ground where the ground really ends.
- An airborne player lands on surfaces up to 60° from flat and slides along anything steeper.
  It does not re-attach to walls or ceilings from the air.

### One-way platforms

`GroundSurface.OneWay = true`: solid only for the floor sensors, only when met from above and only
when the player is not already more than 0.25 m into it. Never a wall or a ceiling. Keep them within
about 60° of flat.

### Loops and terrain planes

- Build a loop as in `ARCHITECTURE.md` 3.2: right half on plane `A`, left half on plane `B`, ground on
  `Both`. Both halves should start and end tangent to their neighbours.
- `PlaneSwitch` reads `player.Velocity.x`; on the ground the velocity is the ground speed along the
  tangent, so it is negative across the top of a loop entered from the left.
- **The surface the player stands on stays solid after a plane switch** until the feet leave it. A
  switch that fires a little before the top is therefore safe. A switch that fires late is not: once
  both floor sensors (0.3 m ahead of and behind the centre) are past the end of the `A` half and the
  player is still on `A`, it falls. The top trigger is a box 1 m wide just under the top: the player's
  circle (radius 0.4 – 0.5 m) reaches it about 1 m before the top, early enough.
- A switch decides on enter AND on exit, from the direction of travel at that moment, so a player who
  turns round inside a switch's box leaves on the plane of the way it goes.
- `PlaceAt` and every respawn reset the plane to `A`. `SetTerrainPlane(Both)` is treated as `A`.

### Positions

- `LevelDescriptor.SpawnPoint` is the player's **centre**. The player starts airborne and drops, so
  put it 0.5 m to 1 m above the ground.
- `SetCheckpoint(index, position)`: the player reappears 0.6 m above `position`, so a point on the
  ground or up to a metre above it both work. Only a higher index than the current one is accepted.
- Below `LevelDescriptor.KillY` the player respawns (0.8 s pause, Chimes lost, no `Hurt` event).

## 3. Numbers to lay the level out with

From `PlayerTuning` defaults. Gravity, cruise, top speed, jump height and radii equal `DesignTargets`.

| Thing | Value |
|---|---|
| Rest to cruise (10 m/s) on flat ground | 2 s, about 10 m |
| Coast from cruise to rest, no input | 2.5 s, about 12.5 m |
| Brake from cruise | 0.25 s, about 1.3 m |
| Full jump | 3.5 m high, 14.5 m/s launch, 0.97 s in the air, about 9.7 m long at cruise |
| Tapped jump | about 0.6 m high |
| Slope pull, running | 7 m/s² × sin(slope) |
| No input on a slope | Gentler than 23° (`standFirmAngle`): friction (4 m/s²) beats the pull, the player coasts to a stop and stands. Steeper: no friction without input, so the slope carries a coasting runner downhill and slides a standing one |
| Slope pull, tucked | 10 m/s² × sin uphill, 20 m/s² × sin downhill |
| Steepest slope walked up from rest | about 45° (acceleration 5 against a pull of 7 × sin) |
| Loss of grip | below 3 m/s on ground steeper than 45°: controls lock 0.5 s; steeper than 70°: the player drops |
| Burst | 12.7 m/s (one tap) to 21 m/s (six taps); the charge leaks 25 % per second |
| Knock-back | 5 m/s away, 8 m/s up (about 1.1 m high, 2.7 m long); invulnerable 2 s |
| Woofer | `Bounce(v)` sets the velocity to `v`; height reached = v.y² / 60 |
| Rebound (a popped Clipper) | Upward speed raised to 9 m/s; never lowered, so a jump still rising faster keeps its rise |
| `Speed` / `Speed01` (what the HUD, music and wind read) | Travel speed: \|ground speed\| on the ground, \|velocity.x\| in the air, capped at top speed. `Velocity` is the full vector |

`Bounce` on a grounded player with a launch that lifts it off the ground by 1 m/s or less (a
horizontal Woofer) sets the ground speed and leaves the player running or rolling. Any other bounce
makes the player airborne and untucked, so it is **not** attacking after a Woofer.

### Loops: speed needed at the foot

For a player who stays tucked (or coasts): the slope pull is all there is, and below 3 m/s on the
wall the grip is lost.

| Loop radius | Tucked |
|---|---|
| 6 m | about 17 m/s |
| 7 m | about 18.5 m/s |

A player who keeps HOLDING FORWARD while running needs far less: the run acceleration (5 m/s² below
cruise) applies on walls and ceilings too and nearly cancels the running slope pull (7 × sin), so
either loop is completed from about 3.5 m/s at the foot. The level therefore does not gate its loops on
a runner's speed; the run-ups are there so that the loop is taken fast.

Speed gained on a downhill run-up that drops `H` metres, starting at cruise:

| Drop | Running (holding forward) | Tucked |
|---|---|---|
| 4 m | about 12 m/s | about 15 m/s |
| 6 m | about 13.5 m/s | about 17 m/s |
| 8 m | about 14.5 m/s | about 19 m/s |

So a 6 m loop wants a run-up that drops about 6 m, and a 7 m loop about 8 m (or a horizontal Woofer
of 16 m/s or more just before it). A full Burst (21 m/s) clears both from a standstill at the foot;
a Burst of four taps or fewer may not clear the 7 m loop. If the built level's run-ups are lower,
lower `slopeFactorRunning` and `slopeFactorTuckedUphill` rather than moving terrain.

## 4. Events raised by the gameplay folder

| Event | When |
|---|---|
| `GameStarted` | `GameSession.StartGame()` and `Restart()`, after the player has been placed. |
| `UiConfirmed` | Jump pressed on the title card, or on the results panel after it has shown for 1 s. |
| `Jumped` | A jump leaves the ground. |
| `Landed(impactSpeed)` | Any touch-down from the air, including the first drop at the spawn point; the speed into the surface, m/s. Not filtered: the audio applies its own threshold. |
| `Footstep(speed01)` | Once per stride while running; the stride grows from 0.9 m to 2.6 m with speed (`DesignTargets.StrideLength`, which the character's legs step by too). |
| `TuckStarted` | A roll on the ground begins (down pressed, or a Burst). Its end (too slow, a jump, a ledge, a hit, a Woofer into the air) has no event: read `IsRolling && IsGrounded`. |
| `WindUpCharged(charge01)` | Each tap; 1/6 per tap. |
| `BurstReleased(charge01)` | Down released while charging. Not raised if a hit cancels the charge; `IsCharging` goes false. |
| `Skidded` | Once per braking from 5 m/s or more. |
| `ChimeCollected(combo)` | Once per Chime in `AddChimes`. |
| `Hurt`, `ChimesScattered(count)` | Inside `Hurt` when it returns true; `ChimesScattered` only when Chimes were carried. |
| `Respawned(section)` | When the player reappears. |
| `GoalReached` | Inside `Finish`, once per run. |

State details the art and the audio may rely on:

- `IsRolling` is true on the ground while tucked and in the air after a jump. It is false after a
  Woofer, after a hit and on landing (landing always untucks; holding down re-tucks on the next step
  and raises `TuckStarted` again).
- `IsAttacking` = tucked or winding up.
- `GroundAngle` is −180°..180°, counter-clockwise; `Facing` follows the ground speed's sign, so it
  stays +1 all the way round a loop entered from the left.
- `HasControl` is false on the title card, during the 0.8 s respawn pause and after the goal.
  During the respawn pause the player is frozen where it was lost and `IsInvulnerable` is true.
- `Position` is the interpolated transform position: smooth for the camera in `LateUpdate`.

## 5. Restart

`GameSession.Restart()` does not reload the scene. It zeroes the run, calls
`PlayerController.ResetForNewRun()` (spawn point, no checkpoint, plane `A`) and raises `GameStarted`.
Level pieces put themselves back when they hear `GameStarted`. Scattered Chimes still in flight are
left to expire.

## 6. Visual effects spawned from here

The player calls `ArtKitBase.SpawnEffect` for `Dust` (a skid, and a landing at 12 m/s or more) and
`BurstPuff` (a Burst). A re-collected scattered Chime spawns `ChimePop`. Nothing else in the project
should spawn these for the same moments.
