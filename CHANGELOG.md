# Changelog

## [1.0.0] - 2026-11-20

The first version of the standalone package. It is generated from the SFX FORGE repository (the firmware, the engine
and the Unity code live there) by `tools/make_unity_package.py`; nothing in it is edited by hand.

### In it

- **Runtime** (`CatSplat.SfxForge`): `SfxPatch`, `SfxPreset`, `SfxSong`, `SfxPlayer`, `SfxMusicPlayer`, `SfxNative`
  with the native library for Windows x64, `SfxWav`, and the link: `SfxLink`, `SfxProtocol`, `SfxForgeLink`,
  `SfxTelemetrySender`, `SfxEncoderBinding`.
  - `SfxMusicPlayer.loopPattern`: the pattern that plays repeats until `CuePattern` chooses another. The native
    library has the engine's loop switch for it (`sfxn_player_set_loop`).
  - The finer grid of firmware 1.6.0 (song format 6): each pattern has a grid of 16, 32 or 64 steps a bar.
    `SfxSong.Grid`, `StepsPerSixteenth`, `GridSteps`, `HasFinerGrid`, `PackedFormat` and `GridStepOn`,
    `GridStepNote`, `GridStepVelocity`, `GridStepLength`, `GridLanePoint` read every step; the older `StepOn`,
    `StepNote`, `StepVelocity`, `StepLength` and `LanePoint` count sixteenths and read the first step of each.
    `SfxMusicPlayer.Step` stays the sixteenth; `GridStep` and `StepsPerSixteenth` are new.
  - `SfxPackedSong` reads a packed song of any format from bare bytes (`Format`, `IsValid`, `IsPlayable`, `Div`,
    `Grid`, `StepOn`, `StepNote`, `StepVelocity`, `StepLength`, `LanePoint`). `SfxSongTransfer` collects a song that
    arrives in pieces, in either shape of the song message.
  - `SfxProtocol`: `SongVersion` 6, `GridSteps` 64, `SongHead`, `SongBytesMin` (211), `SongBytesMax` (38355),
    `SongBytesV5` (7699), `SongPieceBytes` (240), `SongPiecesMax` (160), `BuildSongPieces`. A song has no fixed
    size any more: `SongBytes`, `SongChunkBytes`, `SongChunks` and `TryTakeSongChunk` are obsolete and kept.
  - `SfxNative` (the library's ABI is 5): `SongConvert` (an older song as format 6), `SongRegrid` and
    `SongRegridLoss` (a pattern in another grid, and what a coarser one would drop), `PlayerGrid`.
  - For designing sounds: `SfxGenerator` (a new sound of one of ten kinds, from a seed), `SfxMutate` and
    `SfxRandom` (variants that can be made again, with an amount and locks), `SfxVariationSet` and
    `SfxVariationPlayer` (one sound as several, shuffled), `SfxBake` and `SfxBakeOptions` (a patch as samples at a
    rate, mono, at a note, trimmed, normalised), `SfxLevels` (peak, RMS, matching by LEVEL), `SfxPatchText` (a
    sound as 62 characters of text and as a `.syx` file).
- **Reserved for later versions**, so that a project made with this one does not break when the Unity side can do
  more than a device, or a firmware for other hardware appears:
  - The identity (firmware 1.4.0): the link asks the device what it is (`SfxLink.RequestIdentity`, `Device`,
    `IdentityReceived`; `SfxDeviceInfo`). A firmware before 1.4.0 does not answer and is taken for one that speaks
    song format 5. A device that cannot take what is sent is sent nothing, and `SfxLink.LastRefusal` says why.
    `SfxLink.Refusal(device, packedSong)` asks beforehand: a firmware up to 1.5.0 takes a format-5 song only, and
    a song with a value out of range goes to no device.
  - `SfxProfile`: a device's limits as numbers. `SfxProfile.VibeFm1` is firmware 1.6.0 on (song format 6, 64
    steps a pattern) and every sound and song of this version fits it; `SfxProfile.VibeFm15` is what the firmwares
    1.4 and 1.5 said of themselves (song format 5, 16 steps).
  - `SfxPreset` and `SfxSong` keep the asset format that wrote them (`Format`) and the one that saved them
    last. A sound's is 1. A song's is 2 when it holds a format-6 song and 1 with an older one
    (`SfxSong.CurrentFormat` is 2). An asset from a newer SFX Forge says so in its Inspector and once in the Console.
  - A patch or a song with a higher format number is reported as newer, not as broken (`SfxPatch.IsNewer`,
    `SfxSong.IsNewer`, `SfxLink.NewerReceived` for one that arrives from the device).
  - There is no target switch and no feature the device lacks: this version only reserves the room.
- **Editor** (`CatSplat.SfxForge.Editor`): the Inspectors of presets and songs and the window **Window > SFX
  Forge**, built with UI Toolkit to look like the device's display, and `SfxForgeEditorLink`.
  - The sound inspector (`SfxPresetPanel`): the pages OSC, TONE, FILTER, WAVE, ENV, LFO and FX with the engine and
    all 25 values as knobs, each value in the device's units; the sound's waveform as the device draws it; HOLD for
    held sounds; the keys PLAY, MUTATE, UNDO, RANDOM, BAKE WAV, SEND, HEAR CHANGES, PLAY ON DEVICE, SEND CHANGES.
    Under the picture, the sound's peak and RMS. Under the keys, four tools: NEW (ten kinds of sound), VARY
    (MUTATE's amount and locks, MORE LIKE THIS with eight variants, MAKE SET), LEVEL (MATCH RMS, MATCH PEAK) and
    SHARE (the sound as text; EXPORT .SYX, IMPORT .SYX).
  - The Inspector of a variation set (`SfxVariationSetPanel`) with its BAKE (`SfxVariationBaker`), and the window
    **Window > SFX Forge Bake** (`SfxBakeView` over an `SfxBakeModel`; `SfxBatchBake` does the work).
  - The song inspector (`SfxSongPanel`): tempo, steps, swing, chain, the patterns A – H, the arrangement, and the
    pages STEPS, MIX, MASTER and AUTO. It is a viewer: TEMPO, SWING and a sound's LAYER, VOLUME and PAN can be
    changed in it, nothing else. It shows each pattern in its own grid: the title says `PATTERN B  1/32`, the rows
    draw 16, 32 or 64 steps, a finer pattern's tile has `32` or `64` in its corner. The line under its keys says
    LINKED, NOT CONNECTED, ANOTHER FORMAT or A DAMAGED SONG, and why SEND is off.
  - The window (`SfxForgeView` over an `SfxForgeModel`): the link's state, what the device says it is, the
    device's knobs, 16 pads that play or fetch, a text for the device, the patches that arrived with their
    waveform, the last messages.
  - The kit they are built from, in `Editor/UI` (`Knob`, `ValueTile`, `WaveformView`, `PadGrid`, `SoftKeyRow`,
    `SfxUnits`, `SfxForge.uss`), with a README of its own.
  - `SfxSong` has read-only accessors for what the song inspector shows: each pattern's grid, steps, notes,
    velocities, lengths, the mix, the master effects, the automation lanes.
- **Sample** "Showcase": a one-level game whose every sound is SFX FORGE, with its own song, editor checks, Play
  mode tests, a Windows build command and a self-check in the built game.
- **Documentation~**: the Unity manual, the device's manual with the cheat sheet, the link's protocol, five videos.
- **Firmware~**: the built instrument, firmware 1.7.0, without debug information. Firmware 1.7.0 adds the
  PERFORM page on the device: the whole pattern as a grid, and the pads as steps, the kit, mutes or patterns.
  Nothing in the package's code or formats changes with it: a device with 1.6.0 and one with 1.7.0 speak the same
  song format 6.
- The native library's import settings name the editor and Windows x64 only.

The code is the package that was `com.catsplat.sfxforge` 0.1.0 inside the source repository. The id, the display name and the
version are new; namespaces, assembly names and asset GUIDs are unchanged.

### Known limits

- Windows x64 only; MIDI on Windows only. Developed and tested with Unity 6000.3.8f1 only.
- The link was tested against the VIBE FM SDK's emulator over a virtual MIDI cable, not against hardware or the
  plugin.
- Nobody has listened to the audio. Levels and "sound comes out" are measured (peaks, RMS, the build's probe).
- If a song transfer breaks off and a different song of as many pieces follows within two seconds, pieces of the
  two can be mixed.
- Songs are in format 6 (firmware 1.6.0). A firmware up to 1.5.0 drops the new song message without a word, so the
  package refuses in words before it sends one a format-6 song; nothing turns a format-6 song into format 5. A
  package from before firmware 1.6.0 does not read a format-6 song: it shows the banner of a newer asset, then NO SONG YET,
  and plays nothing. Neither an older firmware nor an older package was run against this one.
- The package reads and shows a pattern's grid and does not set it: it is chosen on the device. The demo song and
  the sample use sixteenths only, and nobody has listened to a finer pattern in Unity.
- The identity was not asked of a device by the package's own tests, only built and read. There is one device and
  one profile id; nothing can be targeted at another, and nothing converts a newer patch, song or asset.
- `loopPattern` is tested in the engine through the native library, and inside Unity by one Play mode test of the
  sample, headless and muted; nobody has listened to it. Firmware 1.7.0 passes its own checks; the link was
  last run against it in the emulator (six tests and the recorded session, songs of sixteenths only). Its PERFORM
  page was tested by a script only: nobody has played it by hand, and it was not run on hardware or in the plugin.
- The tools for designing sounds are measured, not heard: nobody has listened to a generated sound, a variant or
  a baked file. A variation set that is not baked plays only where the native library is, and nothing checks at
  build time that a set is baked or up to date.
- The Inspectors and the two windows were tested as elements in a test panel (169 EditMode tests in the source
  repository), and the pictures of them in these pages were rendered there. Nobody has opened them in the editor
  itself; their menus, typing into a value, and their keys against a device were not tried. No keys: the arrow
  keys on a tile, ENTER in the window's text field and the double-click that opens a value's field were never
  sent; what they call was called directly.
- Tried on the generated package, headless: added to an empty project from disk, the sample imported with the
  Package Manager's import call, with and without the Test Framework, its checks, tests, Windows build and the
  build's probe (the README, "What is tested", has the numbers, and which of it was repeated with the tools,
  `loopPattern` and firmware 1.5.0, and again with firmware 1.7.0 and song format 6: the sample's checks and
  tests, not the import call and not the build). Not tried: the Package Manager's buttons clicked
  by hand, a git URL, a project with another render pipeline or the Input System alone.
- Straight after the import, one of the sample's 32 editor checks fails (the scene is not in the build settings)
  until **SFX Forge > Rebuild Showcase Scene** is run or the scene is added by hand.
