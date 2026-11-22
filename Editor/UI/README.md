# The kit: SFX FORGE's display, in UI Toolkit

What the inspectors and the window are built from. It mirrors the device's display (`sfx-forge/sfx-forge.c`: the colours
BG, PANEL, LINE, DIM, TEXT, the engines' colours, and its drawing code). Editor only; no fonts, no images, no other package.

Everything sits in **one panel**: `SfxUI.Panel()` – dark, warm, round corners, and it carries the stylesheet
(`SfxForge.uss`). Narrower than 400 px the panel has the class `sfx-narrow` (smaller values, shorter picture).
Texts are `TextElement`s made with `SfxUI.Text(text, class)`, so the editor's theme does not restyle them.

| Class | File | What it is on the device | How to use it |
|---|---|---|---|
| `SfxColors` | SfxKit.cs | the palette, `Engine(e)`, `Mix(a, b, t)` (= `vg_mix`), `Hex(0xRRGGBB)` | for anything drawn with a painter; the USS has the same colours as `--sfx-…` variables |
| `SfxUI` | SfxKit.cs | – | `Panel()`, `Text()`, `Box(class)`, `Menu(under, names, ticked, picked)`, `RoundRect` / `Rect` for a `Painter2D`; `NativeThere`: the device's engine is here (every panel asks this one) |
| `SfxHeader` | SfxKit.cs | the top line: number tile, NAME, ENGINE, the link dot | `Set(number, name, sub, colour)`, `SetLink(on)`, `numberClicked`, `Slot` for more |
| `SfxBanner` | SfxKit.cs | – (the device has no such notice) | a strip with an orange edge for what must not be missed: `Set(bold, small)`, `Set(null)` takes it away; `Shown`, `Bold`, `Small`. The inspectors show one for an asset from a newer SFX Forge |
| `SfxFooter` | SfxKit.cs | the two lines under the values (`footer()`) | `Set(bold, colour, small)` |
| `PageTabs` | SoftKeys.cs | the stage's page tabs | `new PageTabs("OSC", …)`, `Accent`, `SetIndex`, `SetAvailable`, `changed` |
| `SoftKey`, `SoftKeyRow` | SoftKeys.cs | F1 – F6 | `row.Add("PLAY", action, tooltip)`; `Lit` (green: it is on), `Off` (dim: nothing to do, no click). `new SoftKeyRow(true)`: a smaller second row. A row wraps into rows of the same number of keys (3 + 3, 2 + 2 – never 3 + 1). `AddBlank()`: an empty place at the end, for a number of keys that does not divide (7, 10) |
| `Knob` | ValueTile.cs | the ring with the encoder's number (`dial()`) | a picture only: `Set(bar, centre, colour)`; `Unlit`: dim, whatever its colour |
| `ValueTile` | ValueTile.cs | a value: ring, dim label, large value in its unit (`hud()`, `row()`) | `Show(label, text, bar, centre, colour)`, `ShowMixed`, `ShowEmpty`; it keeps no value – listen to `nudged`, `typed`, `resetRequested`, `chosen`, `dragStarted` / `dragEnded`. `Look.Corner` (in a picture) or `Look.Row` (a tile of its own). `Interactive = false`: view only, and it looks it – an outline instead of a tile, a dim dial (class `sfx-tile--view`). `Locked = true`: a thin outline (class `sfx-tile--locked`) for a value MUTATE and the variants leave alone; the panel puts ` *` behind its label |
| `SfxPicture` | WaveformView.cs | the samples the device draws a sound from (`preview_render()`): one dry voice, 24 kHz, 1.5 s at most, a held sound let go after 0.8 s | `Render(patch, into)` -> how many samples; `Rate`, `Max`, `Hold`. Not what PLAY and BAKE WAV render: those have the effects |
| `WaveformView` | WaveformView.cs | the picture of a sound page (`scene()`) | `SetSamples(mono, count)`, `SetColour`, the white line: fill `Overlay`, `SetOverlay(count)`; `SetMark(x)`: a thin upright line (where a held sound is let go); `SetCaptions`; `SetMessage` instead of a picture; `Compact = true`: a small one without room for captions (a patch's tile) |
| `PadGrid` | PadGrid.cs | the 16 pads as tiles | `SetPad(i, name, colour, PadState)`, `pressed` / `released` (one for every `pressed`, also when the mouse is taken away without its button going up), `Tile(i)` |
| `StepRow` | SongBits.cs | a sound's row of steps in its pattern's grid: 16, 32 or 64 cells (the stage) | fill `Level[]`, `Hold[]` (the line of a step that holds), set `Count` (16, 32 or 64; the arrays hold `MaxSteps` = 64), `Length`, `Head`, `Accent`, `Dimmed`, `Selected`, then `Changed()`. `Level`, `Hold`, `Length` and `Head` count in cells. At 32 and 64 the cells are plain rectangles on whole pixels, with a pixel more between two sixteenths. It may have children (the song puts the chosen sound's notes over it, at 16 cells only) |
| `PatternTile` | SongBits.cs | a pattern A – H: 16 small bars, one a sixteenth, and `32` or `64` in its corner when its grid is finer (`draw_song()`) | `Set(soundsOnStep, PatternState, steps = 16)`: for each sixteenth how many sounds are set anywhere in it, and the pattern's grid; `GridText`, `Column(i)`, `clicked` |
| `SfxFader` | SongBits.cs | the fader and the pan of a MIX tile | `SetLevel(value, mark, colour)` or `SetPan(pan, colour)`; shows only |
| `IntensityStrip` | SongViews.cs | INTENSITY with every sound's LAYER mark over it (the stage) | `Value` (0..127), `SetMark(sound, level, colour)`, `Text`, `Fill`; dragging tells `changed`; `Interactive = false`: a bar that only shows (the LAYER bar) |
| `LaneRow` | SongViews.cs | an automation lane over one pattern (the stage's lanes) | fill `Point[]` (-1: none), `Plays[]`, set `Count` (the cells: 16, 32 or 64, the pattern's grid), `Hi`, `Length`, `Accent`, then `Changed()` |
| `MixTile` | SongViews.cs | a sound on the MIX page: name, fader, pan, M | `Set(name, colour, volume, pan, muted, selected)`, `clicked` |
| `MasterView` | SongViews.cs | the MASTER page's two pictures: the delay's echoes, the reverb's fading | `Set(delayTime, feedback, size, damping)` |
| `LinkBars` | LinkViews.cs | the LINK page's panel: a yellow title, a line a value with its number and a thin bar (`draw_link()`) | `new LinkBars(title, names…)`, `SetValue(i, 0..127)` (-1: "-", none came) |
| `PortTile` | LinkViews.cs | a value tile of the LINK page, for a MIDI port: the ring (green all round while open) with IN / OUT in it | `Show(label, name, open, there)`, `Locked`, `clicked` |
| `PatchTile` | LinkViews.cs | a patch that came from the device: pad number on the engine's colour, engine, time, the sound's shape, PLAY and SAVE / SHOW | `Set(pad, engine, time, saved)`, `SetPicture(patch, native)`, `play` / `save` / `show`; `PatchTile.Picture(patch, out count)`: the samples a picture is made of (`SfxPicture`) |
| `VariantTile` | VariantTile.cs | – (a `PatchTile`'s look, for a variant of a sound) | `Set(number, engine, kept, word)`: the number on the engine's colour, KEEP lit while the sound is this variant; `SetPicture(patch, native)`, `ClearPicture`; `play` / `keep`, `PlayKey`, `KeepKey`; `ShowKeep = false`: PLAY alone (a variation set's tiles) |
| `SfxUnits` | SfxUnits.cs | every value in its unit, every name, the pages of four | `ParamText(k, v)`, `ParamName(k, engine)`, `AutoName` / `AutoText` / `AutoHi` (what a lane moves), `Percent`, `TimeText`, `PanText`, `NoteText`, `DelayTimeText`, `PageParams`, `TryParse` for typed values, `TryParseSeconds`; for the tools `DbText` / `DbNumber` ("-3.2dB", "SILENT"), `CentsText` ("±50ct"), `SeedText` / `TryParseSeed` (eight hex digits), `TryParseDb`, `PageMask(page)` (a page's values as `SfxParam` bits). The curves use the SDK's own 2^x (`vd_exp2`) in float steps, so every text is the device's |

## Rules the controls keep

- A control shows what it is given and tells what the mouse did. The value lives in the asset: change it through a
  `SerializedObject`, then show it again (`SfxPresetPanel` is the example: `Apply`, `Refresh`).
- A drag is one undo step: `dragStarted` -> `Undo.IncrementCurrentGroup`, each change -> `Undo.CollapseUndoOperations`.
- Dragging a tile up or down turns it (SHIFT: finer). The wheel turns it only over the ring, so an Inspector still scrolls.
  A double-click on the value types one, a double-click on the ring resets it. With `Choices`, a click on the value opens them.
- Several things selected and they differ: `ShowMixed` – the value reads "-".
- An undo step is named after what it changes ("Edit SFX Preset Jump"): a key that calls Unity's Undo asks first whether
  Unity's next step has that name (`Undo.GetCurrentGroupName`), and is dark while it has not.
- Tiles in rows wrap into complete rows; one left over is as wide as the others, never as wide as the page.
- What is drawn (`generateVisualContent`) allocates nothing of its own: arrays are kept in the element, the waveform is one
  mesh strip.

## An inspector as one element

Each inspector is a static `Build(SerializedObject) -> VisualElement` that `CreateInspectorGUI` returns
(`SfxPresetEditor.Build`). A test or a picture builds the same element without an Inspector window.

`Editor/AssemblyInfo.cs` names `CatSplat.SfxForge.Editor.Tests` in an `InternalsVisibleTo`: that is the test project
of the source repository (`tests/unity-inspectors`), which is not in the package. What it opens: `SfxUI.PretendNativeMissing`,
which makes the panels show their state without the native library, and `SfxPresetPanel.Seeds`, where the tools' seeds come from
(a test sets it and gets the same sounds). `Runtime/AssemblyInfo.cs` names the same assembly, for `SfxVariationSet.PretendNativeMissing`. Any assembly of
that name in a project sees it too, and whatever is made `internal` here later.

## The tools of a sound

`SfxPresetPanel` has, under the picture, a **level line** (an `SfxFooter`: peak and RMS of the sound as PLAY and BAKE WAV
render it, orange with `CLIPS` when it is over full scale) and, under the link note, a **tool drawer**: a `PageTabs`
(`NEW`, `VARY`, `LEVEL`, `SHARE`) over four boxes of the class `sfx-tools`, one shown at a time. The tab is a static of
the panel, as HEAR CHANGES is: every Inspector shows the same one. Each box is small `SoftKeyRow`s and an `SfxFooter`
that says what the tool did last, or why it did nothing:

- `NEW`: ten keys, one a kind of sound (`SfxGenerator`); the note names the seed that gives the sound again.
- `VARY`: an `AMOUNT` tile (`Look.Row`), keys that lock the page's four values (`ValueTile.Locked` shows it; the
  engine's place is a dim `ENGINE STAYS`: nothing changes the engine), and MORE LIKE THIS: a `sfx-patches` grid of eight
  `VariantTile`s, two in a row under 560 px, four from there, each captioned with the value that moved most from the
  base ("ENV TIME -19"). Their pictures are drawn one every 10 ms (`FinishPictures()` draws what is still to come at
  once: for a picture or a test).
- `LEVEL`: `MATCH RMS` and `MATCH PEAK` for several selected sounds.
- `SHARE`: the sound's text in a `TextField` of the class `sfx-field`, and `COPY`, `PASTE`, `EXPORT .SYX`, `IMPORT .SYX`.
  The field's letters shrink until all 62 characters show (`FitShareText`, never under 6 px).

Amount and locks are the session's (`SessionState`), not the asset's. Every tool that changes a sound goes through the
panel's `Edit`, so it is one undo step named after what it did ("Generate SFX Preset Jump", "Keep …", "Level 3 SFX
Presets", "Paste …") and the UNDO key lights for it. MAKE SET makes a file and is no undo step. A tool's note is about
the sounds as its key left them: the panel remembers a number for them (`SoundsNow()`) and takes the note down when an
edit or an undo made them something else. The level line measures a few sounds a tick (`MeasureSome`), so a large
selection of long sounds does not hold the Inspector up; `MeasureNow()` does it at once.

`SfxVariationSetPanel` (made by `SfxVariationSetEditor.Build`) is built the same way: a header, an `SfxBanner` for the
state of its clips, an `ObjectField` of the class `sfx-object` for its preset, six tiles in `sfx-song-values`, a key a
page under VARIANTS KEEP with a line in words under them (which pages and which single values are kept), its variants
as `VariantTile`s without KEEP, and a small key for MONO in a `sfx-toggle-row`.

## The bake window

Window > SFX Forge Bake is `SfxBakeView` over a `SfxBakeModel` (both in `SfxBakeView.cs`), as the SFX Forge window is a
view over a model: the model is what is shown (how many presets, sets and files the selection holds, the options, where
the files go, the last run's `SfxBakeReport`) and what can be asked for (`bake`, `bakeSets`, `show`, `setTo`,
`setFolder`, `optionsChanged`). The view never touches the AssetDatabase: `SfxBakeWindow` fills the model from
`Selection` and answers the actions with `SfxBatchBake`; a test or a picture fills it with anything
(`SfxBakeWindow.Build(model)`). Where the files go is two keys under THE FILES GO, the lit one being where
(`view.ToKey(SfxBakeTo)`), as a page's tab is. The last run's newest eight lines stand in an `sfx-log` box; a line that
failed or has a note has the class `sfx-log__line--warn`, and wraps instead of being cut. With nothing to bake the
header's number is not lit. After the model changed: `view.Refresh()`.

## The song

`SfxSongPanel` (made by `SfxSongEditor.Build`) is a viewer of the packed song, read through `SfxSong`'s accessors
(`Grid`, `GridSteps`, `GridStepOn`, `GridStepNote`, `GridStepVelocity`, `GridStepLength`, `GridLanePoint`, `LaneSound`,
`LaneTarget`, `Master`, `Muted` …). It changes five numbers of the song's head through the `SerializedObject` – TEMPO,
SWING, a sound's LAYER, VOLUME and PAN – and nothing else: steps, patterns and their grids, the chain, the
arrangement, mutes, the master and the lanes only show. The pattern, the sound and the intensity it is looked at
with are not part of the song.

From firmware 1.6.0 on (song format 6) each pattern has its own grid of 16, 32 or 64 steps a bar, and the panel
follows the pattern looked at:

- The title beside the page's name says it: `PATTERN B  1/32` (`PatternTitle`, `GridName(steps)`).
- The rows of STEPS and the lanes of AUTO draw 16, 32 or 64 cells (`RowCount(sound)`, `StepRow.Count`,
  `LaneRow.Count`). The chosen sound's notes are written in the cells at 16 only; at 32 and 64 the line under the
  tiles names the steps that are set, each with its number.
- A pattern's tile keeps 16 columns, a sixteenth each, and carries `32` or `64` in its corner.
- `SfxSong`'s older accessors (`StepOn`, `StepNote`, `StepVelocity`, `StepLength`, `LanePoint`) count sixteenths and
  read the first step of each: a panel that draws every step uses the `Grid…` ones.

The line under the keys is `LinkState(linked, port, device, song, out bold, out small, out colour)`, which also says
whether SEND is off: `LINKED`, `NOT CONNECTED`, `ANOTHER FORMAT` (the device cannot take this song: a format-6 song
and a firmware before 1.6.0, for one) or `A DAMAGED SONG` (a value out of range, `SfxPackedSong.IsPlayable`). The
last two say "SEND is off:" and the text of `SfxLink.Refusal(device, song)`.

## The window

Window > SFX Forge is `SfxForgeView` over a `SfxForgeModel` (both in `SfxForgeView.cs`): the model is what is shown (the
ports, the link's state, the knobs' last values, the pads held, the patches and messages that came) and what can be
asked for (`connect`, `playPad`, `fetchPad`, `save` …, as actions). `model.identity` is the line under the link's state: what the
device said it is, or that it did not answer (null: no line). The view never touches a MIDI port: `SfxForgeWindow`
fills the model from `SfxLink` and answers the actions, a test or a picture fills it with anything
(`SfxForgeWindow.Build(model)`). After the model changed: `view.Refresh()`.
