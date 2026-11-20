# SFX FORGE – the link, byte by byte

What travels between the device and a game engine. The Unity package implements it in
`Runtime/Core/SfxProtocol.cs` and `Runtime/Core/SfxLink.cs`; [section 6 of the Unity manual](UNITY.md#6-from-the-device-to-the-game)
says how to use it and what of it is tested. The C functions named here (`sfx_song_pack`, `sfx_player_note_on`)
are the engine's, in the source repository.

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
| game → device | CC 16–17, ch 2 | shown on the LINK page, not mapped |
| game → device | `F0 7D 56 42 02 <slot> <28 patch bytes> F7` | load a patch into a pad. Slot 16–31 (firmware 1.3.1): pad 1–16 of the MUSIC kit whichever kit is shown – how a song's sounds are sent |
| game → device | `F0 7D 56 42 03 <ASCII, ≤ 24> F7` | text for the top line |
| game → device | `F0 7D 56 42 04 <slot> F7` | request a dump of a pad |
| both ways | `F0 7D 56 42 05 <piece> <pieces> <≤ 200 bytes> F7` | the song with its mix, in 39 pieces (7699 bytes, `sfx_song_pack`). Only this format travels; the older, shorter songs are read from files. A receiver puts each piece where it belongs and takes the song when all 39 are there: in any order, twins ignored. It forgets a transfer that stops for two seconds; a *different* song sent sooner than that after one that broke off can be mixed with it |
| game → device | `F0 7D 56 42 06 F7` | request the 16 sounds and the song (as SEND does) |

`<slot>` is 0–15, a pad of the kit the device shows. SEND and the request `06` always give the MUSIC kit: the song's
sounds. A game plays sound effects beside the song with slots 16–31 of the player (`sfx_player_note_on`). Telemetry also arrives as the DAW parameters Mod Pitch / Cutoff / FM / Level and Intensity.

**Patch bytes** (28, each 0–127): `version(2) engine(0–4)`, then 25 parameters, then `checksum` = the sum of the
first 27 bytes `& 0x7F`. The parameters, in order: `base_freq pitch_env_amt pitch_env_time a b noise_mix filter_cutoff
filter_res decay bitcrush attack sustain release filter_env filter_env_time lfo_rate lfo_pitch lfo_filter lfo_timbre
spread chorus delay reverb level table`. `pitch_env_amt` and `filter_env`: 64 = none. `sustain` 0 = a one-shot.
`level` 100 = as designed. `a` / `b` are the engine's own two controls (FM ratio / depth, arpeggio step / speed, grain
rate / amount, wavetable position / warp). Engines: 0 IMPACT, 1 UI BLIP, 2 SCI-FI FM, 3 FOLEY, 4 WAVETABLE.
