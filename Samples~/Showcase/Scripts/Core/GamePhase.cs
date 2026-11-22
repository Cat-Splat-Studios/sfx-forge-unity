namespace SfxShowcase.Core
{
    /// <summary>Where a run is.</summary>
    public enum GamePhase
    {
        /// <summary>The title card over the live level, waiting for Jump.</summary>
        Title = 0,

        /// <summary>The run: the timer counts.</summary>
        Playing = 1,

        /// <summary>The Resonator was passed: the results panel, waiting for Jump to play again.</summary>
        Finished = 2,
    }
}
