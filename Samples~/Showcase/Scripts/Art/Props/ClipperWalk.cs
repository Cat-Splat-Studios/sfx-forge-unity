using UnityEngine;

namespace SfxShowcase.Art
{
    /// <summary>
    /// A Clipper's walk on the spot. It moves like the square wave it is: legs and body jump between two positions,
    /// with nothing in between.
    /// </summary>
    public sealed class ClipperWalk : MonoBehaviour
    {
        [SerializeField, Tooltip("The block, which bobs with each step.")]
        Transform body;

        [SerializeField, Tooltip("The leg at the back.")]
        Transform backLeg;

        [SerializeField, Tooltip("The leg at the front.")]
        Transform frontLeg;

        [SerializeField, Tooltip("Steps per second.")]
        float stepsPerSecond = 5f;

        [SerializeField, Tooltip("How high a leg lifts, metres.")]
        float stepHeight = 0.1f;

        [SerializeField, Tooltip("How far the body drops on every other step, metres.")]
        float bodyBob = 0.05f;

        /// <summary>Sets the parts to animate. Called by <see cref="ClipperArt"/>.</summary>
        public void Configure(Transform bodyPart, Transform backLegPart, Transform frontLegPart)
        {
            body = bodyPart;
            backLeg = backLegPart;
            frontLeg = frontLegPart;
        }

        void Update()
        {
            bool backLegUp = Mathf.FloorToInt(Time.time * stepsPerSecond) % 2 == 0;
            SetHeight(backLeg, backLegUp ? stepHeight : 0f);
            SetHeight(frontLeg, backLegUp ? 0f : stepHeight);
            SetHeight(body, backLegUp ? 0f : -bodyBob);
        }

        static void SetHeight(Transform part, float height)
        {
            Vector3 position = part.localPosition;
            position.y = height;
            part.localPosition = position;
        }
    }
}
