// SPDX-License-Identifier: MIT
using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;

namespace CatSplat.SfxForge
{
    /// <summary>
    /// One of the device's encoders (LINK page: encoders 1-4, A, B) as a control in Unity: an exposed AudioMixer
    /// parameter, and/or anything else through the event. Works in Play mode; needs an SfxForgeLink in the scene.
    /// </summary>
    [AddComponentMenu("SFX Forge/SFX Encoder Binding")]
    public class SfxEncoderBinding : MonoBehaviour
    {
        public enum Encoder { Encoder1, Encoder2, Encoder3, Encoder4, A, B }

        public Encoder encoder = Encoder.Encoder1;
        [Tooltip("Optional: a mixer and the name of one of its exposed parameters.")]
        public AudioMixer mixer;
        public string exposedParameter = "";
        [Tooltip("The parameter's value with the encoder at 0 and at 127 (a volume: -80 and 0 dB).")]
        public float min = -80f, max = 0f;

        [Serializable] public class ValueEvent : UnityEvent<float> { }
        [Tooltip("The value between Min and Max, whenever the encoder turns.")]
        public ValueEvent onValue = new ValueEvent();

        void OnEnable() => SfxLink.Shared.EncoderChanged += OnEncoder;
        void OnDisable() => SfxLink.Shared.EncoderChanged -= OnEncoder;

        void OnEncoder(int index, float value01)
        {
            if (index != (int)encoder) return;
            float v = Mathf.Lerp(min, max, value01);
            if (mixer != null && !string.IsNullOrEmpty(exposedParameter)) mixer.SetFloat(exposedParameter, v);
            onValue.Invoke(v);
        }
    }
}
