# The device's side: SFX FORGE 1.7.0 for VIBE FM

`sfx-forge.zip` is the built instrument (a "Vibe"). Drop the zip onto the window of the VIBE FM plugin; or unzip it
and drop `sfx-forge.wasm` onto the VIBE FM Playground, <https://play.thefirmwarehub.com/>, in Chrome or Edge.

How to play it: [the device's manual](../Documentation~/MANUAL.md). How to join it to Unity:
[section 6 of the Unity manual](../Documentation~/UNITY.md#6-from-the-device-to-the-game).

This build (1.7.0) was run without the emulator's page, in the VIBE FM SDK's command-line tools and, for
SysEx and stored files, in a small host of the source repository's own: its checks hand the built `.wasm`
events and SysEx and read its sound, its MIDI and what it stores, and all 22 of them pass. A few of them also
compare the display, to tell that it changed or that the LINK light is on: what a page shows and what a toast
says is not read. Against
Unity, the six link tests of the Unity manual passed in the SDK's local emulator on 24 Nov 2026, in one run,
with songs of sixteenths only and the beat a quarter. That was the build before this one: this one differs
in one text of the display, the LINK page's footer (`CC 20-23` became `CC 20-25`), and the link tests were
not run again with it. The session of the link video
was recorded with the 1.7.0 build of 23 Nov 2026, before the beat was added; with four quarters the display
is the same, and it was not recorded again.
It was not run in the plugin or on hardware. Nobody has played its new page, PERFORM, by hand, nobody has
set a time signature by hand, and nobody has listened to what is new in it. Its source is not in this
package.

New in 1.7.0: PERFORM, a fourth page of the K5 key (K5 walks SEQ, SONG, AUTO, PERFORM). It shows the whole
pattern as a grid, one row for each of the 16 sounds, and F1 to F4 choose what the pads are: the steps of
the selected sound, the kit, mutes, or the patterns and the intensity
([the device's manual](../Documentation~/MANUAL.md#13-perform-the-whole-pattern-and-the-pads-four-ways)). It
changes nothing in this package's code, in the messages or in the formats: a device with 1.6.0 and one with
1.7.0 speak the same song format 6, and the package works with either. The page was tested by a script that
presses its keys and does not look at the display.

Also new in 1.7.0: a beat and a time signature. The bar's length was the song's already (encoder B on
SONG and PERFORM, 1 to 16 sixteenths). New is the beat: SHIFT + B on SEQ, SONG and PERFORM chooses an
eighth, three sixteenths, a quarter (as the device comes) or a dotted quarter. The click, the shades of
the grids and FILL follow it, and the display names the bar by it: 12 sixteenths are `BAR 3/4` with a
quarter and `BAR 6/8` with a dotted quarter ([the device's
manual](../Documentation~/MANUAL.md#the-bar-and-the-beat-34-68-78)). The beat is the device's, not the
song's: it is not in the packed song and in no message, a game never gets it, and it changes no note.
The device keeps it in a file of its own, `meter.bin`. Nothing in the package's code or formats changes:
the package has nothing to read, and a bar of 12 sixteenths plays the same in Unity whether the device
calls it 3/4 or 6/8. With four quarters, as the device comes, nothing looks, sounds or is stored
differently than before. Not there: a bar longer than 16 sixteenths (no 5/4, 6/4, 9/8 or 12/8),
triplets, a length or a beat for each pattern, groupings like 2+2+3. The beat was tested by a script
that reads what FILL writes, where the click falls, the song's bytes and the store; it does not read the
display's words, and nobody has listened to the click.

New in 1.6.0: each pattern has its own grid of 1/16, 1/32 or 1/64, which is 16, 32 or 64 steps a bar (SHIFT+F5
and SHIFT+F6 on the SEQ and AUTO pages: coarser and finer; SHIFT+LEFT and SHIFT+RIGHT: the 16 steps before and
after). With it the song is in format 6, which has no fixed length (the factory song is 2487 bytes), the song
message has a new shape (pieces of up to 240 bytes, 27 messages for the factory song with its 16 sounds), the
song is stored in `song6.bin`, and the identity says song format 6 and 64 steps. Songs of the formats 1 to 5
and the song message of before are still taken.

**This firmware and this package belong together.** A firmware up to 1.5.0 drops the new song message without a
word, so the package does not send a format-6 song to one and says why (`SfxLink.Refusal`,
[the Unity manual](../Documentation~/UNITY.md#when-a-song-is-not-sent)). A package from before firmware 1.6.0 takes no
song from this firmware and does not read a format-6 song asset. An older firmware's `song.bin` is still read,
and never written: put the older firmware back and its song is still there.

New in 1.5.0, on the sequencer pages: LOOP and SONG (F1: a pattern repeats, or the song plays in order), a
pattern strip that shows which pattern plays, which is shown and which waits, REC that plays the pads as the
kit and loops the pattern, ERASE while recording (F4), and UNDO (F2, one step, again for redo).

Licence: the firmware is under the GNU General Public License, version 3 or any later version
(GPL-3.0-or-later), as its manifest says: [LICENSE.md](../LICENSE.md). An additional permission allows
combining it with the hosts of VIBE FM firmwares, which are not free software:
[ADDITIONAL-PERMISSIONS.md](../ADDITIONAL-PERMISSIONS.md).

The `.wasm` is SFX FORGE's code compiled together with the VIBE FM SDK's runtime and its display fonts
(Apache-2.0 and SIL OFL 1.1): see [Third Party Notices](../Third%20Party%20Notices.md) and the licence texts in
[`Documentation~/licenses`](../Documentation~/licenses). It carries no debug information.
