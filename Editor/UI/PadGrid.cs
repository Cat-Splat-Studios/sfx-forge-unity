// SPDX-License-Identifier: GPL-3.0-or-later
//
// The UI kit's 16 pads: PadState is how a pad's tile looks, PadGrid the four rows of four.
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatSplat.SfxForge.Editor
{
    /// <summary>
    /// How a pad's tile looks: Dim – a sound that rests; Selected – the one chosen (its colour, dark text); Lit – it
    /// sounds now (white); Empty – nothing on it.
    /// </summary>
    public enum PadState
    {
        Empty,
        Dim,
        Selected,
        Lit,
    }

    /// <summary>
    /// The 16 pads as tiles, four in a row, laid out as the pads are – each with its number, a name and its colour
    /// (the stage's pads in the firmware). Set a tile with SetPad; pressed / released tell which pad the mouse holds –
    /// released comes for every pressed, also when the mouse is taken away without its button going up.
    /// </summary>
    public class PadGrid : VisualElement
    {
        public const int Pads = 16;

        readonly VisualElement[] tiles = new VisualElement[Pads];
        readonly TextElement[] numbers = new TextElement[Pads];
        readonly TextElement[] names = new TextElement[Pads];
        readonly bool[] down = new bool[Pads];
        public event Action<int> pressed,
            released;

        public PadGrid()
        {
            AddToClassList("sfx-pads");
            for (int i = 0; i < Pads; i++)
            {
                int pad = i;
                var cell = SfxUI.Box("sfx-pad-cell");
                var tile = SfxUI.Box("sfx-pad");
                numbers[i] = SfxUI.Text((i + 1).ToString(), "sfx-pad__number");
                names[i] = SfxUI.Text("", "sfx-pad__name");
                tile.Add(numbers[i]);
                tile.Add(names[i]);
                tile.RegisterCallback<PointerDownEvent>(e =>
                {
                    if (e.button != 0 || down[pad])
                    {
                        return;
                    }

                    down[pad] = true;
                    tile.CapturePointer(e.pointerId);
                    pressed?.Invoke(pad);
                    e.StopPropagation();
                });
                tile.RegisterCallback<PointerUpEvent>(e =>
                {
                    if (e.button != 0)
                    {
                        return;
                    }

                    LetGo(pad);
                    if (tile.HasPointerCapture(e.pointerId))
                    {
                        tile.ReleasePointer(e.pointerId);
                    }
                });

                // the capture went without the button going up (another window came to the front, a dialog opened,
                // the window closed): the pad is let go all the same
                tile.RegisterCallback<PointerCaptureOutEvent>(_ => LetGo(pad));
                tile.RegisterCallback<DetachFromPanelEvent>(_ => LetGo(pad));
                tiles[i] = tile;
                cell.Add(tile);
                Add(cell);
                SetPad(i, "", SfxColors.Line, PadState.Empty);
            }
        }

        void LetGo(int pad)
        {
            if (!down[pad])
            {
                return;
            }

            down[pad] = false;
            released?.Invoke(pad);
        }

        /// <summary>The tile of a pad, for a tooltip or more inside it.</summary>
        public VisualElement Tile(int pad) => tiles[pad];

        public void SetPad(int pad, string name, Color colour, PadState state)
        {
            if (pad < 0 || pad >= Pads)
            {
                return;
            }

            bool dark = state == PadState.Selected || state == PadState.Lit;
            names[pad].text = name;
            tiles[pad].style.backgroundColor =
                state == PadState.Lit ? SfxColors.White
                : state == PadState.Selected ? colour
                : state == PadState.Dim ? SfxColors.Mix(SfxColors.Bg, colour, 0.32f)
                : SfxColors.Hex(0x1A120E);
            numbers[pad].style.color =
                dark ? SfxColors.Bg
                : state == PadState.Dim ? SfxColors.Mix(SfxColors.Bg, colour, 0.9f)
                : SfxColors.Off;
            names[pad].style.color =
                dark ? SfxColors.Bg
                : state == PadState.Dim ? SfxColors.Text
                : SfxColors.Off;
        }
    }
}
