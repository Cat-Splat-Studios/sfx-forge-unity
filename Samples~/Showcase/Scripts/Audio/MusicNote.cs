using System;
using UnityEngine;

namespace SfxShowcase.Audio
{
    /// <summary>
    /// One note the game plays by hand on an instrument of the song with <c>SfxMusicPlayer.PlaySound</c> – a piece of a
    /// stinger or of the fanfare. Times are in sixteenth notes at the song's tempo, so a phrase always fits the music.
    /// </summary>
    [Serializable]
    public struct MusicNote
    {
        [Tooltip("The instrument: its index in the song's 16 sounds (see MusicKit).")]
        [Range(0, 15)]
        [SerializeField]
        int sound;

        [Tooltip("The MIDI note (64 = E above middle C).")]
        [Range(1, 127)]
        [SerializeField]
        int midiNote;

        [Tooltip("When it starts, in sixteenth notes after the phrase begins.")]
        [Min(0f)]
        [SerializeField]
        float startSixteenths;

        [Tooltip(
            "How long it is held, in sixteenth notes. 0 for a one-shot instrument (bells, drums); a held "
                + "instrument (brass, strings) must have a length or it never stops."
        )]
        [Min(0f)]
        [SerializeField]
        float lengthSixteenths;

        [Tooltip("How hard it is played.")]
        [Range(0f, 1f)]
        [SerializeField]
        float velocity;

        /// <summary>The instrument: its index in the song's 16 sounds.</summary>
        public int Sound => sound;

        /// <summary>The MIDI note.</summary>
        public int MidiNote => midiNote;

        /// <summary>When it starts, in sixteenth notes after the phrase begins.</summary>
        public float StartSixteenths => startSixteenths;

        /// <summary>How long it is held, in sixteenth notes; 0 = a one-shot that is never released.</summary>
        public float LengthSixteenths => lengthSixteenths;

        /// <summary>How hard it is played, 0..1.</summary>
        public float Velocity => velocity;

        /// <summary>Creates a note of a phrase.</summary>
        public MusicNote(int sound, int midiNote, float startSixteenths, float lengthSixteenths, float velocity)
        {
            this.sound = sound;
            this.midiNote = midiNote;
            this.startSixteenths = startSixteenths;
            this.lengthSixteenths = lengthSixteenths;
            this.velocity = velocity;
        }
    }
}
