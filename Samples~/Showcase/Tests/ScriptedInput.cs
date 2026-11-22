using SfxShowcase.Core;

namespace SfxShowcase.Tests
{
    /// <summary>
    /// The controls as a test sets them: assign it to <see cref="PlayerBase.InputOverride"/> and write the three
    /// values. The player detects a Jump press itself, so a press is "false, then true".
    /// </summary>
    public sealed class ScriptedInput : IPlayerInput
    {
        /// <inheritdoc/>
        public float MoveX { get; set; }

        /// <inheritdoc/>
        public bool Down { get; set; }

        /// <inheritdoc/>
        public bool JumpHeld { get; set; }

        /// <summary>Lets go of everything.</summary>
        public void Release()
        {
            MoveX = 0f;
            Down = false;
            JumpHeld = false;
        }
    }
}
