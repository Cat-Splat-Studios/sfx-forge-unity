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
| `SoftKey`, `SoftKeyRow` | SoftKeys.cs | F1 – F6 | `row.Add("PLAY", action, tooltip)`; `Lit` (green: it is on), `Off` (dim: nothing to do, no click). `new SoftKeyRow(true)`: a smaller second row. A row wraps into rows of the same number of keys (3 + 3, 2 + 2 – never 3 + 1) |
| `Knob` | ValueTile.cs | the ring with the encoder's number (`dial()`) | a picture only: `Set(bar, centre, colour)`; `Unlit`: dim, whatever its colour |
| `ValueTile` | ValueTile.cs | a value: ring, dim label, large value in its unit (`hud()`, `row()`) | `Show(label, text, bar, centre, colour)`, `ShowMixed`, `ShowEmpty`; it keeps no value – listen to `nudged`, `typed`, `resetRequested`, `chosen`, `dragStarted` / `dragEnded`. `Look.Corner` (in a picture) or `Look.Row` (a tile of its own). `Interactive = false`: view only, and it looks it – an outline instead of a tile, a dim dial (class `sfx-tile--view`) |
| `SfxPicture` | WaveformView.cs | the samples the device draws a sound from (`preview_render()`): one dry voice, 24 kHz, 1.5 s at most, a held sound let go after 0.8 s | `Render(patch, into)` -> how many samples; `Rate`, `Max`, `Hold`. Not what PLAY and BAKE WAV render: those have the effects |
| `WaveformView` | WaveformView.cs | the picture of a sound page (`scene()`) | `SetSamples(mono, count)`, `SetColour`, the white line: fill `Overlay`, `SetOverlay(count)`; `SetMark(x)`: a thin upright line (where a held sound is let go); `SetCaptions`; `SetMessage` instead of a picture; `Compact = true`: a small one without room for captions (a patch's tile) |
| `PadGrid` | PadGrid.cs | the 16 pads as tiles | `SetPad(i, name, colour, PadState)`, `pressed` / `released` (one for every `pressed`, also when the mouse is taken away without its button going up), `Tile(i)` |
| `StepRow` | SongBits.cs | a sound's row of 16 steps (the stage) | fill `Level[]`, `Hold[]` (the line of a step that holds), `Length`, `Head`, `Accent`, `Dimmed`, `Selected`, then `Changed()`. It may have children (the song puts the chosen sound's notes over it) |
| `PatternTile` | SongBits.cs | a pattern A – H with its steps as small bars (`draw_song()`) | `Set(soundsOnStep, PatternState)`, `clicked` |
| `SfxFader` | SongBits.cs | the fader and the pan of a MIX tile | `SetLevel(value, mark, colour)` or `SetPan(pan, colour)`; shows only |
| `IntensityStrip` | SongViews.cs | INTENSITY with every sound's LAYER mark over it (the stage) | `Value` (0..127), `SetMark(sound, level, colour)`, `Text`, `Fill`; dragging tells `changed`; `Interactive = false`: a bar that only shows (the LAYER bar) |
| `LaneRow` | SongViews.cs | an automation lane over one pattern (the stage's lanes) | fill `Point[]` (-1: none), `Plays[]`, set `Hi`, `Length`, `Accent`, then `Changed()` |
| `MixTile` | SongViews.cs | a sound on the MIX page: name, fader, pan, M | `Set(name, colour, volume, pan, muted, selected)`, `clicked` |
| `MasterView` | SongViews.cs | the MASTER page's two pictures: the delay's echoes, the reverb's fading | `Set(delayTime, feedback, size, damping)` |
| `LinkBars` | LinkViews.cs | the LINK page's panel: a yellow title, a line a value with its number and a thin bar (`draw_link()`) | `new LinkBars(title, names…)`, `SetValue(i, 0..127)` (-1: "-", none came) |
| `PortTile` | LinkViews.cs | a value tile of the LINK page, for a MIDI port: the ring (green all round while open) with IN / OUT in it | `Show(label, name, open, there)`, `Locked`, `clicked` |
| `PatchTile` | LinkViews.cs | a patch that came from the device: pad number on the engine's colour, engine, time, the sound's shape, PLAY and SAVE / SHOW | `Set(pad, engine, time, saved)`, `SetPicture(patch, native)`, `play` / `save` / `show`; `PatchTile.Picture(patch, out count)`: the samples a picture is made of (`SfxPicture`) |
| `SfxUnits` | SfxUnits.cs | every value in its unit, every name, the pages of four | `ParamText(k, v)`, `ParamName(k, engine)`, `AutoName` / `AutoText` / `AutoHi` (what a lane moves), `Percent`, `TimeText`, `PanText`, `NoteText`, `DelayTimeText`, `PageParams`, `TryParse` for typed values, `TryParseSeconds`. The curves use the SDK's own 2^x (`vd_exp2`) in float steps, so every text is the device's |

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
of the source repository (`tests/unity-inspectors`), which is not in the package. What it opens is one member,
`SfxUI.PretendNativeMissing`, which makes the panels show their state without the native library. Any assembly of
that name in a project sees it too, and whatever is made `internal` here later.

## The song

`SfxSongPanel` (made by `SfxSongEditor.Build`) is a viewer of the packed song, read through `SfxSong`'s accessors
(`StepOn`, `StepNote`, `StepVelocity`, `StepLength`, `LaneSound`, `LaneTarget`, `LanePoint`, `Master`, `Muted` …).
It changes five numbers of the song's head through the `SerializedObject` – TEMPO, SWING, a sound's LAYER, VOLUME and
PAN – and nothing else: steps, patterns, the chain, the arrangement, mutes, the master and the lanes only show.
The pattern, the sound and the intensity it is looked at with are not part of the song.

## The window

Window > SFX Forge is `SfxForgeView` over a `SfxForgeModel` (both in `SfxForgeView.cs`): the model is what is shown (the
ports, the link's state, the knobs' last values, the pads held, the patches and messages that came) and what can be
asked for (`connect`, `playPad`, `fetchPad`, `save` …, as actions). `model.identity` is the line under the link's state: what the
device said it is, or that it did not answer (null: no line). The view never touches a MIDI port: `SfxForgeWindow`
fills the model from `SfxLink` and answers the actions, a test or a picture fills it with anything
(`SfxForgeWindow.Build(model)`). After the model changed: `view.Refresh()`.
