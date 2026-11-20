# Changelog

## [1.0.0] - 2026-11-20

The first version of the standalone package. It is generated from the SFX FORGE repository (the firmware, the engine
and the Unity code live there) by `tools/make_unity_package.py`; nothing in it is edited by hand.

### In it

- **Runtime** (`CatSplat.SfxForge`): `SfxPatch`, `SfxPreset`, `SfxSong`, `SfxPlayer`, `SfxMusicPlayer`, `SfxNative`
  with the native library for Windows x64, `SfxWav`, and the link: `SfxLink`, `SfxProtocol`, `SfxForgeLink`,
  `SfxTelemetrySender`, `SfxEncoderBinding`.
- **Editor** (`CatSplat.SfxForge.Editor`): the window **Window > SFX Forge**, the Inspectors of presets and songs,
  `SfxForgeEditorLink`.
- **Sample** "Showcase": a one-level game whose every sound is SFX FORGE, with its own song, editor checks, Play
  mode tests, a Windows build command and a self-check in the built game.
- **Documentation~**: the Unity manual, the device's manual with the cheat sheet, the link's protocol, five videos.
- **Firmware~**: the built instrument, firmware 1.3.1, without debug information.
- The native library's import settings name the editor and Windows x64 only.

The code is the package that was `com.catsplat.sfxforge` 0.1.0 inside the source repository. The id, the display name and the
version are new; namespaces, assembly names and asset GUIDs are unchanged.

### Known limits

- Windows x64 only; MIDI on Windows only. Developed and tested with Unity 6000.3.8f1 only.
- The link was tested against the VIBE FM SDK's emulator over a virtual MIDI cable, not against hardware or the
  plugin.
- Nobody has listened to the audio. Levels and "sound comes out" are measured (peaks, RMS, the build's probe).
- If a song transfer breaks off and a different song follows within two seconds, pieces of the two can be mixed.
- Tried on the generated package, headless: added to an empty project from disk, the sample imported with the
  Package Manager's import call, with and without the Test Framework, its checks, tests, Windows build and the
  build's probe (the README, "What is tested", has the numbers). Not tried: the Package Manager's buttons clicked
  by hand, a git URL, a project with another render pipeline or the Input System alone.
- Straight after the import, one of the sample's 32 editor checks fails (the scene is not in the build settings)
  until **SFX Forge > Rebuild Showcase Scene** is run or the scene is added by hand.
