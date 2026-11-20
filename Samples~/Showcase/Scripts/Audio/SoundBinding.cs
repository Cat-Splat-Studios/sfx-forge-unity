using System;
using CatSplat.SfxForge;
using UnityEngine;

namespace SfxShowcase.Audio
{
    /// <summary>
    /// One sound effect as the scene holds it: the <see cref="SoundDefinition"/> from the library with its patch turned
    /// into an <see cref="SfxPreset"/> asset. Visible and tunable in the Inspector on the <see cref="SoundEffects"/>
    /// component.
    /// </summary>
    [Serializable]
    public sealed class SoundBinding
    {
        [Tooltip("Which sound effect this is.")]
        [SerializeField] GameSound sound;

        [Tooltip("The SFX FORGE preset that is the sound. Edit its patch to retune the sound.")]
        [SerializeField] SfxPreset preset;

        [Tooltip("Baked: rendered to a clip at start. LiveOneShot: synthesized per trigger by the voice pool. LiveHeld: on an SfxPlayer of its own (see Continuous Sounds).")]
        [SerializeField] SoundRoute route;

        [Tooltip("The MIDI note it is played as, so it is in tune with the song. 0 = at the patch's own frequency.")]
        [SerializeField] int midiNote;

        [Tooltip("Its place in the mix.")]
        [Range(0f, 1f)] [SerializeField] float volume = 1f;

        /// <summary>Which sound effect this is.</summary>
        public GameSound Sound => sound;
        /// <summary>The SFX FORGE preset that is the sound.</summary>
        public SfxPreset Preset => preset;
        /// <summary>How it is played.</summary>
        public SoundRoute Route => route;
        /// <summary>The MIDI note it is played as; 0 = at the patch's own frequency.</summary>
        public int MidiNote => midiNote;
        /// <summary>Its place in the mix, 0..1.</summary>
        public float Volume => volume;

        /// <summary>For Unity's serializer, which fills the fields itself.</summary>
        SoundBinding() { }

        /// <summary>Binds a library entry to the preset made from its patch.</summary>
        public SoundBinding(SoundDefinition definition, SfxPreset soundPreset)
        {
            sound = definition.Id;
            preset = soundPreset;
            route = definition.Route;
            midiNote = definition.MidiNote;
            volume = definition.Volume;
        }
    }
}
