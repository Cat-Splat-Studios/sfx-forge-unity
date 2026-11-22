using UnityEngine;

namespace SfxShowcase.Core
{
    /// <summary>
    /// The visual of a level piece that reacts when the piece is used: a Woofer's cone punches out, a Tuning Fork and
    /// the Resonator light up. The art kit returns one from its Create methods; the level's behaviour calls it.
    /// </summary>
    public abstract class PropVisual : MonoBehaviour
    {
        /// <summary>
        /// The piece was used: play the reaction (punch and wobble) or switch to the lit look and stay there.
        /// </summary>
        public abstract void Activate();

        /// <summary>Back to the look the piece had when the level started (unlit, at rest).</summary>
        public abstract void ResetVisual();
    }
}
