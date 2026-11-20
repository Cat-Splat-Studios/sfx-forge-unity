# The device's side: SFX FORGE 1.4.0 for VIBE FM

`sfx-forge.zip` is the built instrument (a "Vibe"). Drop the zip onto the window of the VIBE FM plugin; or unzip it
and drop `sfx-forge.wasm` onto the VIBE FM Playground, <https://play.thefirmwarehub.com/>, in Chrome or Edge.

How to play it: [the device's manual](../Documentation~/MANUAL.md). How to join it to Unity:
[section 6 of the Unity manual](../Documentation~/UNITY.md#6-from-the-device-to-the-game). This build (1.4.0) was run in
the VIBE FM SDK's command-line tools (its checks hand the built `.wasm` events and SysEx and read its sound,
its display and its MIDI) and in the SDK's local emulator, where the link tests of the Unity manual passed
against it. It was not run in the plugin or on hardware. Its source is not in this package.

The `.wasm` is SFX FORGE's code compiled together with the VIBE FM SDK's runtime and its display fonts
(Apache-2.0 and SIL OFL 1.1): see [Third Party Notices](../Third%20Party%20Notices.md) and the licence texts in
[`Documentation~/licenses`](../Documentation~/licenses). It carries no debug information.
