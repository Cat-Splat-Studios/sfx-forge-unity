# BRIO: FIRST HARMONIC – architecture

How the showcase is put together, who owns what, and the contracts between the folders. `DESIGN.md` says
what the game is; this file says how the code is organised. Where the appendix of `DESIGN.md` names
folders, assemblies or namespaces differently, **this file wins**.

The purpose of the whole project: a maintainable, easy-to-follow example of SFX FORGE in Unity that
another developer can read and copy. Every rule below serves that.

## 1. The one rule

> **Audio only listens to events and reads player state. Gameplay never calls SFX FORGE.**

- Gameplay and level code *raise* `GameEvents`. They never reference `CatSplat.SfxForge`.
- `SfxShowcase.Audio` is the only assembly that references `CatSplat.SfxForge`. It *subscribes* to
  `GameEvents`, *reads* `PlayerBase` / `GameSessionBase`, and never raises an event or changes the game.
- Outside `Scripts/Audio` the audio code is named in two files only – `Editor/ShowcaseAudio.cs` (the
  builder's install and check) and `Tests/ShowcaseAudioTests.cs` – and called from two lines of
  `ShowcaseBuilder`. Deleting the folder, those two files and those two lines leaves a silent game:
  the four other runtime assemblies, the builder and the gameplay tests have no reference to it.

## 2. Layout and ownership

Everything is under `unity/Demo/Assets/Showcase` – in this repository. The folder does not have to be there:
as the package's sample it is `Assets/Samples/SFX Forge/<version>/Showcase`, and `Editor/ShowcaseBuilder.Root`
and `Tests/ShowcaseFolders.Root` find it from their assembly definition files, so the paths in this document are
this repository's and nothing in the code repeats them. One owner per folder; nobody edits another owner's
files – if a contract in `Scripts/Core` does not fit, tell the technical director, do not work round it.

| Folder | Assembly = namespace | References | Owner | Holds |
|---|---|---|---|---|
| `Scripts/Core` | `SfxShowcase.Core` | – | Technical director | The contracts of section 3. Nothing else. |
| `Scripts/Gameplay` | `SfxShowcase.Gameplay` | Core | Gameplay programmer | The player (movement, input, hurt, respawn, scattered Chimes), the run (`GameSession`), `GameplayInstaller`. |
| `Scripts/Audio` | `SfxShowcase.Audio` | Core, `CatSplat.SfxForge` | Sound designer | The patch library, the effects player, `MusicDirector`, `AudioInstaller`. |
| `Scripts/Level` | `SfxShowcase.Level` | Core | Level designer | `LevelLayout` (the level as data and its installer) and the runtime level pieces: Chime, Woofer, Static, Clipper, Tuning Fork, Resonator, section and plane triggers, the follow camera. |
| `Scripts/Art` | `SfxShowcase.Art` | Core | Art direction | `ArtKit` (meshes, materials), `Palette`, the character visual, background, effects, HUD. |
| `Editor` | `SfxShowcase.Editor` (Editor only) | all of the above, `CatSplat.SfxForge`, `CatSplat.SfxForge.Editor` | Technical director | Scene generation and its checks: `ShowcaseBuilder.cs`, and `ShowcaseAudio.cs` – the only editor file that names the audio code. |
| `Tests` | `SfxShowcase.Tests` | all runtime assemblies, `CatSplat.SfxForge`, `UnityEngine.TestRunner`, NUnit | Technical director | PlayMode tests. |
| `Docs` | – | – | `DESIGN.md`: art direction. `ARCHITECTURE.md`: technical director. | |
| `Generated` | – | – | written by `ShowcaseBuilder` | `ArtKit.asset` (with the two materials inside it), `ShowcaseMeshes.asset` (every generated mesh), `Presets/<sound>.asset` (one `SfxPreset` per sound effect). Never edited by hand. |

The four runtime assemblies (Gameplay, Audio, Level, Art) reference **only Core**, never each other.
That is what lets four people work at once, and what makes each folder liftable on its own. Nothing in
`Assets/Showcase` references code or assets in `Assets/SfxDemo`: the showcase has its own copy of the demo
song in `Song/` (the same bytes, files with their own GUIDs), so the folder is complete by itself.

Facts about the project that constrain everyone:

- Built-in render pipeline (no URP/HDRP). 2D physics (`com.unity.modules.physics2d`) is available.
- Input: the legacy Input Manager (`activeInputHandler: 0`) – `Input.GetAxisRaw("Horizontal")`,
  `"Vertical"`, `Input.GetButton("Jump")`. The Input System package is not installed.
- **uGUI (`UnityEngine.UI`), TextMeshPro and `TextMesh`/`Font` are not in the project** (neither
  `com.unity.ugui` nor the text-rendering module is in `Packages/manifest.json`), and the manifest is
  not to be changed. The HUD is therefore drawn by the art kit as generated meshes in front of the
  camera, with a small generated stroke font (A–Z, 0–9, `:` `.`). This replaces the `LegacyRuntime.ttf`
  exception in `DESIGN.md`: nothing in the showcase is a non-generated asset.
- SFX FORGE's native library is Windows x64 only; `SfxPreset.Bake` returns null and the players stay
  silent where it is missing. Audio code must tolerate a null clip.

## 3. The contracts (`Scripts/Core`)

One type per file, file named after the type.

| Type | Kind | Purpose |
|---|---|---|
| `GameEvents` | static class | The event channel: 19 C# events and a `Raise…` method for each. The only static state in the game. |
| `IPlayerState` | interface | What may be read about the player: position, velocity, ground speed, `Speed` (travel speed: along the ground, sideways in the air, never above `TopSpeed`) and `Speed01`, grounded, ground angle, rolling, charging, `Charge01`, facing, attacking, invulnerable, has control, terrain plane. |
| `IPlayerActor` | interface | What level pieces do to the player: `Bounce`, `Hurt`, `Rebound`, `AddChimes`, `SetCheckpoint`, `Finish`, `SetTerrainPlane`. |
| `IPlayerInput` | interface | The control scheme (`MoveX`, `Down`, `JumpHeld`), so tests can script it. |
| `PlayerBase` | abstract `MonoBehaviour` | `IPlayerState` + `IPlayerActor` as a serializable reference type; adds `InputOverride` and `PlaceAt`. The gameplay player derives from it. |
| `IGameSession` | interface | The run: `Phase`, `ChimeCount`, `ElapsedSeconds`, `TopSpeedReached`, `CurrentSection`. |
| `GameSessionBase` | abstract `MonoBehaviour` | `IGameSession` as a serializable reference type; adds `StartGame` and `Restart`. The gameplay session derives from it. |
| `GamePhase` | enum | `Title`, `Playing`, `Finished`. |
| `SectionId` | enum | The eight sections, in order; the value is the index. |
| `SectionInfo` | struct | A section's id, display name, start x, end x. |
| `Sections` | static class | The table of the eight sections (`All`, `Get`, `At(x)`, `LevelStartX`, `LevelEndX`). |
| `DesignTargets` | static class | The numbers level and movement both build against: gravity 30 m/s², cruise 10 m/s, top speed 22 m/s, jump height 3.5 m, radii, combo window, `LaunchSpeedForHeight`. |
| `TerrainPlane` | enum | `Both`, `A`, `B`: the two collision planes that make loops work. |
| `GroundSurface` | `MonoBehaviour` | Marks a non-trigger `Collider2D` as ground, with its plane and whether it is one-way. |
| `LevelDescriptor` | `MonoBehaviour` | What the built level says about itself: spawn point, bounds, kill y, goal position, checkpoint positions, Chime count. |
| `IArtKit` | interface | Every visual the level and gameplay ask the art for (signatures below). |
| `ArtKitBase` | abstract `ScriptableObject` | `IArtKit` as an asset that can be referenced from a serialized field; exposes the two materials. The art folder's `ArtKit` derives from it. |
| `PropVisual` | abstract `MonoBehaviour` | A visual that reacts: `Activate()` (Woofer punch, fork/Resonator lights) and `ResetVisual()`. |
| `EffectKind` | enum | `ChimePop`, `ClipperShards`, `BurstPuff`, `Dust` for `IArtKit.SpawnEffect`. |
| `ShowcaseContext` | class | What the builder passes between construction steps: `Art`, `Level`, `Player`, `Session`, `Camera`. |

Why abstract classes beside the interfaces: Unity cannot serialize a reference to an interface. A
component that needs the player declares `[SerializeField] PlayerBase player;` and the builder wires
it. Code that only reads should still take the interface type in its own methods where it can.

### 3.1 Events: who raises what

Subscribe in `OnEnable`, unsubscribe in `OnDisable`. Each event is raised by exactly one side:

| Event | Payload | Raised by |
|---|---|---|
| `GameStarted` | – | Gameplay (session), when the title card is dismissed and after a restart |
| `Jumped` | – | Gameplay |
| `Landed` | impact speed, m/s | Gameplay |
| `Footstep` | speed01 | Gameplay (from the foot cycle; not while tucked) |
| `TuckStarted` | – | Gameplay (a ground roll begins; its end is read from `IPlayerState.IsRolling`) |
| `WindUpCharged` | charge01 after the tap | Gameplay |
| `BurstReleased` | charge01 | Gameplay |
| `ChimeCollected` | combo (1, 2, 3 …; resets after `DesignTargets.ChimeComboWindow`) | Gameplay, inside `AddChimes` – once per Chime |
| `ChimesScattered` | count | Gameplay, inside `Hurt` |
| `Hurt` | – | Gameplay, inside `Hurt` (when it returns true) |
| `Skidded` | – | Gameplay |
| `Respawned` | `SectionId` of the respawn point (`Sections.At(x)`) | Gameplay |
| `GoalReached` | – | Gameplay, inside `Finish` (once) |
| `UiConfirmed` | – | Gameplay (session), on Jump at the title card or results panel |
| `WooferBounced` | – | Level (the Woofer, after calling `Bounce`) |
| `ClipperDefeated` | – | Level (the Clipper) |
| `CheckpointReached` | fork index, 0-based | Level (the Tuning Fork, the first time only, after calling `SetCheckpoint`) |
| `SectionEntered` | `SectionId` | Level (section triggers; once per crossing into a different section, and once for the first section when the run starts) |
| `FinalLaunch` | – | Level (a trigger at the lip of Overdrive's launch ramp, x ≈ 930) |

Two rows of the design's audio table are **not** events: the roll hum (5) and the wind (15) are
continuous, so the audio reads `IPlayerState` each frame (`IsRolling && IsGrounded`, `Speed01`)
instead of receiving an event sixty times a second. The Wind-Up rev is released on `BurstReleased` or
when `IsCharging` goes false.

### 3.2 Player and level pieces

- The player GameObject carries: the `PlayerBase`-derived component, a **kinematic `Rigidbody2D`**
  and a **trigger `CircleCollider2D`** (radius `DesignTargets.StandingRadius`). The movement code
  moves it itself; the rigidbody exists so trigger messages arrive.
- **Ground** is every *non-trigger* `Collider2D` with a `GroundSurface` on the same GameObject
  (the level uses `EdgeCollider2D` along the surface polyline). The movement code finds ground with
  `Physics2D` casts that ignore triggers and skips surfaces where `IsSolidFor(Plane)` is false.
  `OneWay` surfaces are solid only when approached from above.
- **Everything else in the level is a trigger collider.** A piece reacts in `OnTriggerEnter2D`:
  `if (!other.TryGetComponent(out PlayerBase player)) return;` then calls `IPlayerActor` methods.
  No tags, no layers, no `Find`.
- A Clipper: `if (player.IsAttacking) { defeat; player.Rebound(); GameEvents.RaiseClipperDefeated(); }
  else player.Hurt(transform.position);`. Static: `player.Hurt(transform.position)`.
- Pits need no object: the player respawns by itself below `LevelDescriptor.KillY`.
- Placed Chimes are the level's (`AddChimes(1)` then hide and `SpawnEffect(ChimePop)`); the Chimes
  scattered by a hit are the gameplay folder's own short-lived objects, drawn with `CreateChime()`.
- **Loops.** The right half of a loop (bottom, up the right side, to the top) is an `EdgeCollider2D`
  on plane `A`; the left half (top, down the left side, to the bottom) is on plane `B`; the ground
  under and around it is `Both`. The player starts on `A`. A trigger at the top of the loop calls
  `SetTerrainPlane(B)` when the player passes moving left (and `A` when moving right); triggers on the
  ground before and after the loop call `SetTerrainPlane(A)`. `PlaceAt` and every respawn reset to `A`.

### 3.3 The art kit (`IArtKit`, exact signatures)

```csharp
GameObject CreateGround(IReadOnlyList<Vector2> surface, float bottomY);
GameObject CreatePlatform(IReadOnlyList<Vector2> surface, float thickness);
GameObject CreateLoop(Vector2 center, float radius, float thickness);
GameObject CreateChime();
PropVisual CreateWoofer();
GameObject CreateStatic(int seed);
GameObject CreateClipper();
PropVisual CreateTuningFork();
PropVisual CreateResonator();
GameObject CreateCharacter(PlayerBase player);
GameObject CreateBackground(Camera camera, Rect levelBounds);
GameObject CreateHud(Camera camera, PlayerBase player, GameSessionBase session);
void       SpawnEffect(EffectKind kind, Vector2 position);
```

Every `Create…` returns a new, unparented object at the origin that is **only a visual** (meshes and
the art's own animation components). The caller adds colliders and behaviour, names it, parents it
and positions it at z = 0; depth ordering, outlines and colours are the art kit's business. Origins
are given in the XML comment of each method (`IArtKit.cs`). `CreateGround`, `CreatePlatform` and
`CreateLoop` take world-space coordinates and are left at the origin. The kit must work both in the
editor (scene generation) and at runtime, and should share one mesh between identical objects.
`ArtKitBase.OpaqueMaterial` / `TransparentMaterial` are the only two materials in the game.

### 3.4 Entry points each folder must provide

`ShowcaseBuilder` calls exactly these. The names and signatures are part of the contract.

| Owner | Entry point | Does |
|---|---|---|
| Art | `public sealed class SfxShowcase.Art.ArtKit : ArtKitBase` | The builder creates the asset with `ScriptableObject.CreateInstance<ArtKit>()`, saves it and stores its two materials inside it. |
| Level | `public static void SfxShowcase.Level.LevelLayout.Build(ShowcaseContext context)` | Needs `context.Art`. Creates the `Level` root with terrain (visual + colliders + `GroundSurface`), every placed object, section / plane / launch triggers and a filled `LevelDescriptor`; sets `context.Level`. |
| Gameplay | `public static void SfxShowcase.Gameplay.GameplayInstaller.Install(ShowcaseContext context)` | Needs `context.Art`, `context.Level`. Creates the player at `Level.SpawnPoint` and the session, wires them; sets `context.Player`, `context.Session`. |
| Level | `public static void SfxShowcase.Level.LevelLayout.BuildCamera(ShowcaseContext context)` | Needs `context.Player`, `context.Level`. Creates `Main Camera` (orthographic, tag `MainCamera`, with the `AudioListener` and the follow-camera component); sets `context.Camera`. |
| Audio | `public static void SfxShowcase.Audio.AudioInstaller.Install(PlayerBase player, GameSessionBase session, SfxSong song, PresetFactory presets)` with `public delegate SfxPreset SfxShowcase.Audio.PresetFactory(string name, SfxPatch patch, float holdSeconds);` | Takes the player and the session, not the context, so the audio folder does not depend on the builder's types. Called through `Editor/ShowcaseAudio.Install`, which supplies the song and the preset factory. Creates the `Audio` object with its `AudioSource`s, `SfxPlayer`s, the `SfxMusicPlayer` and the audio components, wired. For each sound it calls `presets(...)`: the builder's factory creates or updates `Generated/Presets/<name>.asset`, so every sound is also a clickable `SfxPreset` asset. |

Installers are ordinary runtime code (`new GameObject`, `AddComponent`, assigning references) in their
own runtime folders; they contain no `UnityEditor` calls, so a reader sees how to assemble each system
by hand and could call them at runtime.

## 4. How the scene is generated

Nothing is hand-edited: `Assets/Showcase/Showcase.unity` and everything in `Assets/Showcase/Generated`
are the output of `SfxShowcase.Editor.ShowcaseBuilder.Build()` (menu *SFX Forge > Rebuild Showcase
Scene*), in this order:

1. New empty scene. Create / load `Generated/ArtKit.asset`; put it in a `ShowcaseContext`.
2. `LevelLayout.Build(context)` – the level.
3. `GameplayInstaller.Install(context)` – the player and the session.
4. `LevelLayout.BuildCamera(context)` – the camera.
5. `context.Art.CreateCharacter(player)` parented to the player; `CreateBackground(camera, level.Bounds)`;
   `CreateHud(camera, player, session)`.
6. `ShowcaseAudio.Install(player, session)`, which calls `AudioInstaller.Install(player, session, song,
   presetFactory)` with the showcase's own `Song/DemoSong.asset` (written from `Song/DemoSong.bytes` first) and a factory that saves each patch as a preset asset.
7. Store the two materials in the art kit asset and the generated meshes in `Generated/ShowcaseMeshes.asset`,
   save the scene, add it to the build settings (without removing the SfxDemo scene), save assets.

`ShowcaseBuilder.Verify()` opens the saved scene and checks what can be checked without pressing Play –
the level's pieces against the counts `LevelLayout.CountPieces()` gives, the collision conventions of
3.2, that every serialized reference on every showcase component is assigned, that each preset bakes to
a clip that is not silent and each held sound is wired with its note (`ShowcaseAudio.Check`), and the
file-level proofs of `DESIGN.md` section 6 – and exits with 0 or 1. It writes nothing.
`ShowcaseBuilder.SelfTest()` runs `Build()` first and then the same checks. A build makes a new scene
and a new mesh asset, so `Showcase.unity` and `Generated/ShowcaseMeshes.asset` are rewritten wholesale
(new file IDs, a new GUID for the meshes) even when nothing visible changed: use `Verify` to check and
`SelfTest` to rebuild.

Meshes made while building are stored in one asset beside the scene (`ArtKit.CollectGeneratedMeshes()`)
instead of inside it; the two materials are stored in the art kit asset. Meshes that are redrawn at
runtime (HUD text, the speed meter, the trail, the Chime pop) are created by their own components and are
not assets. No Unity layers or tags are created: ground is recognised by its `GroundSurface` component.

## 5. Coding conventions

- C# only. One public type per file; the file has the type's name. Namespace = assembly name.
- Types and methods `PascalCase`, fields and locals `camelCase`, no prefixes, no abbreviations a
  newcomer would have to look up. Events are named in the past tense for what happened (`Jumped`).
- XML `///` comments on every public type and member. Comments say *why*, or what a number means.
- Serialized state is `[SerializeField]` private fields with a `[Tooltip]`, exposed read-only where
  needed and set by one public `Configure(...)` / `Wire(...)` method that the folder's installer
  (or `LevelLayout`) calls. Tunable numbers live in such fields; no magic numbers in logic.
- **No singletons and no static state**, except the `GameEvents` channel. **No `Find…`, no
  `GameObject.Find`, no tags, no `Camera.main`, no `SendMessage` at runtime**: every reference is a
  serialized field wired while the scene is built, or arrives through a trigger callback.
- No dead code, no commented-out code, no TODOs left behind, no editor-only code in runtime folders.
- Units: metres, seconds, degrees. The level runs along +x, up is +y; z is draw order only and
  belongs to the art kit.
- Colours only from the art's `Palette`; no colour literal anywhere else.
- No audio files, textures, models, fonts or third-party assets. Do not write `.meta` files (Unity
  does) and never hand-write scene, prefab or asset YAML.
- Do not change `Packages/manifest.json`, the SFX FORGE package, or `Assets/SfxDemo`. The one project
  setting the showcase touches is the build settings' scene list, to which `Build` adds its scene.

## 6. Building and testing headless

Only one Unity process may have the project open. From the repository root (Git Bash):

```sh
UNITY="C:/Program Files/Unity/Hub/Editor/6000.3.8f1/Editor/Unity.exe"
PROJECT="$PWD/unity/Demo"                           # run from the repository's root
OUT="$PWD/out"

# check: compile, open the saved scene, run the editor checks (writes nothing)
"$UNITY" -batchmode -quit -projectPath "$PROJECT" -executeMethod SfxShowcase.Editor.ShowcaseBuilder.Verify -logFile "$OUT/unity-showcase.log"

# play mode tests (Assets/Showcase/Tests and Assets/SfxDemo/Tests)
"$UNITY" -batchmode -projectPath "$PROJECT" -runTests -testPlatform PlayMode -testResults "$OUT/unity-showcase-playmode.xml" -logFile "$OUT/unity-showcase-playmode.log"

# the existing demo's check, which must keep passing. It regenerates the demo's scene and sets the build
# settings to that scene alone, so it comes BEFORE a rebuild of the showcase.
"$UNITY" -batchmode -quit -projectPath "$PROJECT" -executeMethod SfxDemo.Editor.SfxDemoBuilder.SelfTest -logFile "$OUT/unity-demo.log"

# rebuild: regenerate the scene and assets, then the same checks as Verify (rewrites the generated files)
"$UNITY" -batchmode -quit -projectPath "$PROJECT" -executeMethod SfxShowcase.Editor.ShowcaseBuilder.SelfTest -logFile "$OUT/unity-showcase.log"
```

Exit code 0 is success. Compile errors appear in the log as `error CS`; the self-test prints one
`SHOWCASE: ok` / `SHOWCASE: FAIL` line per check.

## 7. Copy this into your own game

The short version: copy `Scripts/Audio`, give it your own events, and call one installer. The longer
version, with every step, is `../README.md` ("Use SFX FORGE in your own Unity game"). The showcase is under
the package's licence (`GPL-3.0-or-later`), and what you copy from it stays under that licence.

1. **What you need.** The `com.catsplatstudios.sfxforge` package (`Packages/manifest.json`:
   `"com.catsplatstudios.sfxforge": "file:<path>/com.catsplatstudios.sfxforge"`) and an assembly that references
   `CatSplat.SfxForge`. Nothing else: no audio files, no device, no MIDI.

2. **The seam.** `Scripts/Core/GameEvents.cs` is the whole interface between a game and its sound: a
   static class of C# events (`Jumped`, `Landed(impactSpeed)`, `ChimeCollected(combo)` …) with a `Raise…`
   method each. Gameplay raises; audio subscribes in `OnEnable` and unsubscribes in `OnDisable`. Things
   that sound continuously have no event – the audio reads `IPlayerState` every frame. Keep the rule and
   the sound stays removable: gameplay never names a sound, the audio never changes the game.

3. **A sound set in one file.** `Scripts/Audio/SoundLibrary.cs`: each effect is one
   `SfxPatch.Create(engine, …)` (through the named-argument wrapper `Make`, so every value says what it
   is), and one table gives each its route, the MIDI note it is tuned to and its volume. A bare
   `new SfxPatch()` is silent – always start from `Create`. To tune a sound, change a number there and
   rebuild, or turn a knob in the Inspector of `Generated/Presets/<name>.asset` in play mode.

4. **Baked or live?** `SoundRoute.cs` names the three routes; `SoundEffects.cs` and `ContinuousSounds.cs`
   show them.
   - *Baked* – `SfxPreset.Bake(sampleRate, midiNote: …)` once at start, then `AudioSource.PlayOneShot`.
     For short sounds that fire often and are always the same (jump, land, footsteps, Woofer, hurt …).
     Volume and the source's pitch can still follow the game.
   - *Live one-shot* – `SfxPlayer.Play(velocity, semitones, midiNote)` through `LiveVoicePool`. For a
     sound whose pitch is decided per trigger: the Chime climbs a scale with the combo, the scatter runs
     down it.
   - *Live held* – an `SfxPlayer` of its own, `Play(1, 0, midiNote)` to hold (as its note, like every
     pitched sound; a bare `Play()` sounds at the patch's FREQ) and `Release()` to let go, and
     `pitchSemitones`, `cutoffOctaves`, `fm`, `level` written every frame: the roll hum, the wind and the
     Wind-Up rev.

5. **Adaptive music in a handful of lines.** `MusicDirector.cs` drives one `SfxMusicPlayer`. The song
   plays in its own order (the demo song's 16-bar arrangement); the game decides how much of the band
   plays it: `music.intensity = …` runs from the section's floor when the player stands still to 1 at
   top speed (fast in, slow out). `music.CuePattern(n)` is used in the two places that want one
   pattern – the breakdown while the player is in Static Field (`SectionEntered`) and the turnaround
   from the last launch (`FinalLaunch`) – and again on every bar line while that hold lasts, because a
   song with an arrangement treats a cue as "jump to the arrangement's next bar with this pattern" and
   then walks on; only a pattern outside the arrangement, or a song with no arrangement and a chain of 1, holds a cue by itself.
   When the hold ends the arrangement walks on from where it is. Then
   `music.PlaySound(instrument, velocity, note)` for the checkpoint's bell answer (started on the song's
   next sixteenth: notes by hand are not quantised) and the goal's fanfare, `music.volume` for the dip
   when hurt (ramped over 50 ms here: the player applies `volume` per audio block without smoothing), and
   `Stop()` / `Reload()` / `Play()` around the goal and a restart.

6. **Wiring it up.** `AudioInstaller.Install(player, session, song, presetFactory)` builds the whole `Audio`
   object in plain runtime code – read it top to bottom to do the same by hand: an `AudioSource` with an
   `SfxMusicPlayer` (assign the `SfxSong`), an `AudioSource` for baked one-shots and a second one for the footsteps (its pitch follows speed), one `AudioSource` +
   `SfxPlayer` per held sound (assign its `SfxPreset`), and a few `AudioSource` + `SfxPlayer` pairs with
   no preset for the pool. Every source is 2D with no clip and `playOnAwake` off; the players keep their
   sources running themselves.

7. **Lifting the audio folder out.** Copy `Scripts/Audio` (17 scripts and its `.asmdef`). From `Scripts/Core` it needs
   exactly these ten files: `GameEvents`; `PlayerBase` with the interfaces it implements, `IPlayerState`
   (speed, grounded, rolling, charging), `IPlayerActor` and `IPlayerInput`, and `TerrainPlane`, which
   those name; `GameSessionBase` with `IGameSession`, `GamePhase` and `SectionId`. The folder's
   `SfxShowcase.Audio.asmdef` references the assembly `SfxShowcase.Core` by name: change that to the
   assembly you put them in. It does not need `ShowcaseContext`, the art kit or the level. Either copy
   those ten files and raise the events from your game, or replace the subscriptions in `OnEnable` with
   your own events and the `IPlayerState` reads in `ContinuousSounds.Update` and
   `MusicDirector.TargetIntensity` with your own values, and take nothing from Core. `SectionId` in
   `SectionCue` becomes your own area enum. Delete the handlers you have no event for; nothing else
   depends on them.

8. **Limits.**
   - The synthesizer is a native library, Windows x64 only today. Elsewhere `SfxNative.Available` is
     false, `Bake` returns null and the players stay silent; the showcase then runs without sound and
     logs a warning (one from `SoundEffects`, one each from the package's two player components).
   - `SfxPlayer` and `SfxMusicPlayer` are not spatialised (they write into a 2D `AudioSource`). For a 3D
     one-shot, bake a clip and play it on a 3D source.
   - One `SfxPlayer` holds one patch (up to 16 overlapping notes of it); a different patch reshapes what
     still sounds, which is why live one-shots go through a small pool.
   - Baking is synchronous: the 14 baked sounds are rendered in `SoundEffects.Awake`. Spread the bakes
     over frames, or bake in the editor (`Bake WAV` on a preset), if a start-up hitch matters.
