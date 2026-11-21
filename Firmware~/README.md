# The device's side: SFX FORGE 1.5.0 for VIBE FM

`sfx-forge.zip` is the built instrument (a "Vibe"). Drop the zip onto the window of the VIBE FM plugin; or unzip it
and drop `sfx-forge.wasm` onto the VIBE FM Playground, <https://play.thefirmwarehub.com/>, in Chrome or Edge.

How to play it: [the device's manual](../Documentation~/MANUAL.md). How to join it to Unity:
[section 6 of the Unity manual](../Documentation~/UNITY.md#6-from-the-device-to-the-game).

This build (1.5.0) was run in the VIBE FM SDK's command-line tools: its checks hand the built `.wasm`
events and SysEx and read its sound and its MIDI, and all of them pass. One of them also compares the display, for
the LINK light only: what a page shows and what a toast says is not checked. It was not run against
Unity: the link tests of the Unity manual were last run with firmware 1.4.0, in the SDK's local emulator, and
were not repeated with this build. Its song format, its messages and its identity are those of 1.4.0, apart
from the version it reports. It was not run in the plugin or on hardware, and nobody has listened to what is
new in it. Its source is not in this package.

New in 1.5.0, on the sequencer pages: LOOP and SONG (F1: a pattern repeats, or the song plays in order), a
pattern strip that shows which pattern plays, which is shown and which waits, REC that plays the pads as the
kit and loops the pattern, ERASE while recording (F4), and UNDO (F2, one step, again for redo).

The `.wasm` is SFX FORGE's code compiled together with the VIBE FM SDK's runtime and its display fonts
(Apache-2.0 and SIL OFL 1.1): see [Third Party Notices](../Third%20Party%20Notices.md) and the licence texts in
[`Documentation~/licenses`](../Documentation~/licenses). It carries no debug information.
