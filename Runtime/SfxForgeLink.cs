// SPDX-License-Identifier: MIT
using System;
using UnityEngine;
using UnityEngine.Events;

namespace CatSplat.SfxForge
{
    /// <summary>
    /// The link to the device while the game runs: put one in the scene. It opens the MIDI ports (those chosen in
    /// Window > SFX Forge, unless named here), hands out what the device sends, and is what the other components send through.
    /// </summary>
    [AddComponentMenu("SFX Forge/SFX Forge Link")]
    [DefaultExecutionOrder(-100)]
    public class SfxForgeLink : MonoBehaviour
    {
        public const string InputPref = "SfxForge.Input", OutputPref = "SfxForge.Output";

        [Tooltip("Part of the MIDI input's name. Empty: the port chosen in Window > SFX Forge.")]
        public string inputPort = "";
        [Tooltip("Part of the MIDI output's name. Empty: the port chosen in Window > SFX Forge.")]
        public string outputPort = "";
        [Tooltip("Shown on the device's top line when the game starts (24 characters).")]
        public string textOnStart = "";

        [Serializable] public class PadEvent : UnityEvent<int, float> { }
        [Serializable] public class EncoderEvent : UnityEvent<int, float> { }

        [Tooltip("A pad was played on the device: pad 0..15, velocity 0..1.")]
        public PadEvent onPad = new PadEvent();
        [Tooltip("An encoder turned on the device's LINK page: encoder 0..5, value 0..1.")]
        public EncoderEvent onEncoder = new EncoderEvent();

        bool openedHere;

        public static SfxLink Link => SfxLink.Shared;

        void OnEnable()
        {
            var link = SfxLink.Shared;
            if (!link.IsOpen)
            {
                string i = string.IsNullOrEmpty(inputPort) ? PlayerPrefs.GetString(InputPref, "") : inputPort;
                string o = string.IsNullOrEmpty(outputPort) ? PlayerPrefs.GetString(OutputPref, "") : outputPort;
                if (i.Length + o.Length > 0)
                {
                    if (!link.Open(i, o)) Debug.LogWarning($"SFX Forge: could not open every MIDI port (in '{i}', out '{o}'). Choose them in Window > SFX Forge.");
                    openedHere = link.IsOpen;
                }
            }
            link.PadDown += OnPad;
            link.EncoderChanged += OnEncoder;
            if (!string.IsNullOrEmpty(textOnStart)) link.SendText(textOnStart);
        }

        void OnDisable()
        {
            var link = SfxLink.Shared;
            link.PadDown -= OnPad;
            link.EncoderChanged -= OnEncoder;
            if (openedHere) { link.Close(); openedHere = false; }
        }

        void Update() => SfxLink.Shared.Poll();

        void OnPad(int pad, float velocity) => onPad.Invoke(pad, velocity);
        void OnEncoder(int encoder, float value) => onEncoder.Invoke(encoder, value);

        /// <summary>Text for the device's top line: a level, a state.</summary>
        public void SendText(string text) => SfxLink.Shared.SendText(text);
    }
}
