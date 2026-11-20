namespace SfxShowcase.Core
{
    /// <summary>What the HUD, the results panel and the audio may read about the run.</summary>
    public interface IGameSession
    {
        /// <summary>Title card, playing, or finished.</summary>
        GamePhase Phase { get; }
        /// <summary>The Chimes the player carries now.</summary>
        int ChimeCount { get; }
        /// <summary>Seconds since the run began; stops at the goal, keeps running through respawns.</summary>
        float ElapsedSeconds { get; }
        /// <summary>The highest <see cref="IPlayerState.Speed"/> of the run so far, m/s: never above the speed cap.</summary>
        float TopSpeedReached { get; }
        /// <summary>The section the player is in.</summary>
        SectionId CurrentSection { get; }
    }
}
