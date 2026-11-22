using System;
using SfxShowcase.Core;
using UnityEngine;

namespace SfxShowcase.Audio
{
    /// <summary>
    /// What the music does in one section of the level: how full the band is at the least, and whether one pattern of
    /// the song is held there. The table of all sections is on the <see cref="MusicDirector"/> (DESIGN.md, "Music").
    /// </summary>
    [Serializable]
    public struct SectionCue
    {
        /// <summary>
        /// The <see cref="Pattern"/> value that leaves the music as it is: a pattern that is held stays held.
        /// </summary>
        public const int KeepPattern = -1;

        /// <summary>
        /// The <see cref="Pattern"/> value that plays the song in its own order: a hold ends and the
        /// arrangement walks on.
        /// </summary>
        public const int SongOrder = -2;

        [Tooltip("The section of the level.")]
        [SerializeField]
        SectionId section;

        [Tooltip(
            "The pattern held in this section: 0 = A … 7 = H. -2: none, the song plays in its own order. -1: "
                + "whatever is held stays held."
        )]
        [Range(-2, 7)]
        [SerializeField]
        int pattern;

        [Tooltip(
            "The music's intensity when the player stands still in the section; speed fills the rest of the way to 1."
        )]
        [Range(0f, 1f)]
        [SerializeField]
        float intensityFloor;

        /// <summary>The section of the level.</summary>
        public SectionId Section => section;

        /// <summary>
        /// The pattern held in this section, 0 = A; <see cref="SongOrder"/> or <see cref="KeepPattern"/> for none.
        /// </summary>
        public int Pattern => pattern;

        /// <summary>
        /// The music's intensity when the player stands still in the section; speed fills the rest of the way to 1.
        /// </summary>
        public float IntensityFloor => intensityFloor;

        /// <summary>Creates a row of the table.</summary>
        public SectionCue(SectionId section, int pattern, float intensityFloor)
        {
            this.section = section;
            this.pattern = pattern;
            this.intensityFloor = intensityFloor;
        }
    }
}
