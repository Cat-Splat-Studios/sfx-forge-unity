using SfxShowcase.Core;
using UnityEngine;

namespace SfxShowcase.Art
{
    /// <summary>
    /// A Woofer's reaction: when it fires, the cone punches out along the Woofer's axis and wobbles back to rest like a
    /// struck speaker – a decaying cosine, which is also what the bounce sounds like.
    /// </summary>
    public sealed class WooferVisual : PropVisual
    {
        [SerializeField, Tooltip("The cone, a child that moves along local +y.")]
        Transform cone;
        [SerializeField, Tooltip("How far the cone punches out, metres.")]
        float punchDistance = 0.35f;
        [SerializeField, Tooltip("Wobbles per second while it settles.")]
        float wobbleFrequency = 5f;
        [SerializeField, Tooltip("How fast the wobble dies away, per second.")]
        float damping = 6f;
        [SerializeField, Tooltip("How much the cone stretches along the axis per metre it is pushed out.")]
        float stretchPerMetre = 1.5f;
        [SerializeField, Tooltip("Seconds after which the cone is considered at rest again.")]
        float settleSeconds = 1f;

        [SerializeField, Tooltip("Where the cone sits at rest, in the Woofer's local space.")]
        Vector3 restPosition;

        float sinceFired;

        /// <summary>Sets the part to animate; where it is now becomes its rest position. Called by <see cref="WooferArt"/>.</summary>
        public void Configure(Transform conePart)
        {
            cone = conePart;
            restPosition = conePart.localPosition;
        }

        /// <inheritdoc/>
        public override void Activate()
        {
            sinceFired = 0f;
            enabled = true;
        }

        /// <inheritdoc/>
        public override void ResetVisual()
        {
            Pose(0f);
            enabled = false;
        }

        void Awake()
        {
            enabled = false;
        }

        void Update()
        {
            sinceFired += Time.deltaTime;
            if (sinceFired >= settleSeconds)
            {
                ResetVisual();
                return;
            }
            Pose(punchDistance * Mathf.Exp(-damping * sinceFired) * Mathf.Cos(2f * Mathf.PI * wobbleFrequency * sinceFired));
        }

        void Pose(float pushedOut)
        {
            cone.localPosition = restPosition + Vector3.up * pushedOut;
            cone.localScale = new Vector3(1f, 1f + pushedOut * stretchPerMetre, 1f);
        }
    }
}
