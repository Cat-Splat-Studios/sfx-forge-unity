using SfxShowcase.Core;
using UnityEngine;

namespace SfxShowcase.Art
{
    /// <summary>
    /// The reaction of a Tuning Fork and of the Resonator: when struck it switches to its lit look and stays lit, and
    /// it rings – its prongs shiver in and out, fast and dying away, like a struck fork.
    /// </summary>
    public sealed class ForkVisual : PropVisual
    {
        [SerializeField, Tooltip("The Dormant Grey look, shown until the fork is struck. None for the Resonator, which is always lit.")]
        GameObject unlitPart;
        [SerializeField, Tooltip("The Fork Green look.")]
        GameObject litPart;
        [SerializeField, Tooltip("How far the prongs shiver, as a fraction of the fork's width.")]
        float ringAmount = 0.12f;
        [SerializeField, Tooltip("Shivers per second.")]
        float ringFrequency = 9f;
        [SerializeField, Tooltip("How fast the ringing dies away, per second.")]
        float ringDamping = 2.5f;
        [SerializeField, Tooltip("Seconds the fork rings for.")]
        float ringSeconds = 1.6f;

        float sinceStruck;

        /// <summary>Sets the two looks. Called by <see cref="ForkArt"/>; starts unlit.</summary>
        public void Configure(GameObject unlit, GameObject lit)
        {
            unlitPart = unlit;
            litPart = lit;
            ShowLit(false);
        }

        /// <inheritdoc/>
        public override void Activate()
        {
            ShowLit(true);
            sinceStruck = 0f;
            enabled = true;
        }

        /// <inheritdoc/>
        public override void ResetVisual()
        {
            ShowLit(false);
            SetWidth(1f);
            enabled = false;
        }

        void Awake()
        {
            enabled = false;
        }

        void Update()
        {
            sinceStruck += Time.deltaTime;
            if (sinceStruck >= ringSeconds)
            {
                SetWidth(1f);
                enabled = false;
                return;
            }
            SetWidth(1f + ringAmount * Mathf.Exp(-ringDamping * sinceStruck) * Mathf.Sin(2f * Mathf.PI * ringFrequency * sinceStruck));
        }

        void ShowLit(bool lit)
        {
            if (unlitPart == null) return;
            unlitPart.SetActive(!lit);
            litPart.SetActive(lit);
        }

        void SetWidth(float width)
        {
            litPart.transform.localScale = new Vector3(width, 1f, 1f);
        }
    }
}
