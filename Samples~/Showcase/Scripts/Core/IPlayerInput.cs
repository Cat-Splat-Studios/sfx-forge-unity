namespace SfxShowcase.Core
{
    /// <summary>
    /// The whole control scheme: left/right, down, one Jump button. The gameplay code reads the keyboard and gamepad
    /// through this; a test replaces it with a scripted one (<see cref="PlayerBase.InputOverride"/>).
    /// </summary>
    public interface IPlayerInput
    {
        /// <summary>Left/right, -1..1.</summary>
        float MoveX { get; }
        /// <summary>Down is held (tuck, crouch, Wind-Up).</summary>
        bool Down { get; }
        /// <summary>The Jump button is held; the player detects the press itself.</summary>
        bool JumpHeld { get; }
    }
}
