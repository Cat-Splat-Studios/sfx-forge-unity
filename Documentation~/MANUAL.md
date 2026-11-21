# SFX FORGE – the manual

**A synthesizer for game audio.** Design sound effects and instruments, write music with them, mix it – and the game
plays the result with the same engine, so what you hear here is what the player hears.

Everything is synthesized. There are no samples and no recordings in it.

> **In the Unity package.** This is the manual of the instrument, as it is in the repository the package is
> generated from. The built instrument (firmware 1.5.0) is in the package's `Firmware~` folder:
> `sfx-forge.zip`, with `sfx-forge.wasm` inside it. The game's side is [the Unity manual](UNITY.md). The
> instrument's and the engine's C source (`sfx-forge/`, `core/`) and the scripts under `tests/` and `docs/` are
> in that repository, not in the package; here the engine is the compiled `Runtime/Plugins/x86_64/sfxforge.dll`.

This manual describes firmware 1.5.0.

| I want to … | Go to |
|---|---|
| try it right now | [1. Five minutes](#1-five-minutes) |
| know what each control is | [2. The device](#2-the-device) · [3. The display](#3-the-display) |
| make a sound effect | [5. Tutorial: a sound effect](#5-tutorial-a-sound-effect) |
| understand the knobs | [6. How a sound is built](#6-how-a-sound-is-built) · [7. The five engines](#7-the-five-engines) |
| keep a pattern playing and play into it | [Loop a pattern and play into it](#loop-a-pattern-and-play-into-it) |
| write a beat and a tune | [9. Tutorial: a beat](#9-tutorial-a-beat) |
| take something back | [UNDO](#undo) |
| make music that follows the game | [11. Layers](#11-layers-music-that-follows-the-game) |
| move a knob by itself | [12. Automation](#12-automation) |
| mix | [13. The mixer](#13-the-mixer-and-the-master-effects) |
| work with the mouse | [14. The stage](#14-the-stage) |
| get it into my game | [15. Into the game](#15-into-the-game) |
| look something up | [19. Every control](#19-every-control-page-by-page) |
| see everything on one picture | [the cheat sheet](#the-cheat-sheet) |

---

## 1. Five minutes

| | Do this | What happens |
|---|---|---|
| **1** | Drop `sfx-forge.wasm` onto the [Playground](https://play.thefirmwarehub.com/) (or `sfx-forge.zip` onto the VIBE FM plugin) | SFX FORGE starts |
| **2** | Press **MENU** | the tour: it plays and names what it can do, about 40 seconds. Any key ends it |
| **3** | Hit the **pads** | 16 sound effects. The display shows the one you hit |
| **4** | Press **F2 MUTATE** a few times | each press is a new variation. **F3 UNDO** takes one back |
| **5** | Press **PLAY** | the demo song, bar after bar |
| **6** | Press **K5** twice, turn **encoder 3** down and up again | INTENSITY: it starts at 100 %. Down, the band thins out to the kick; up, it comes in instrument by instrument |
| **7** | Press **STOP**, then **K5** until the page is SEQUENCER, then **PLAY** | one pattern, round and round: the top line says LOOP. **REC**, and what you play on the pads is recorded into it ([section 9](#loop-a-pattern-and-play-into-it)) |
| **8** | Press **HOME** on any page | what every control does on that page |

---

## 2. The device

![The device's controls](img/device.png)

| | Control | What it does |
|---|---|---|
| **1** | the display | the selected sound, the page you are on, the four knobs' values, six soft keys |
| **2** | **F1 – F6** | the six soft keys shown along the bottom of the display. They change with the page |
| **3** | **encoders 1 – 4** | the four values the display shows. Hold SHIFT to turn fast |
| **4** | **DATA** | selects a sound |
| **5** | **the 16 pads** | play the kit's sounds. On the song pages they are steps, patterns or mutes – and the display draws them as 16 tiles |
| **6** | **SHIFT** | the second meaning of a key, and fast encoders |
| **7** | **K1 – K6** | the pages. Press a K key again for its next page |
| **8** | **A · B** | A: the output volume (on SEQ, SONG and AUTO, the tempo). B: the scale on SEQ, the bar's length on SONG. On LINK both send a controller to the game |
| **9** | **LEFT · MENU · RIGHT** | the page before and after – on SEQ, SONG and AUTO the pattern · the tour |
| **10** | **REC · STOP · PLAY** | PLAY plays one pattern round and round (LOOP) or the song in its order (SONG): the top line of the song's pages says which. With REC lit, what you play is recorded |
| **11** | **the keys** | play the selected sound as an instrument, in tune |
| **12** | **RETURN · HOME** | back to the first page · the controls of this page |

### The pages

Six K keys, thirteen pages. Pressing a K key again walks to its next page.

```mermaid
flowchart LR
  K1["K1 SOUND"] --> OSC --> TONE --> FILTER --> WAVE["WAVE<br/>(wavetable only)"]
  K2["K2 ENV"] --> ENV
  K3["K3 LFO"] --> LFO
  K4["K4 FX"] --> FX["FX<br/>a sound's sends"] --> MIX["MIX<br/>faders"] --> MASTER["MASTER<br/>delay, reverb"]
  K5["K5 SEQ"] --> SEQ["SEQ<br/>steps"] --> SONG["SONG<br/>patterns, layers"] --> AUTO["AUTO<br/>automation"]
  K6["K6 LINK"] --> LINK["LINK<br/>the game"]
```

The top four rows shape **one sound**. The K4 and K5 rows after their first page are about **the song**.

---

## 3. The display

**A page is one of two kinds, and nothing else.**

| Kind | Pages | What you see |
|---|---|---|
| **A picture** | the seven sound pages, MASTER | one picture of what the knobs shape, with their four values in its four corners |
| **The pads** | SEQ, SONG, AUTO, MIX | the 16 pads drawn as 16 tiles, laid out as the pads are – with the four values in a column beside them |

### A picture page

![A sound page](img/page-sound.png)

| | What it is |
|---|---|
| **1** | the selected sound: its pad number, its name, its engine |
| **2** | the kit it belongs to: SFX or MUSIC. On SEQ, SONG, AUTO, MIX and MASTER this place says **LOOP** or **SONG** instead: what PLAY plays |
| **3** | the song: playing or stopped – or a red dot while REC is lit, playing or not – the pattern that plays, the tempo – `A>C` while pattern C waits for the bar line – and a dot that is green while a game is linked |
| **4** | the page's name |
| **5** | six marks for K1 – K6. The bright one is the key you are on; the dots under it are that key's pages |
| **6** | the picture: the sound's whole shape – and over it, in white, what this page's knobs shape (the wave, the envelope, the filter, the LFO) |
| **7** | a value in each corner: its name, its value in its unit, and a ring that stands where the knob stands. The number in the ring is the encoder: 1 top left, 2 top right, 3 bottom left, 4 bottom right |
| **8** | the soft keys = F1 – F6 |

**Turn a knob and its value is written large across the middle for a moment** – so you never have to look for it:

![A knob being turned](img/touch.png)

The picture redraws as you turn. That is the quickest way to learn what a knob does.

### A pad page

![The SEQ page](img/page-seq.png)

The tiles are the pads. Whatever a pad does on this page, its tile shows – here, the 16 steps of the selected sound.
The four values are in the column on the right, numbered like the encoders. Under them a line says what the pads do
here, and on SEQ and AUTO the eight patterns stand under that. The numbers in this picture are explained in
[section 9](#reading-the-seq-page).

---

## 4. Two kits

SFX FORGE holds two sets of 16 sounds.

| | **SFX kit** | **MUSIC kit** |
|---|---|---|
| What is in it | the game's sound effects | the band the song plays |
| At the start | explosion, punch, coin, laser, footstep, wind … | kick, snare, hi-hat, FM bass, FM keys, brass, strings … |
| Where it goes | straight to the output, beside the song | through the mixer: fader, pan, mute |
| F1 PRESET offers | 16 sound effects | 23 instruments |

**SHIFT + K1** changes kit. The top line says which one you are in. Going to a song page chooses MUSIC by itself.

Nothing stops you from sequencing a laser or playing a footstep on the keys. The kits only keep the two jobs apart.

---

## 5. Tutorial: a sound effect

**1. Hit a pad** in the SFX kit. Press **F1 PRESET** until you have something close – here, LASER.

![A laser](img/t-sfx-1.png)

**2. Press F2 MUTATE.** Every value moves by up to 15 %. The picture changes; so does the sound. Press again for
another; **F3 UNDO** takes one back.

![The laser, mutated](img/t-sfx-2.png)

**3. Shape it by hand.** Press **K1** until FILTER is lit, and turn **encoder 1**, CUTOFF. The filter's curve moves in
white over the picture, and the sound's shape behind it gets duller.

![The filter page](img/t-sfx-3.png)

**4. Give it a tail.** Press **K2** for the envelope and turn **encoder 2**, DECAY.

![The envelope page](img/t-sfx-4.png)

**5. Keep it.** **F6 SAVE** stores both kits and the song. **F5 EXPORT** sends this sound to the game
([section 15](#15-into-the-game)).

> **Lost?** **F4 RANDOM** is a completely new sound. **SHIFT + MENU, twice** puts everything back as it came from the
> factory – both kits and the song. What you saved is gone then.

---

## 6. How a sound is built

Every sound follows the same path. Each box is one page of four knobs.

```mermaid
flowchart LR
  E["ENGINE<br/>OSC and TONE pages<br/>the raw sound"] --> F["FILTER<br/>FILTER page<br/>takes highs away"] --> A["ENVELOPE<br/>ENV page<br/>start, hold, end"] --> L["LEVEL<br/>FX page"]
  LFO["LFO<br/>a slow wave"] -.-> E
  LFO -.-> F
  L --> OUT["to the output"]
  L --> S["CHORUS · DELAY · REVERB<br/>sends on the FX page"] --> OUT
```

### Every value

| Page | Value | What it does | Range |
|---|---|---|---|
| **OSC** | ENGINE | which of the five engines makes the sound | |
| | FREQ | the pitch (the keys override it) | 20 Hz – 14.5 kHz |
| | PITCH ENV | how far the pitch starts above (+) or below (−) and falls back | ±4 octaves; 0 = none |
| | ENV TIME | how long that takes | 5 ms – 2.6 s |
| **TONE** | A · B | the engine's own two controls ([section 7](#7-the-five-engines)) | |
| | NOISE | how much of it is noise | 0 – 100 % |
| | CRUSH | fewer bits and samples: grit | OFF – 100 % |
| **FILTER** | CUTOFF | where the filter closes | 40 Hz – 15.5 kHz |
| | RESO | a peak at the cutoff | 0 – 100 % |
| | FILTER ENV | how far the cutoff starts above or below and returns | ±5 octaves; 0 = none |
| | F.ENV TIME | how long that takes | 5 ms – 2.6 s |
| **WAVE** | TABLE | which wavetable | six |
| | SPREAD | two detuned copies, for width | up to 30 cents |
| **ENV** | ATTACK | time to reach full level | 1.5 ms – 4 s |
| | DECAY | how fast it falls after that | 10 ms – 3.9 s |
| | SUSTAIN | the level it holds at | OFF, or up to 100 % |
| | RELEASE | how long it rings after the pad is let go | 5 ms – 3.9 s |
| **LFO** | LFO RATE | the slow wave's speed | 0.1 – 29 Hz |
| | > PITCH | vibrato | up to an octave |
| | > FILTER | wah | up to 3 octaves |
| | > TIMBRE | moves FM depth or wavetable position | |
| **FX** | CHORUS · DELAY · REVERB | how much goes to each effect | 0 – 100 % |
| | LEVEL | the sound's own loudness | 100 = as designed |

### One-shot or held: SUSTAIN decides

| SUSTAIN | The sound is | Use it for |
|---|---|---|
| **OFF** | a **one-shot**: it plays to its end however short you hit the pad | drums, coins, explosions |
| **above OFF** | **held**: it sounds while the pad or key is down, then rings out over RELEASE | strings, organs, an engine hum |

The ENV page draws which one you have:

![The ENV page](img/env.png)

The LFO page draws the slow wave, and the FX page shows the three sends and the level:

| LFO | FX |
|---|---|
| ![The LFO page](img/lfo.png) | ![The FX page](img/fx.png) |

---

## 7. The five engines

ENGINE is the first knob of the OSC page. The two knobs **A** and **B** on the TONE page change meaning with it.

| Engine | What it is | A | B | Good for |
|---|---|---|---|---|
| **IMPACT** | a sine whose pitch falls, with FM and noise, driven hard | FM RATIO | FM DEPTH | hits, kicks, explosions |
| **UI BLIP** | a square wave that can step upwards | ARP STEP | ARP SPEED | coins, menus, chiptune |
| **SCI-FI FM** | two sines, one bending the other | FM RATIO | FM DEPTH | lasers, engines, FM bass and keys |
| **FOLEY** | noise through a band, in grains | GRAIN RATE | GRAIN AMT | steps, wind, fire, hi-hats |
| **WAVETABLE** | a wave that morphs through a table | POSITION | WARP | pads, strings, brass, organs |

**A tip for each:**

| Engine | Try this |
|---|---|
| IMPACT | PITCH ENV high with a short ENV TIME is a kick. A small fall over a long time is a tom. NOISE adds the crack |
| UI BLIP | ARP STEP 7 (a fifth) at a high ARP SPEED is the classic coin. At 0 it is a plain square wave |
| SCI-FI FM | FM RATIO around 42 is round (a bass, an electric piano); higher is bells and metal. Its brightness falls over ENV TIME – that is the pluck |
| FOLEY | CUTOFF is called BAND: it is where the noise sits. PITCH ENV sweeps the band – a whoosh. GRAIN AMT chops it: crackle, gravel, rain |
| WAVETABLE | Turn POSITION and watch the wave morph. `> TIMBRE` on the LFO page moves it by itself. SPREAD makes it wide |

A wavetable sound has a fourth SOUND page, WAVE, with its table – SAW-SQ, PULSE, PEAK, VOWEL, ORGAN or GLASS:

![The WAVE page](img/music-wave.png)

---

## 8. Instruments

**Any sound is an instrument.** The keys play the selected sound in tune (A4 = 440 Hz), up to 16 notes at once.

The MUSIC kit starts with a band on its pads:

| | | | |
|---|---|---|---|
| **1** TIMPANI | **2** SNARE | **3** KICK | **4** COWBELL |
| **5** PIZZICATO | **6** BRASS | **7** FM BASS | **8** FM KEYS |
| **9** HARP | **10** CELESTA | **11** ORGAN | **12** WT SWEEP |
| **13** HI-HAT | **14** BELLS | **15** CHOIR | **16** STRINGS |

F1 PRESET in the MUSIC kit also offers OPEN HAT, FLUTE, WT VOWEL, WT BASS, PAD, SQ BASS and SQ LEAD.

These are synthesizer instruments in the style of their names – STRINGS is a slow, detuned wavetable with chorus and
reverb, not a recording of a string section.

---

## 9. Tutorial: a beat

A **pattern** is one bar: 16 steps for each of the MUSIC kit's 16 sounds. There are eight of them, A – H. The SEQ
page (**K5**) is where a pattern is made.

### Loop a pattern and play into it

**On the SEQ page, PLAY keeps one pattern playing, round and round.** The top line says so: **LOOP**. That is all
there is to it – no chain to set, no arrangement to clear. (From firmware 1.5.0.)

**1. Press K5** while nothing plays. The page is SEQUENCER, and the top line says LOOP. (If the song is playing,
press STOP first – or press F1.) DATA chooses the sound whose steps you see. A device as it comes shows pad 1,
TIMPANI, with no steps; turn DATA two clicks to the right for the kick, as in the picture.

![The SEQ page, stopped: LOOP in the top line](img/loop-1.png)

| | What it is |
|---|---|
| **1** | **LOOP**: PLAY will repeat the pattern. (The other word here is SONG: PLAY will play the song in its order) |
| **2** | **F1** changes between the two. It says what it will do: TO SONG |
| **3** | the eight patterns. The blue one is the one you see |

**2. Press PLAY.** Pattern A repeats until you press STOP. Nothing moves on to B.

**3. Another pattern: LEFT or RIGHT.** It does not cut in. It starts on the next bar line, and then it is the one
that repeats. While it waits, the display says so in three places:

![A pattern that waits for the bar line](img/loop-2.png)

| | What it is |
|---|---|
| **1** | `A>G`: A plays, G comes next |
| **2** | `EDIT G - PLAYS A`, in orange: what you see is not yet what you hear |
| **3** | G blinks yellow until it starts. White is the pattern that plays |

Here G is the first empty pattern of the demo song: a place to try things.

**4. Press REC.** Now the pads are the drum kit and the keys are the notes. Play in time: every hit is written onto
the step nearest to it, and you hear it again on the next round. A click keeps time.

![Playing it in](img/loop-3.png)

| | What it is |
|---|---|
| **1** | the red dot: REC is lit. It beats with the click |
| **2** | the hits, as they land. The tiles show the sound you played last |
| **3** | what the pads do now |
| **4** | **F4** is **ERASE** while REC plays |

**5. A wrong note?** Hold **F4 ERASE**: the selected sound's steps are erased as the bar passes them; a tap erases
only the step under the playhead. Or press **F2 UNDO**: everything you played since the first hit is gone in one
press. F2 again brings it back.

![UNDO takes the whole take back](img/loop-4.png)

| | What it is |
|---|---|
| **1** | what was taken back: `TAKE`, all you played in |
| **2** | **F2 UNDO**. Pressed again it is redo |

**6. Press REC again** and the pads are the 16 steps once more, to add a hit by hand or fix a note. **STOP** ends it.

**7. The song in its order again?** Press **F1 TO SONG** – or go to the SONG page while it is stopped and REC is
off ([section 10](#what-play-plays-loop-or-song)).

### Step by step: a beat from nothing

Press **K5** for the SEQ page, and **RIGHT** until you are on an empty pattern – in the row of eight letters under
the values an empty one is dim. In the demo song that is G. While the song is stopped, LEFT and RIGHT change the
pattern at once.

**1. The kick.** Turn **DATA** to KICK. Press **F3 FILL** once: a kick on every 4th step.

![A kick on every beat](img/t-beat-1.png)

**2. The snare.** DATA to SNARE. Hit **pads 5 and 13**: the pads are the 16 steps.

![Kick and snare](img/t-beat-2.png)

**3. The hi-hat.** DATA to HI-HAT. Press **F3 FILL** twice: every 2nd step.

![Kick, snare and hat](img/t-beat-3.png)

**4. A bass line.** DATA to FM BASS. **Hold a step's pad and play a key**: that step plays that note.

![With a bass line](img/t-beat-4.png)

**5. Press PLAY.** The pattern repeats: the top line says LOOP. Go on changing it while it plays.

![The pattern, playing](img/t-beat-5.png)

Anything you do here **F2 UNDO** takes back ([below](#undo)).

### Reading the SEQ page

![The SEQ page](img/page-seq.png)

| | What it is |
|---|---|
| **1** | what PLAY plays: **LOOP** (this pattern, round and round) or **SONG** (the song in its order) |
| **2** | playing (a green triangle) or stopped (a square) – or, while REC is lit, a red dot in their place, playing or not · the pattern that plays · the tempo. `A>C` while C waits for the bar line |
| **3** | the page and the pattern you see: `PATTERN A` – or `EDIT B - PLAYS A`, in orange, while another one plays. `layer off` beside it: the selected sound is silent at the current intensity |
| **4** | the 16 steps of the selected sound, as the 16 pads: **a row is a beat**. A step that is on shows its **note**; its **height** is how hard it plays; `>3` in its corner means it holds 3 steps. The frame is the step the encoders edit; the white tile is the step playing now |
| **5** | STEP · NOTE · VELOCITY · LENGTH of the framed step |
| **6** | what the pads do: `PADS: THE 16 STEPS`. With REC lit: `REC READY: PRESS PLAY`, then `REC: PLAY IT IN - F4 ERASE`. `ITS LAYER IS OFF NOW` warns that you will not hear the selected sound |
| **7** | the eight patterns, and the scale NOTE moves in (with REC lit: `REC off: steps`) |
| **8** | TO SONG (or TO LOOP) · UNDO · FILL · CLEAR the sound's steps · COPY the pattern · SAVE |

The eight letters, on SEQ and on AUTO:

| A letter is | It means |
|---|---|
| **yellow, blinking** | chosen: it starts on the next bar line |
| **white** | it plays |
| **blue** | it is the one you see and edit |
| **light on dark** | something is in it |
| **dim** | empty |

The page shows one sound at a time. To see every sound's steps at once, open the stage ([section 14](#14-the-stage)).

### Everything a step can do

| To do this | Do this |
|---|---|
| switch a step on or off | hit its pad (REC not lit) |
| choose another sound | turn DATA, or SHIFT + its pad (you hear it) |
| give a step a note | hold its pad and play a key – or turn encoder 2 NOTE (it stays in the scale, which B chooses) |
| make it louder or softer | encoder 3 VELOCITY, 1 – 8 |
| make a held note shorter | encoder 4 LENGTH: AUTO, or 1 – 15 steps |
| the sound on every 4th step, every 2nd, every one, none | F3 FILL, again and again |
| take the selected sound out of the pattern | F4 CLEAR. `NOTHING TO CLEAR` if it has no steps |
| try a variation | F5 COPY: the pattern is copied into the next empty one, and you are on the copy (`COPIED A > G`). While it plays the copy starts on the bar line. `NO EMPTY PATTERN` if all eight hold something |
| play a part in, live | **REC, then PLAY** – see below |
| change the tempo | A. SHIFT + A is the swing (the top line says it: `SWING 20%`) |

A step **without a note** plays the sound as designed – right for drums. **LENGTH AUTO** holds a note until the sound
plays again or the bar ends; LENGTH is for held sounds, a one-shot always plays to its own end.

### Playing it in

Press **REC**, then **PLAY** – or PLAY first and REC when you are ready. On the SEQ page, while REC is lit:

| | |
|---|---|
| **the pads are the drum kit** | hit a pad in time and its sound is recorded on the step nearest to your hit, as hard as you hit it. Kick, snare and hat can be played in together |
| **the keys are the notes** | SHIFT + a pad selects an instrument (without recording it); then every key you play is recorded on its step, at its pitch – a bass line or a melody in one pass |
| **the tiles follow you** | they show the steps of the sound you played last, so you see each hit land |
| **the pattern repeats** | switching REC on sets LOOP (`REC ON: PATTERN LOOPS` if it was SONG). What you play is laid on top of what is there, round after round |
| **the display stays with the sound** | LEFT and RIGHT still choose another pattern, on the bar line. Until then the display stays on the pattern that plays, so every hit lands in what you see |
| **a metronome keeps time** | a click on every beat, higher on the bar line, while REC is lit and it plays. The red dot in the top line beats with it. **SHIFT + REC** turns the click off and on (`CLICK OFF`, `CLICK ON`). It is the device's own: it is not recorded and a game never hears it. There is no count-in: the first round of the loop is one |
| **F4 is ERASE** | held, it erases the selected sound's steps as the bar passes them, in the pattern that plays. A tap erases the step under the playhead |
| **F2 takes the take back** | all of it, from the first hit to STOP, PLAY or REC – with whatever you erased, filled or turned in between |

While it is stopped with REC lit the pads only sound; nothing is recorded (`REC READY: PRESS PLAY`).

Press **REC** again and the pads are the sixteen steps once more.

Two things to know. Only the **MUSIC kit** is recorded: with the SFX kit selected (on a sound page) a hit says
`MUSIC KIT ONLY: K5 SEQ` and nothing is written. And if you press **F1 TO SONG** while REC plays, the take goes along
the song: each hit lands in the pattern that plays in that bar.

### UNDO

**F2** on SEQ, SONG and AUTO takes back the last thing you did to the song. Pressed again, it brings it back. The
top line says what: `UNDO: TAKE`, `REDO: TAKE`. With nothing to take back the key is dim and says `NOTHING TO UNDO`.

| It says | One step is |
|---|---|
| `EDITS` | steps, notes, velocities, lengths and automation points changed in a row. The row ends when you change the page or the pattern, or press PLAY, STOP or REC |
| `TAKE` | everything played in, from the first hit to STOP, PLAY or REC |
| `BARS` | bars added to the arrangement and taken away in a row |
| `FILL` · `CLEAR` · `RAMP` · `CLR PAT` · `COPY` · `ARR` | that one key press (`ARR`: the arrangement cleared). After COPY, F2 also takes you back to the pattern you copied |
| `NEW` · `DEMO` · `LOAD` | the whole song that was replaced – by NEW, by DEMO, or by a song a game sent |

| | |
|---|---|
| **One step only** | the next thing you do replaces what F2 holds |
| **The notes** | F2 brings back patterns, automation and the arrangement. After NEW, DEMO and LOAD it brings back the whole song, with tempo and mix |
| **Not the knobs** | tempo, swing, the scale, CHAIN, the bar's length, layers and the mixer are not taken back by F2 |
| **Not the sounds** | a sound has its own UNDO, F3 on the sound pages. After a game's LOAD, F2 brings back the song, not the sounds that came with it |
| **Not kept** | switching off, or a factory reset, forgets it |

---

## 10. The song

Press **K5 again** for the SONG page.

![The SONG page](img/page-song.png)

| | What it is |
|---|---|
| **1** | what PLAY plays: **SONG** here, or LOOP. On this page too the line under it says `EDIT A - PLAYS B` while you look at one pattern and another plays |
| **2** | the eight patterns on pads 1 – 8, each with a sketch of what is in it. White: playing. Blue: the one you edit. Blinking yellow: chosen, it starts on the next bar line |
| **3** | INTENSITY on pads 9 – 16: the tiles light up to the current level |
| **4** | PATTERN · CHAIN · INTENSITY · the selected sound's LAYER |
| **5** | the arrangement: a pattern for each bar. The white one is playing (no bar is white while a pattern loops) |
| **6** | TO LOOP (or TO SONG) · UNDO · CLR PAT · NEW · SEND to the game · SAVE |

### How a song is put together

```mermaid
flowchart TD
  S["THE SONG<br/>tempo, swing, the mix"] --> A["ARRANGEMENT<br/>up to 32 bars: A B A B C D …"]
  A --> P["8 PATTERNS, A – H<br/>one bar each"]
  P --> R["16 ROWS<br/>one for each sound of the MUSIC kit"]
  R --> ST["16 STEPS<br/>each: on or off, a note, a velocity, a length, an automation point"]
```

### What PLAY plays: LOOP or SONG

| The top line says | PLAY plays |
|---|---|
| **LOOP** | the pattern, round and round. A pattern you choose takes over on the next bar line, and then that one repeats |
| **SONG** | the arrangement, bar after bar, from the first bar that has the pattern you see. If no bar has it: that pattern once, then the arrangement from the top. Without an arrangement: the chain |

The word stands in the top line of SEQ, SONG, AUTO, MIX and MASTER. On the sound pages and LINK the kit stands
there instead; PLAY plays what was set last.

| This happens | The mode is |
|---|---|
| you go to **SEQ** or **AUTO**, while it is stopped and REC is not lit | LOOP |
| you go to **SONG**, while it is stopped and REC is not lit | SONG |
| you go to any page while it plays, or with REC lit | as it was |
| **F1** on SEQ, SONG or AUTO – at any time, also while it plays | the other one (`LOOP: PATTERN REPEATS`, `SONG: PLAYS IN ORDER`). The key says what it will do: TO SONG, TO LOOP |
| you switch **REC** on | LOOP |
| you press PLAY or STOP | as it was: the top line said what PLAY will do |
| the device is switched on | SONG: PLAY plays the song |

From LOOP to SONG while it plays, the song goes on behind the first bar of the arrangement that has the pattern
you hear – from the top if no bar has it. Without an arrangement the chain goes on from the pattern you hear.

### Choosing a pattern

| | |
|---|---|
| **with** | LEFT / RIGHT on SEQ, SONG and AUTO · pads 1 – 8 and encoder 1 on SONG · a click on the stage · a cue from a game |
| **stopped** | it is there at once |
| **playing** | it starts on the next bar line. Until then its letter blinks yellow and the top line reads `A>C` |
| **STOP before the bar line** | the pattern you chose is the one you see, and the one the next PLAY starts with |

In **LOOP** the chosen pattern repeats. In **SONG** it depends on the arrangement:

| The chosen pattern is … | What happens |
|---|---|
| **in** the arrangement | the song carries on from its next bar with that pattern |
| **not in** it | it takes over on the bar line and stays until you choose another |
| (no arrangement) | the chain goes on from it |

### The order of the song

| You want | Do this |
|---|---|
| your own order | the arrangement: **SHIFT + pad 1 – 8** adds that pattern as a bar (`BAR ADDED`; `32 BARS: FULL` at the end) |
| to take the last bar away | SHIFT + any of pads 9 – 15 |
| no arrangement (the chain plays again) | SHIFT + pad 16, **twice**: it asks `PRESS AGAIN: CLEAR ARR` |
| A, B, C, D in a row, a bar each, without an arrangement | CHAIN (encoder 2) at A – D. While there is an arrangement the chain is not used, and the knob says `ARRANGEMENT IN USE` |
| one pattern, round and round | LOOP – F1 |
| a bar of fewer than 16 steps | encoder B (`BAR: 12 STEPS`). It is the bar's length for the whole song |
| to empty the pattern you see | F3 CLR PAT |
| an empty song | F4 NEW, **twice**: it asks `PRESS AGAIN: NEW SONG`. Tempo 120, no swing, no arrangement, every fader at 100, every layer at 0. Your sounds stay |
| the demo song again | SHIFT + F4, **twice**: `PRESS AGAIN: DEMO` |

A key that asks twice counts the second press while its question is in the top line, about two seconds. All of
these F2 takes back.

---

## 11. Layers: music that follows the game

This is what makes it game music.

> **Every sound has a LAYER mark. The song has one INTENSITY. A sound plays only while INTENSITY is at or above its mark.**

![How intensity brings the band in](img/layers.png)

So one song is many. At 0 % only the kick plays; as the game heats up, hat, bass, snare, strings, keys, brass and
bells join in that order.

| | On the SONG page |
|---|---|
| set INTENSITY | encoder 3, or pads 9 – 16 (eight steps). A game sends it |
| set a sound's LAYER mark | select the sound (DATA), turn encoder 4 |

On the page the eight intensity tiles light up as the level rises; LAYER is the selected sound's own mark.

---

## 12. Automation

Automation moves a knob by itself while the pattern plays. **Any knob can have a lane** – and a sound can have as many
lanes as it has knobs that should move. Press **K5 a third time** for the AUTO page. As on SEQ, PLAY repeats the
pattern here (LOOP) – so a lane can be drawn while it goes round.

![The AUTO page](img/page-auto.png)

| | What it is |
|---|---|
| **1** | the pattern, and the knob this lane moves |
| **2** | the lane, as the 16 pads: a bar for each step. Bright with a white edge: a point. Dim: the value still held from the last point. The dot in a corner: the sound plays on that step |
| **3** | STEP · VALUE (in the knob's own unit) · TARGET (which knob). The orange dot says this knob has a lane |
| **4** | the eight patterns, as on SEQ, and how many of the song's 24 lanes are in use |
| **5** | TO SONG (or TO LOOP) · UNDO · RAMP joins the points with straight lines · CLEAR empties the lane · COPY the pattern · SAVE |

### What a lane can move

| | Targets |
|---|---|
| **the sound's own** | any of its 25 values: CUTOFF, FM DEPTH, POSITION, a send … |
| **its place in the mix** | VOLUME, PAN |
| **the master** | DELAY TIME, FEEDBACK, ROOM SIZE, DAMPING, DRIVE |

### The quick way: turn the knob, then press K5

**AUTO opens on the knob you turned last.** So: on any page of the MUSIC kit turn the knob you want to move – CUTOFF
on the FILTER page, a fader on MIX, ROOM SIZE on MASTER – then go to AUTO. Its lane is ready.

| | Do this |
|---|---|
| **1** | turn the knob you want to automate, on its own page |
| **2** | press **K5** until AUTO. TARGET already names that knob (or turn **encoder 3** to choose another) |
| **3** | hit **pad 1**. A point appears where the knob stands. Turn **encoder 2** down |
| **4** | hit **pad 16**. Turn encoder 2 up |
| **5** | press **F3 RAMP** – the picture above |

For a second lane on the same sound, turn TARGET to another knob and put points on it. Both play.

LEFT and RIGHT choose the pattern, as on SEQ. **F2 UNDO** takes back points, a RAMP or a CLEAR
([section 9](#undo)). CLEAR on a lane without points says `NOTHING TO CLEAR`.

On a sound page, an orange dot beside a value says a lane moves that knob:

![A knob with a lane](img/lanes-sound.png)

### Four rules

| | |
|---|---|
| **A point holds** | until the next point, or the end of the bar |
| **Nothing is written** | at every bar line each knob is back where you set it by hand |
| **It moves in steps** | a 16th note at a time. Use RAMP for a sweep that sounds smooth |
| **24 lanes a song** | the first point of a knob takes a lane; clearing its last point gives it back |

---

## 13. The mixer and the master effects

```mermaid
flowchart LR
  M["MUSIC kit<br/>16 instruments"] --> F["FADER · PAN · MUTE<br/>MIX page"] --> SUM(("sum"))
  X["SFX kit<br/>16 sound effects"] --> SUM
  F --> FX["CHORUS · DELAY · REVERB<br/>MASTER page"]
  X --> FX
  FX --> SUM
  SUM --> D["DRIVE<br/>MIX page"] --> O["OUTPUT"]
```

Sound effects skip the faders: a muted bass never mutes an explosion.

### MIX – press K4 twice

![The MIX page](img/page-mix.png)

| | What it is |
|---|---|
| **1** | every sound of the MUSIC kit as a tile, where its pad is: its name, its fader (the small mark is 100, as designed), its pan under it. **M**: muted. **S**: solo. The frame is the selected sound |
| **2** | VOLUME and PAN of the selected sound · DRIVE: saturation on the whole mix · OUTPUT volume |
| **3** | what the pads do here |
| **4** | MUTE · SOLO · RESET the fader · ALL ON · PLAY · SAVE |

**The pads mute. SHIFT + a pad solos** – only soloed sounds play.

The top line says LOOP or SONG here too, and **F5 PLAY** plays that: the pattern you were on, round and round, or
the song. F1 on SEQ, SONG or AUTO changes it.

### MASTER – press K4 a third time

![The MASTER page](img/page-master.png)

| | What it is |
|---|---|
| **1** | the delay, drawn as its echoes: left and right in turn |
| **2** | the reverb, drawn as its fading |
| **3** | the delay's values, in its corners: DELAY TIME (a 16th to a half note – it follows the tempo) and FEEDBACK |
| **4** | the reverb's: ROOM SIZE and DAMPING (low: a bright hall, high: a dark room) |

Each sound's own FX page decides how much of it goes into these.

**The mix belongs to the song.** It is saved with it and it is the mix in the game.

---

## 14. The stage

In the Playground and the plugin, **Stage** opens a bigger picture of the whole instrument, for the mouse.

![The stage](img/stage.png)

| | What it is | With the mouse |
|---|---|---|
| **1** | LOOP / SONG: what PLAY plays | click: the other one |
| **2** | PLAY / STOP · REC | click |
| **3** | the tempo | click its left or right half, or the wheel |
| **4** | the patterns A – H. White: playing. Blue: the one shown. Blinking yellow: it starts on the next bar line | click: go there – on the bar line while it plays · **right-click: add a bar of it to the arrangement** |
| **5** | the arrangement | click a bar: its pattern · right-click: end the arrangement before it (UNDO brings it back) |
| **6** | the kit's 16 pads | click to play and select |
| **7** | SFX / MUSIC | choose the kit |
| **8** | the selected sound's picture | |
| **9** | the ten tabs | click |
| **10** | the tab's four values | **drag**, or the wheel (SHIFT: bigger steps) |
| **11** | PRESET · MUTATE · UNDO · RANDOM · EXPORT · SAVE | click (right-click PRESET: the one before) |
| **12** | the rows' names | click: hear and select · right-click: mute |
| **13** | the pattern | click a step · **wheel: its note** · SHIFT + wheel: velocity · CTRL + wheel: length |
| **14** | INTENSITY, with each sound's mark above | drag |
| **15** | the selected sound's LAYER mark | drag |
| **16** | what plays now. Messages appear over it: what a key did, or what it asks | |
| **17** | FILL · CLEAR · COPY · UNDO · DEMO | click. DEMO asks twice. UNDO is dim while there is nothing to take back |

SHIFT-click does the same as right-click everywhere.

The picture shows pattern A repeating: LOOP is lit, and no bar of the arrangement moves. ERASE, the click's switch
and the swing are on the device only.

### Automation on the stage

![The stage's AUTO tab](img/stage-auto.png)

| | Do this |
|---|---|
| **1** | click the **AUTO** tab: the 16 rows become the sounds' lanes |
| **2** | choose the **TARGET** (drag the cell, or the wheel). Drag **VALUE** to set one point exactly |
| **3** | **click or drag in the sound's row to draw.** The height is the value; drag across to paint a curve. Right-click removes a point |
| **4** | FILL has become **RAMP** |

---

## 15. Into the game

```mermaid
flowchart LR
  subgraph DEVICE["SFX FORGE"]
    S1["a sound effect"]
    S2["the song, its instruments, its mix"]
  end
  subgraph UNITY["Unity"]
    P1["SfxPreset<br/>asset"] --> PL["SfxPlayer"]
    P2["SfxSong<br/>asset"] --> MP["SfxMusicPlayer"]
  end
  S1 -- "F5 EXPORT" --> P1
  S2 -- "F5 SEND (SONG page)" --> P2
  G["your game"] -- "pitch, cutoff, level" --> PL
  G -- "intensity, cues" --> MP
```

The game runs the same engine as the device (`core/`, plain C, built as a native library), so nothing is converted
and nothing sounds different.

### What the game controls

| The game wants to … | It uses |
|---|---|
| play a sound effect | `SfxPlayer` – live, or baked to an ordinary WAV |
| bend a sound while it plays (an engine that follows speed) | `SfxPlayer`'s pitch, cutoff, timbre and level |
| bring the band in and out | `SfxMusicPlayer.intensity`, 0 – 1 |
| change the music on the next bar | `SfxMusicPlayer.CuePattern(0 … 7)` |
| keep one pattern playing until the next cue | `SfxMusicPlayer.loopPattern` – the device's LOOP |
| play one of the song's instruments by hand | `SfxMusicPlayer.PlaySound(sound, velocity, note)` |
| move the mix | `SetTrackVolume` · `SetTrackPan` · `SetTrackMute` · `SetMaster` |

### Cues and the arrangement

| The cued pattern is … | What happens |
|---|---|
| **in** the arrangement | the song carries on from that bar |
| **not in** it (keep one aside for the boss, the menu) | it takes over on the bar line and stays until the next cue |

That is with the song playing in its order. With `loopPattern` on – or LOOP on the device – every cued pattern
takes over on the bar line and repeats until the next cue, wherever it stands in the arrangement.

`loopPattern` is new with firmware 1.5.0. The package holds the native library with the loop flag, and the
library's own test (`tests/dotnet`) passes with it: a looped pattern stays bar after bar, and a cue changes it on
the bar line. Inside Unity one Play mode test runs it, headless and muted: with `loopPattern` on, the demo song
stays on its pattern for more than three bars; with it off, the arrangement goes on. Nobody has listened to it.

### Tuning live

Linked over MIDI, the game drives the device while you listen and adjust. The **LINK page** (K6) shows it:

![The LINK page](img/page-link.png)

| | What it is |
|---|---|
| **1** | what the game sends: pitch, cutoff, timbre, level, intensity, pattern |
| **2** | encoders 1 – 4 send CC 20 – 23 to the game: tune a game value from here |
| **3** | green while a game is linked, with any text it sent |

From firmware 1.4.0 a game can also ask the device what it is. The device answers with its **identity**: the
hardware, the firmware's version, the formats of its sounds and songs, and what it holds – 16 sounds in each of 2
kits, 8 patterns of 16 steps, 24 automation lanes, 32 bars, 16 voices, 5 engines, 6 wavetables. Today every device
answers the same; the question is there so that later firmwares for other hardware, and a Unity side that can do
more than a device, can be told apart without breaking what is made now. An older firmware does not answer, and the
game takes that for a VIBE FM with a firmware before 1.4. Nothing shows on the device when it is asked, except that
the LINK light comes on as for any message from a game.

The protocol, byte by byte, is in [LINK-PROTOCOL.md](LINK-PROTOCOL.md). The Unity side is this package:
[the Unity manual](UNITY.md) explains it, and its sample, the showcase game, shows all of it in one level.

---

## 16. Saving and resetting

| | |
|---|---|
| **F6 SAVE** | keeps both kits and the song – patterns, arrangement, layers, mix, automation. A DAW project keeps them too |
| **F4 NEW** (SONG page), twice | an empty song. It replaces your song, not your sounds. F2 UNDO brings the song back |
| **SHIFT + F4** (SONG page), twice | the demo song again. It replaces your song, not your sounds. F2 UNDO brings the song back |
| **SHIFT + MENU, twice** | factory reset: both kits and the song as they came, and saved so. Press it once and it asks; twice within three seconds does it |

The game and the device read songs from every earlier version of the format. A kit or song *stored on the device* by
an earlier version is not read; the factory kits and the demo appear instead. The same holds the other way: a
sound or song from a *newer* version is not taken by this firmware (it keeps what it has), and a kit or song stored
by a newer firmware is not read. The next SAVE then writes over it.

Firmware 1.5.0 stores sounds and songs exactly as 1.4.0 did. What it adds is not stored: after switching on, PLAY
plays the song (SONG), the click is on, REC is off and F2 has nothing to take back.

---

## 17. Recipes

| I want … | Do this |
|---|---|
| **a laser in ten seconds** | SFX kit, F1 until LASER, F2 MUTATE until you like it |
| **a footstep that is never the same** | in the game, play FOOTSTEP at a slightly different pitch each time – or EXPORT four mutations and pick one at random |
| **an engine that follows speed** | ENGINE HUM (a held sound) on an `SfxPlayer`, with an `SfxTelemetrySender` feeding speed to pitch and cutoff |
| **a chord that swells** | STRINGS: one step with a note, LENGTH 12. AUTO page: TARGET VOLUME, a low point on step 1, a high one on step 12, RAMP |
| **a filter sweep into the drop** | AUTO page, the bass, TARGET CUTOFF, points on steps 1 and 16, RAMP |
| **echoes on the snare only** | SNARE's FX page: DELAY up. MASTER page: DELAY TIME 3/16, FEEDBACK to taste |
| **music that reacts to danger** | low LAYER marks for the calm instruments, high for the tense ones. The game sets `intensity` from how close the enemy is |
| **a boss theme** | write it in pattern H and leave H out of the arrangement. `CuePattern(7)` when the boss appears, `CuePattern(0)` after |
| **one pattern, round and round, while I work on it** | K5 for the SEQ page, PLAY. The top line says LOOP |
| **to play a part in** | SEQ page, REC, PLAY, then the pads or the keys. A bad take: F2 |
| **one wrong sound out of a take** | SHIFT + its pad selects it. Hold F4 ERASE for one round |
| **a variation of a pattern** | F5 COPY on the SEQ page: the next empty pattern is a copy, and you are on it. F2 takes it back |
| **the whole song again, after looping** | F1 TO SONG, PLAY |
| **to start over** | SHIFT + MENU, twice |

---

## 18. What it does not do

| | |
|---|---|
| **Samples** | it cannot play recorded audio: no voice lines, no recorded foley, no real orchestra |
| **Timing** | a bar is 16 steps. No triplets inside a bar, no notes between steps; swing is the only timing change |
| **Size** | 8 patterns, 32 bars of arrangement, 16 voices – with long reverberant sounds the oldest note is cut to make room |
| **Automation** | 24 lanes a song, moving in steps, not glides |
| **Chords** | one note per step for each sound. A chord needs several sounds, or the keys |
| **Recording** | onto the 16 steps: a hit is moved to the step nearest to it. No count-in. The MUSIC kit only |
| **Undo** | one step back for the song, and forth again. Not for tempo, swing or the mixer; a sound has its own |
| **Platforms** | the Unity package runs on Windows x64 so far. Its MIDI link is tested against the emulator over a virtual cable, not against the hardware |
| **The stage** | has no control for the swing, the click or ERASE; they are on the device |
| **What is tested** | firmware 1.5.0 passes all 14 scripted checks (`sh build.sh`): scripts press its keys and the checks look at what it then plays, sends and holds – the loop, a take over several bars, ERASE, UNDO, the keys that ask twice, COPY. It has not been run on the hardware or in the plugin. The link was last tested against the emulator with firmware 1.4.0. Nobody has listened to the new features yet |

---

## 19. Every control, page by page

### The cheat sheet

For those who know their way around: every page and every shortcut on one picture.

![The cheat sheet](img/cheatsheet.png)

### The same, as tables

**Everywhere:** pads play · keys play the selected sound in tune · DATA selects · SHIFT + K1 the other kit ·
PLAY STOP: a pattern round and round (LOOP) or the song (SONG) · REC: what is played is recorded · SHIFT + REC the
click · MENU the tour · HOME this page's controls · A the output volume (not on SEQ, SONG, AUTO and LINK: see below) · SHIFT + MENU twice: reset.

| Page | Key | Pads | Encoders 1 – 4 | F1 – F6 |
|---|---|---|---|---|
| **OSC** | K1 | play | ENGINE · FREQ · PITCH ENV · ENV TIME | PRESET · MUTATE · UNDO · RANDOM · EXPORT · SAVE |
| **TONE** | K1 ×2 | play | A · B · NOISE · CRUSH | the same |
| **FILTER** | K1 ×3 | play | CUTOFF · RESO · FILTER ENV · F.ENV TIME | the same |
| **WAVE** | K1 ×4 | play | TABLE · SPREAD | the same |
| **ENV** | K2 | play | ATTACK · DECAY · SUSTAIN · RELEASE | the same |
| **LFO** | K3 | play | LFO RATE · > PITCH · > FILTER · > TIMBRE | the same |
| **FX** | K4 | play | CHORUS · DELAY · REVERB · LEVEL | the same |
| **MIX** | K4 ×2 | mute · SHIFT: solo | VOLUME · PAN · DRIVE · OUTPUT | MUTE · SOLO · RESET · ALL ON · PLAY · SAVE |
| **MASTER** | K4 ×3 | play | DELAY TIME · FEEDBACK · ROOM SIZE · DAMPING | as MIX |
| **SEQ** | K5 | steps (REC lit: the kit) · SHIFT: select a sound | STEP · NOTE · VELOCITY · LENGTH | TO SONG / TO LOOP · UNDO · FILL · CLEAR (REC playing: ERASE, held) · COPY · SAVE |
| **SONG** | K5 ×2 | 1 – 8 pattern · 9 – 16 intensity · SHIFT: arrangement | PATTERN · CHAIN · INTENSITY · LAYER | TO SONG / TO LOOP · UNDO · CLR PAT · NEW (SHIFT: DEMO), both twice · SEND · SAVE |
| **AUTO** | K5 ×3 | a point on / off · SHIFT: select a sound | STEP · VALUE · TARGET (it opens on the knob turned last) | TO SONG / TO LOOP · UNDO · RAMP · CLEAR · COPY · SAVE |
| **LINK** | K6 | play | send CC 20 – 23 | as the sound pages |

| Other encoder | Where | What it does |
|---|---|---|
| **A** | SEQ, SONG, AUTO | the tempo. SHIFT + A: the swing |
| **A · B** | LINK | send CC 24 and 25 |
| **B** | SEQ | the scale NOTE moves in |
| **B** | SONG | the number of steps in a bar |

| Other key | Where | What it does |
|---|---|---|
| **LEFT · RIGHT** | SEQ, SONG, AUTO | the pattern before / after: at once while stopped, on the bar line while it plays |
| **REC** | everywhere | lit: PLAY repeats the pattern, and the keys and the pads are recorded into it – the pads on every page where they play the MUSIC kit: SEQ, the sound pages, MASTER and LINK |
| **SHIFT + REC** | everywhere | the click on / off |
| **SHIFT + pad 1 – 8** | SONG | add that pattern to the arrangement as a bar |
| **SHIFT + pad 9 – 15** | SONG | take the last bar away |
| **SHIFT + pad 16**, twice | SONG | no arrangement |

| The top line says | It means |
|---|---|
| `LOOP: PATTERN REPEATS` · `SONG: PLAYS IN ORDER` | F1: what PLAY plays now |
| `REC ON: PATTERN LOOPS` | REC changed SONG to LOOP |
| `ERASE: HOLD F4` | F4 while REC plays: held, it erases the selected sound |
| `MUSIC KIT ONLY: K5 SEQ` | a hit of the SFX kit was not recorded |
| `UNDO: …` · `REDO: …` · `NOTHING TO UNDO` | F2 |
| `PRESS AGAIN: …` | a key that asks twice: press it again while this stands |
| `NOTHING TO CLEAR` | CLEAR or CLR PAT with nothing there. What F2 holds is kept |
| `COPIED A > G` · `NO EMPTY PATTERN` | F5 COPY |
| `ARRANGEMENT IN USE` | CHAIN is not used while there is an arrangement |

The pictures in this manual are made from the firmware itself: `node docs/shots.mjs` takes the screenshots,
`python docs/figures.py` puts the marks on them.
