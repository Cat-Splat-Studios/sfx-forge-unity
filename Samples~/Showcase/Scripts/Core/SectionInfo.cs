namespace SfxShowcase.Core
{
    /// <summary>
    /// One section of the level: its id, the name the HUD shows, and where it starts and ends along x (metres).
    /// </summary>
    public readonly struct SectionInfo
    {
        /// <summary>Which section this is.</summary>
        public readonly SectionId Id;

        /// <summary>The name shown on the HUD, already in upper case.</summary>
        public readonly string DisplayName;

        /// <summary>World x where the section begins.</summary>
        public readonly float StartX;

        /// <summary>World x where the section ends (the next one begins).</summary>
        public readonly float EndX;

        /// <summary>Creates the description of a section.</summary>
        public SectionInfo(SectionId id, string displayName, float startX, float endX)
        {
            Id = id;
            DisplayName = displayName;
            StartX = startX;
            EndX = endX;
        }

        /// <summary>Whether a world x lies in this section (the start included, the end not).</summary>
        public bool Contains(float x) => x >= StartX && x < EndX;
    }
}
