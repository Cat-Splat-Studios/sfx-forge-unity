using UnityEngine;

namespace SfxShowcase.Art
{
    /// <summary>
    /// Makes a Chime look as if it turns slowly about its vertical axis by scaling its width. The phase depends on
    /// where the Chime is, so a line of Chimes turns as a travelling wave rather than all at once.
    /// </summary>
    public sealed class ChimeSpin : MonoBehaviour
    {
        [SerializeField, Tooltip("The diamond whose width is scaled.")]
        Transform diamond;

        [SerializeField, Tooltip("Turning speed, radians per second.")]
        float turnRate = 3f;

        [SerializeField, Tooltip("Phase shift per metre along x, radians: the wave along a line of Chimes.")]
        float phasePerMetre = 0.6f;

        /// <summary>Sets the part to animate. Called by <see cref="ChimeArt"/>.</summary>
        public void Configure(Transform diamondPart)
        {
            diamond = diamondPart;
        }

        void Update()
        {
            float width = Mathf.Cos(Time.time * turnRate - transform.position.x * phasePerMetre);
            diamond.localScale = new Vector3(width, 1f, 1f);
        }
    }
}
