using UnityEngine;

namespace SfxShowcase.Core
{
    /// <summary>
    /// The run as the other folders see it (see <see cref="PlayerBase"/> for why this is a class and not only the
    /// interface). The gameplay folder's session component derives from this.
    /// </summary>
    public abstract class GameSessionBase : MonoBehaviour, IGameSession
    {
        /// <inheritdoc/>
        public abstract GamePhase Phase { get; }

        /// <inheritdoc/>
        public abstract int ChimeCount { get; }

        /// <inheritdoc/>
        public abstract float ElapsedSeconds { get; }

        /// <inheritdoc/>
        public abstract float TopSpeedReached { get; }

        /// <inheritdoc/>
        public abstract SectionId CurrentSection { get; }

        /// <summary>Leaves the title card and starts the run (what pressing Jump on the title card does).</summary>
        public abstract void StartGame();

        /// <summary>Starts the level again from the beginning (what pressing Jump on the results panel does).</summary>
        public abstract void Restart();
    }
}
