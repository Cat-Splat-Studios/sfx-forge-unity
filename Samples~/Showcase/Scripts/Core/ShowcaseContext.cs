using UnityEngine;

namespace SfxShowcase.Core
{
    /// <summary>
    /// What the scene builder passes from one construction step to the next: each step reads what the earlier ones made
    /// and adds its own (ARCHITECTURE.md, "How the scene is generated"). Only used while the scene is being built.
    /// </summary>
    public sealed class ShowcaseContext
    {
        /// <summary>The art kit asset. Set by the builder before any step runs.</summary>
        public ArtKitBase Art { get; set; }

        /// <summary>The built level. Set by the level step.</summary>
        public LevelDescriptor Level { get; set; }

        /// <summary>The player. Set by the gameplay step.</summary>
        public PlayerBase Player { get; set; }

        /// <summary>The run. Set by the gameplay step.</summary>
        public GameSessionBase Session { get; set; }

        /// <summary>The game camera. Set by the camera step.</summary>
        public Camera Camera { get; set; }
    }
}
