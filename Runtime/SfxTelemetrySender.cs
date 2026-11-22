// SPDX-License-Identifier: MIT
using UnityEngine;

namespace CatSplat.SfxForge
{
    /// <summary>
    /// Streams one game value to the device (and to local SfxPlayers) while the game runs: a vehicle's speed raises the
    /// pitch of the sound that is held, danger opens its filter. One component per value.
    /// </summary>
    [AddComponentMenu("SFX Forge/SFX Telemetry Sender")]
    public class SfxTelemetrySender : MonoBehaviour
    {
        /// <summary>Where the value comes from.</summary>
        public enum Source
        {
            RigidbodySpeed,
            Rigidbody2DSpeed,
            AnimatorFloat,
            Manual,
        }

        [Tooltip(
            "What the value moves. Sounds: Pitch (up to +24 semitones), Cutoff (0.5 = none, ±4 octaves), FM "
                + "Depth, Level. Music: Intensity (layers come in), Pattern (A..H from the next bar)."
        )]
        public SfxTelemetry channel = SfxTelemetry.Pitch;

        /// <summary>Where the value comes from.</summary>
        public Source source = Source.RigidbodySpeed;

        [Tooltip("For Animator Float: the parameter's name.")]
        public string animatorParameter = "Speed";

        [Tooltip("For Manual: set this from your own code.")]
        public float value;

        [Tooltip("The source value that becomes 0.")]
        public float min = 0f;

        [Tooltip("The source value that becomes 1.")]
        public float max = 20f;

        /// <summary>Shapes the value between min and max: 0..1 in, 0..1 out.</summary>
        public AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("Players in the game that follow the same value – the game then sounds the same without the device.")]
        public SfxPlayer[] localPlayers = new SfxPlayer[0];

        [Tooltip("Music players in the game that follow the same value (Intensity, Pattern).")]
        public SfxMusicPlayer[] musicPlayers = new SfxMusicPlayer[0];

        Rigidbody body;
        Rigidbody2D body2D;
        Animator animator;
        int animatorHash;

        /// <summary>What was sent last, 0..1.</summary>
        public float Normalized { get; private set; }

        void Awake()
        {
            body = GetComponentInParent<Rigidbody>();
            body2D = GetComponentInParent<Rigidbody2D>();
            animator = GetComponentInParent<Animator>();
            animatorHash = Animator.StringToHash(animatorParameter);
        }

        float Read()
        {
            switch (source)
            {
#if UNITY_6000_0_OR_NEWER
                case Source.RigidbodySpeed:
                    return body != null ? body.linearVelocity.magnitude : 0f;
                case Source.Rigidbody2DSpeed:
                    return body2D != null ? body2D.linearVelocity.magnitude : 0f;
#else
                case Source.RigidbodySpeed:
                    return body != null ? body.velocity.magnitude : 0f;
                case Source.Rigidbody2DSpeed:
                    return body2D != null ? body2D.velocity.magnitude : 0f;
#endif
                case Source.AnimatorFloat:
                    return animator != null ? animator.GetFloat(animatorHash) : 0f;
                default:
                    return value;
            }
        }

        void Update()
        {
            float t = Mathf.Approximately(max, min) ? 0f : Mathf.Clamp01((Read() - min) / (max - min));
            Normalized = Mathf.Clamp01(curve.Evaluate(t));
            SfxLink.Shared.SendTelemetry(channel, Normalized); // (only goes out when its 7-bit value changes)
            for (int i = 0; i < localPlayers.Length; i++)
            {
                if (localPlayers[i] != null)
                {
                    localPlayers[i].ApplyTelemetry(channel, Normalized);
                }
            }

            for (int i = 0; i < musicPlayers.Length; i++)
            {
                if (musicPlayers[i] != null)
                {
                    musicPlayers[i].ApplyTelemetry(channel, Normalized);
                }
            }
        }

        void OnDisable()
        {
            // back to rest: no pitch, no FM, the filter and the level as they are
            // the music stays as it is
            if (channel == SfxTelemetry.Intensity || channel == SfxTelemetry.Pattern)
            {
                return;
            }

            float rest =
                channel == SfxTelemetry.Cutoff ? 64f / 127f
                : channel == SfxTelemetry.Level ? 1f
                : 0f;
            SfxLink.Shared.SendTelemetry(channel, rest);
            for (int i = 0; i < localPlayers.Length; i++)
            {
                if (localPlayers[i] != null)
                {
                    localPlayers[i].ApplyTelemetry(channel, rest);
                }
            }
        }
    }
}
