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
  - For designing sounds: `SfxGenerator` (a new sound of one of ten kinds, from a seed), `SfxMutate` and
    `SfxRandom` (variants that can be made again, with an amount and locks), `SfxVariationSet` and
    `SfxVariationPlayer` (one sound as several, shuffled), `SfxBake` and `SfxBakeOptions` (a patch as samples at a
    rate, mono, at a note, trimmed, normalised), `SfxLevels` (peak, RMS, matching by LEVEL), `SfxPatchText` (a
    sound as 62 characters of text and as a `.syx` file).
- **Reserved for later versions**, so that a project made with this one does not break when the Unity side can do
  more than a device, or a firmware for other hardware appears:
  - The identity (firmware 1.4.0): the link asks the device what it is (`SfxLink.RequestIdentity`, `Device`,
    `IdentityReceived`; `SfxDeviceInfo`). A firmware before 1.4.0 does not answer and works as before. A device that
    says it speaks another patch or song format is sent none (`SfxLink.LastRefusal`).
  - `SfxProfile`: a device's limits as numbers; `SfxProfile.VibeFm1` is the only one there is. Every sound and
    song of this version fits it.
  - `SfxPreset` and `SfxSong` keep the asset format that wrote them (`Format`, 1) and the one that saved them
    last. An asset from a newer SFX Forge says so in its Inspector and once in the Console.
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
    changed in it, nothing else.
  - The window (`SfxForgeView` over an `SfxForgeModel`): the link's state, what the device says it is, the
    device's knobs, 16 pads that play or fetch, a text for the device, the patches that arrived with their
    waveform, the last messages.
  - The kit they are built from, in `Editor/UI` (`Knob`, `ValueTile`, `WaveformView`, `PadGrid`, `SoftKeyRow`,
    `SfxUnits`, `SfxForge.uss`), with a README of its own.
  - `SfxSong` has read-only accessors for what the song inspector shows: steps, notes, velocities, lengths, the
    mix, the master effects, the automation lanes.
- **Sample** "Showcase": a one-level game whose every sound is SFX FORGE, with its own song, editor checks, Play
  mode tests, a Windows build command and a self-check in the built game.
- **Documentation~**: the Unity manual, the device's manual with the cheat sheet, the link's protocol, five videos.
- **Firmware~**: the built instrument, firmware 1.5.0, without debug information.
- The native library's import settings name the editor and Windows x64 only.

The code is the package that was `com.catsplat.sfxforge` 0.1.0 inside the source repository. The id, the display name and the
version are new; namespaces, assembly names and asset GUIDs are unchanged.

### Known limits

- Windows x64 only; MIDI on Windows only. Developed and tested with Unity 6000.3.8f1 only.
- The link was tested against the VIBE FM SDK's emulator over a virtual MIDI cable, not against hardware or the
  plugin.
- Nobody has listened to the audio. Levels and "sound comes out" are measured (peaks, RMS, the build's probe).
- If a song transfer breaks off and a different song follows within two seconds, pieces of the two can be mixed.
- The identity was not asked of a device by the package's own tests, only built and read. There is one profile
  and one device; nothing can be targeted at another, and nothing converts a newer patch, song or asset.
- `loopPattern` is tested in the engine through the native library, and inside Unity by one Play mode test of the
  sample, headless and muted; nobody has listened to it. Firmware 1.5.0 passes its own checks; the link was
  last run against 1.4.0.
- The tools for designing sounds are measured, not heard: nobody has listened to a generated sound, a variant or
  a baked file. A variation set that is not baked plays only where the native library is, and nothing checks at
  build time that a set is baked or up to date.
- The Inspectors and the two windows were tested as elements in a test panel (147 EditMode tests in the source
  repository), and the pictures of them in these pages were rendered there. Nobody has opened them in the editor
  itself; their menus, typing into a value, and their keys against a device were not tried. No keys: the arrow
  keys on a tile, ENTER in the window's text field and the double-click that opens a value's field were never
  sent; what they call was called directly.
- Tried on the generated package, headless: added to an empty project from disk, the sample imported with the
  Package Manager's import call, with and without the Test Framework, its checks, tests, Windows build and the
  build's probe (the README, "What is tested", has the numbers, and which of it was repeated with the tools,
  `loopPattern` and firmware 1.5.0: the sample's checks and tests, not the build). Not tried: the Package Manager's buttons clicked
  by hand, a git URL, a project with another render pipeline or the Input System alone.
- Straight after the import, one of the sample's 32 editor checks fails (the scene is not in the build settings)
  until **SFX Forge > Rebuild Showcase Scene** is run or the scene is added by hand.
