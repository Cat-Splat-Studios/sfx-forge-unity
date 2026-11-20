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
- **Sample** "Showcase: Brio - First Harmonic": a one-level game whose every sound is SFX FORGE, with its own song, editor checks, Play
  mode tests, a Windows build command and a self-check in the built game.
- **Documentation~**: the Unity manual, the device's manual with the cheat sheet, the link's protocol, five videos.
- **Firmware~**: the built instrument, firmware 1.3.1.

The code is the package that was `com.catsplat.sfxforge` 0.1.0 inside the source repository. The id, the display name and the
version are new; namespaces, assembly names and asset GUIDs are unchanged.

### Known limits

- Windows x64 only; MIDI on Windows only. Developed and tested with Unity 6000.3.8f1 only.
- The link was tested against the VIBE FM SDK's emulator over a virtual MIDI cable, not against hardware or the
  plugin.
- Nobody has listened to the audio. Levels and "sound comes out" are measured (peaks, RMS, the build's probe).
- If a song transfer breaks off and a different song follows within two seconds, pieces of the two can be mixed.
- Not yet tried when this was written: this package added to a fresh project, the sample imported through the
  Package Manager, a project without the Test Framework.
