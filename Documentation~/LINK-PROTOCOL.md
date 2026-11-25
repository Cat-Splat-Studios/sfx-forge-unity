# SFX FORGE – the link, byte by byte

What travels between the device and a game engine. The Unity package implements it in
`Runtime/Core/SfxProtocol.cs` and `Runtime/Core/SfxLink.cs`; [section 6 of the Unity manual](UNITY.md#6-from-the-device-to-the-game)
says how to use it and what of it is tested. The C functions named here (`sfx_song_pack`, `sfx_player_note_on`)
are the engine's, and `tests/check-identity.mjs`, `check-wire.mjs`, `check-store.mjs` and `build.sh` are the firmware's
checks and its build script:
all of them are in the source repository, not in this package.

Plain MIDI, so it works over USB, the plugin's MIDI ports and Web MIDI. SysEx uses the non-commercial ID `7D`, then `56 42`.

| Direction | Message | Meaning |
|---|---|---|
| device → game | Note On/Off, ch 1, notes 36–51 | a pad was played |
| device → game | CC 20–25, ch 1 | encoders 1–4, A, B on the LINK page |
| device → game | `F0 7D 56 42 01 <slot> <28 patch bytes> F7` | EXPORT: one patch |
| game → device | Note On/Off, notes 36–51 | play a pad |
| game → device | CC 10–13, ch 2 | telemetry: pitch (0…+24 st), cutoff (64 = none, ±4 oct), timbre (FM depth, wavetable position), level |
| game → device | CC 14, ch 2 | music: intensity 0…127 (layers come in) |
| game → device | CC 15, ch 2 | music: pattern A…H (value / 16), from the next bar line |
| game → device | CC 16–17, ch 2 | not mapped: kept, and like any telemetry they switch the link light on. The LINK page draws CC 10–15 only |
| game → device | `F0 7D 56 42 02 <slot> <28 patch bytes> F7` | load a patch into a pad. Slot 16–31 (firmware 1.3.1): pad 1–16 of the MUSIC kit whichever kit is shown – how a song's sounds are sent |
| game → device | `F0 7D 56 42 03 <ASCII, ≤ 24> F7` | text for the bottom line of the LINK page: it shows only while that page is open |
| game → device | `F0 7D 56 42 04 <slot> F7` | request a dump of a pad |
| both ways | `F0 7D 56 42 05 <piece lo> <piece hi> <format> <pieces lo> <pieces hi> <1–240 bytes> F7` | the song with its mix (`sfx_song_pack`), from firmware 1.6.0 on: the piece's number and the count of pieces in 14 bits each, the song's format in every piece, at most 251 bytes a message and 160 pieces. Every piece but the last has 240 bytes. The factory song, 2487 bytes of format 6, is 11 pieces. A receiver puts each piece where it belongs and takes the song when all are there: in any order, twins ignored. It forgets a transfer that stops for two seconds; a *different* song of as many pieces sent sooner than that after one that broke off can be mixed with it. A second whole song sent straight after the first is taken when any of its pieces differs; the same song again loads nothing. A first piece that does not begin with the format it names is ignored |
| both ways up to firmware 1.5.0, then game → device | `F0 7D 56 42 05 <piece> 39 <200 bytes, the last 99> F7` | the song message up to firmware 1.5.0, which sends it and takes it: a format-5 song of 7699 bytes in 39 pieces. Firmware 1.6.0 still takes it and no longer sends it; the Unity package takes it from an older device. A firmware up to 1.5.0 knows this one only, and drops the message above without a word |
| game → device | `F0 7D 56 42 06 F7` | request the 16 sounds and the song (as SEND does) |
| game → device | `F0 7D 56 42 07 F7` | request the identity (firmware 1.4.0 on). Exactly these 6 bytes; with any other length it is ignored |
| device → game | `F0 7D 56 42 08 <18 bytes> F7` | the identity: which hardware, which firmware, what it holds (below). A device that receives one ignores it |

`<slot>` is 0–15, a pad of the kit the device shows. SEND and the request `06` always give the MUSIC kit: the song's
sounds. A game plays sound effects beside the song with slots 16–31 of the player (`sfx_player_note_on`). Telemetry also arrives as the DAW parameters Mod Pitch / Cutoff / FM / Level and Intensity.

### The host's parameters

What the host (the plugin in a DAW, a mixer controller) sees of the device: 71 parameters, in this order. The first
six are as they were before firmware 1.7.1; the mixer's 65 came with 1.7.1 and follow the VIBE FM SDK's mixer
convention (SDK 11; the SDK's `CONTROLLERS.md`, section 2).

| Parameter (id) | Name | Group | Range |
|---|---|---|---|
| `volume` | Volume | OUTPUT | 0…1 |
| `mod_pitch` | Mod Pitch | TELEMETRY | 0…24 st |
| `mod_cutoff` | Mod Cutoff | TELEMETRY | −4…+4 oct |
| `mod_fm` | Mod Timbre | TELEMETRY | 0…1 |
| `mod_level` | Mod Level | TELEMETRY | 0…1 |
| `intensity` | Intensity | MUSIC | 0…1 |
| `mix_<n>_level`, n = 1…16 | Level n | MIX n | 0…1: the fader of sound n, the song's 0–127 divided by 127 |
| `mix_<n>_pan` | Pan n | MIX n | −1…+1: all left, 0 the middle, all right (the song's 0, 64 and 127) |
| `mix_<n>_mute` | Mute n | MIX n | 0 or 1 (two steps): 1 is muted |
| `mix_<n>_solo` | Solo n | MIX n | 0 or 1 (two steps): 1 is solo |
| `mix_select` | Selected sound | MIX | 1…16 (16 steps): the selected sound |

The mixer's parameters stand four a sound (level, pan, mute, solo: sound 1's, then sound 2's, up to sound 16's), then
`mix_select`. There is no `mix_<n>_arm`: REC is one key for the whole song, not one for each sound.

- **Nothing new is kept for them.** The values are the song's own mix and the selected sound, as the MIX page shows
  them.
- **To the host.** After every event and every draw the firmware compares each value with what the host was told
  last and tells it those that differ; at the start, on every init, it tells all 65. So it does not matter what
  moved a value: a fader turned on MIX, a pad that mutes, ALL ON, UNDO, NEW, a song from a game. (The SDK's text
  suggests comparing once per audio block; it is done in the control flow instead, and the comment over `mix_report`
  in `sfx-forge/fw_pages.h` says why.) A pad played, or a note 36–51 from outside, selects that sound, so
  `mix_select` is told with it.
- **From the host.** A value the host sets goes into the song (`mix_select`: it selects the sound) and is not said
  back to the host. A level is rounded to the song's 0–127 and a pan to 0–127 with 64 the middle; a mute or a solo
  is on from 0.5. No undo step is made for it, as none is made for the MIX page's own faders and mutes. A value
  between two steps is rounded and not said back, so the host and the device can differ by up to half a step of 127.
- **Not taken.** A value that is no number (NaN or an infinity), and `mix_select` while the tour runs. The host is
  then told what the value really is; in the tour's case that is when the tour is over. The tour's own selections are
  not told to the host.
- **Which sounds.** Levels, pans, mutes and solos are always the song's sounds (the MUSIC kit). `mix_select` is the
  selected pad's number in whichever kit is chosen, as on the MIX page.

Tested by `tests/check-mixer.mjs` on the built firmware, through the SDK's own host functions (`vibe_wasm_param_set`,
`_value`, `_changed`). Not tried: a real mixer controller, the VIBE FM plugin, a DAW, the Playground. In the
plugin's Editor view the mixer is 17 more groups, and a mute or a solo is a plain value of two steps: that is read
from the list, nobody has seen it on a screen.

**Patch bytes** (28, each 0–127): `version(2) engine(0–4)`, then 25 parameters, then `checksum` = the sum of the
first 27 bytes `& 0x7F`. The parameters, in order: `base_freq pitch_env_amt pitch_env_time a b noise_mix filter_cutoff
filter_res decay bitcrush attack sustain release filter_env filter_env_time lfo_rate lfo_pitch lfo_filter lfo_timbre
spread chorus delay reverb level table`. `pitch_env_amt` and `filter_env`: 64 = none. `sustain` 0 = a one-shot.
`level` 100 = as designed. `a` / `b` are the engine's own two controls (FM ratio / depth, arpeggio step / speed, grain
rate / amount, wavetable position / warp). Engines: 0 IMPACT, 1 UI BLIP, 2 SCI-FI FM, 3 FOLEY, 4 WAVETABLE.

### The identity, and what is reserved for later

SFX FORGE 1.x runs on one hardware, and from firmware 1.6.0 on both sides hold the same things. A firmware up to 1.5.0
already holds less (below), and it will go further: the Unity side can grow
past what a device holds, and firmwares for other hardware will hold less or other things. So that neither breaks a
project made today, a game can ask a device what it is before it sends anything, and every format says which
version wrote it. Nothing about the sound or the messages above changes.

**Identity bytes** (18, each 0–127), the answer to `07`:

| Byte | | Today |
|---|---|---|
| 0 | hardware: 0 unknown, 1 VIBE FM (the SDK's host – emulator, plugin, VIBE Box; the firmware cannot tell them apart) | 1 |
| 1 2 3 | firmware version: major, minor, patch | 1 7 1 |
| 4 | profile: 1 = what SFX FORGE 1.x offers on VIBE FM | 1 |
| 5 | patch format (`SFX_PATCH_VERSION`) | 2 |
| 6 | song format (`SFX_SONG_VERSION`) | 6 (firmware 1.4 and 1.5: 5) |
| 7 8 | sounds in a kit, kits | 16, 2 |
| 9 10 | patterns, the most steps a pattern holds | 8, 64 (firmware 1.4 and 1.5: 16) |
| 11 12 | automation lanes, bars in an arrangement | 24, 32 |
| 13 14 15 | voices, engines, wavetables | 16, 5, 6 |
| 16 | values in a patch (`SFX_P_COUNT`) | 25 |
| 17 | flags: bit 0 has pads, bit 1 has encoders for the game (LINK page), bit 2 takes a song's sounds (`02` with slot 16–31), bit 3 takes telemetry (CC 10–17, ch 2) | 15 |

The whole answer today, 24 bytes: `F0 7D 56 42 08 01 01 07 01 01 02 06 10 02 08 40 18 20 10 05 06 19 0F F7`.

- **Fields are only ever added at the end.** A reader takes the first 18 bytes and ignores what follows. A message
  with fewer than 18, or with a byte above 127, is not an identity.
- **No answer has a meaning.** A firmware before 1.4.0 ignores `07` and never answers: that is a VIBE FM with a
  firmware before 1.4 and profile 1 as it was then: song format 5, 16 steps a pattern.
- **A profile is a description of limits** – the numbers above. The profile's number is still 1; what changed with
  firmware 1.6.0 is in bytes 6 and 10. So not every song fits every device any more: a song with a pattern of 32 or
  64 steps, and any song in format 6, is not for a firmware up to 1.5.0. The Unity side asks before it sends, and
  says in words why it does not (`SfxLink.Refusal`): to a device that said song format 5, or said nothing, it sends
  a format-5 song only, in the message of before.
- **Requests and answers may come twice.** The emulator hands a device's own output back to it and delivers every
  message twice. A device ignores an identity that comes in, and answering a request twice is harmless. The host
  takes one SysEx a block, so a second answer, or one asked for while a song goes out, follows a moment later.
  A pad's dump that is asked for (`04`, or EXPORT on the device) waits in the same way: asked for right after the
  identity it follows it, and asked for while the sounds and the song go out it comes after them.
- **Patches and songs carry their format** in their first byte (2 and 6 today). A higher number was written by a
  newer SFX FORGE: it is newer, not broken. This firmware does not take it and keeps what it has. A newer patch
  of 28 bytes shows BAD PATCH, and one of another length is ignored without a word. A newer song shows
  `NEWER SONG: UPDATE` and nothing of it is kept. A song of a format the firmware knows with a value out of range
  shows `BAD SONG`. The Unity side does not take it either and says that it is newer: the link raises
  `NewerReceived` for a patch or a song that arrives in a higher format, the SFX Forge window writes it to the
  Console, a song asset that holds one says A NEWER SONG, and the music player warns instead of staying silent
  without a word. A game that got an identity knows the formats a device takes (bytes 5 and 6) before it sends.
- **What the device stores** (`kits.bin`, `song6.bin`, `meter.bin`) has no format number of its own: the kits are 32
  patches, each with its format byte and checksum, and the song is a packed song with its format byte. A stored
  patch of another format keeps its pad's factory sound, and the next SAVE writes this firmware's format over it.
  `meter.bin` (firmware 1.7.0) is one byte, the beat in sixteenths: 2, 3 or 6. It is there only while the beat is
  not a quarter: SAVE with a quarter removes it, and any other byte in it leaves the quarter. It is the device's
  and never crosses the link.
- **Two song files.** From firmware 1.6.0 on the song is saved to `song6.bin`: the format-6 song and two check bytes of the
  `song.bin` beside it. `song.bin`, the file of the firmwares up to 1.5.0, is read and never written, so an older
  firmware put back on the device still finds its song. If that older firmware then saves, its `song.bin` is the
  later one: the next start of a firmware from 1.6.0 on loads it, says `OLDER FIRMWARE'S SONG` on the top line, and F2 on SEQ, SONG or
  AUTO brings the `song6.bin` song back. SAVE says `SAVED`, or `NOT SAVED: STORE FULL` when a write failed. The
  factory reset deletes `song.bin` and saves.

`tests/check-identity.mjs`, `check-wire.mjs` and `check-store.mjs` (run by `build.sh`) send these messages into the
built firmware, read what it sends and look at what it stores; `check-meter.mjs` looks at `meter.bin`. A firmware up to 1.5.0 was not run against any of
this: what is said of it is read from its code.
