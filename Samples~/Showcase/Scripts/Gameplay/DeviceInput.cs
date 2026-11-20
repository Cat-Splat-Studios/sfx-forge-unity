using UnityEngine;
using SfxShowcase.Core;

namespace SfxShowcase.Gameplay
{
    /// <summary>
    /// The keyboard and gamepad, read through Unity's legacy Input Manager (the one this project has enabled).
    /// Left/right: arrows, A/D or the left stick. Down: down arrow, S or the stick. Jump: Space, Z or a gamepad face button.
    /// This is the only class that touches <see cref="Input"/>; to change the controls, or to move to the Input System
    /// package, replace this one class (or set <see cref="PlayerBase.InputOverride"/>).
    /// </summary>
    public sealed class DeviceInput : IPlayerInput
    {
        const string HorizontalAxis = "Horizontal";
        const string VerticalAxis = "Vertical";
        const string JumpButton = "Jump";

        /// <summary>The vertical axis has to be pushed at least this far down to count as "down".</summary>
        const float DownThreshold = -0.5f;

        /// <inheritdoc/>
        public float MoveX => Input.GetAxisRaw(HorizontalAxis);

        /// <inheritdoc/>
        public bool Down => Input.GetAxisRaw(VerticalAxis) < DownThreshold;

        /// <inheritdoc/>
        public bool JumpHeld =>
            Input.GetButton(JumpButton) || Input.GetKey(KeyCode.Z) || Input.GetKey(KeyCode.JoystickButton0);
    }
}
