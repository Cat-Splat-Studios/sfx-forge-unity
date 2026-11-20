# Third Party Notices

The package's code, the showcase, the manuals and their figures are Cat Splat Studios' own work under the MIT
licence ([LICENSE.md](LICENSE.md)). Everything you hear is synthesized by that code: there are no samples or
recordings. Three things come from, or depend on, someone else's work: the VIBE FM SDK (in the firmware and in the
videos), the fonts of the device's display (in the firmware and in the videos), and the C runtime that the compiler
links into the native library. Their licence and notice texts are in
[`Documentation~/licenses`](Documentation~/licenses), copied unchanged.

## VIBE FM SDK

- Project: <https://github.com/Vibe-FM-SDK/vibe-fm-sdk>
- Licence: Apache License 2.0, <https://www.apache.org/licenses/LICENSE-2.0>
- Author: Sven Trogus (zvenson). "VIBE FM" is its owner's trademark.

What of it is here:

- **In the videos** (`Documentation~/video`, and the previews and still frames cut from them in
  `Documentation~/img/readme`): the picture of the device in `sfx-forge-tour.mp4` and `sfx-forge-build-a-song.mp4` is
  the device as the SDK's emulator draws it, and `sfx-forge-unity-link.mp4` shows the SDK's emulator page itself,
  recorded while SFX FORGE ran in it.
- **In `Firmware~/sfx-forge.zip`**: the SDK's build compiles two of its own source files into every firmware, and
  so into `sfx-forge.wasm`: its runtime (`sdk/src/vibe_sdk.c`) and its display fonts (`sdk/src/vibe_font.c`),
  together with what its headers contribute. They are there in compiled form, unchanged, under the Apache License
  2.0. The zip also holds SFX FORGE's own compiled code, its description and its icon.
- Licence text: [`VIBE-FM-SDK-LICENSE-Apache-2.0.txt`](Documentation~/licenses/VIBE-FM-SDK-LICENSE-Apache-2.0.txt).
  The SDK's NOTICE file, as Apache-2.0 section 4(d) asks:
  [`VIBE-FM-SDK-NOTICE.txt`](Documentation~/licenses/VIBE-FM-SDK-NOTICE.txt).

No source file of the SDK is in this package, and the Unity code does not use it.

## The device's display fonts

- The SDK's `vibe_font.c` is glyph bitmaps rendered from fonts under the SIL Open Font License 1.1. Its own first
  lines name them: "Fonts: Roboto Condensed (the UI), Archivo (the logo) – SIL OFL 1.1". The NOTICE beside it
  (`sdk/NOTICE`, the one copied here) says the same; the NOTICE at the root of the SDK's repository names JetBrains
  Mono and Archivo instead. Which of the two is right for the UI font was not determined here.
- Copyright lines and licence text, as the SDK ships them:
  [`VIBE-FM-SDK-LICENSE-FONTS-OFL-1.1.txt`](Documentation~/licenses/VIBE-FM-SDK-LICENSE-FONTS-OFL-1.1.txt).
- Where they are: as bitmaps inside `Firmware~/sfx-forge.zip` (`sfx-forge.wasm`), and as the text on the device's
  display wherever the videos, previews and still frames show the device.

## The native library's C runtime

`Runtime/Plugins/x86_64/sfxforge.dll` is SFX FORGE's engine compiled with Zig 0.13.0 (`zig cc`, target
`x86_64-windows-gnu`). A game built with this package ships that file, so this applies to the game as well.

- **mingw-w64 runtime.** The linker puts the mingw-w64 C runtime's start-up code into the library (it is what
  prints "Mingw-w64 runtime failure"). Licence: [`mingw-w64-COPYING.txt`](Documentation~/licenses/mingw-w64-COPYING.txt),
  the file Zig ships with that runtime (Zope Public License 2.1, with parts in the public domain or under other
  permissive terms).
- **Zig's compiler runtime.** The compiler may also have linked helper routines of its own `compiler_rt` into the
  library and into the firmware. Whether any are in there was not determined. Licence, in case:
  [`Zig-LICENSE.txt`](Documentation~/licenses/Zig-LICENSE.txt) (MIT).
- The library imports the Universal C Runtime that is part of Windows (`api-ms-win-crt-*.dll`) and `KERNEL32.dll`;
  nothing of those is in the package.

## Not included, but named

- loopMIDI (Tobias Erichsen) is named as an example of a virtual MIDI cable. It is not part of the package.
- The showcase is an original game. Its character, level, art and song are Cat Splat Studios' own.
