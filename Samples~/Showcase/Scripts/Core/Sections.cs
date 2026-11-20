using System.Collections.Generic;

namespace SfxShowcase.Core
{
    /// <summary>
    /// The level's sections as data, in order (DESIGN.md section 3). The level builds its section triggers from this, the
    /// HUD takes the names from it, and the audio keeps its own table of what the music does in each
    /// <see cref="SectionId"/>.
    /// </summary>
    public static class Sections
    {
        static readonly SectionInfo[] Table =
        {
            new SectionInfo(SectionId.SoftStart, "SOFT START", 0f, 90f),
            new SectionInfo(SectionId.RollingSines, "ROLLING SINES", 90f, 220f),
            new SectionInfo(SectionId.FullCycle, "FULL CYCLE", 220f, 340f),
            new SectionInfo(SectionId.WooferSteps, "WOOFER STEPS", 340f, 460f),
            new SectionInfo(SectionId.StaticField, "STATIC FIELD", 460f, 600f),
            new SectionInfo(SectionId.SplitSignal, "SPLIT SIGNAL", 600f, 800f),
            new SectionInfo(SectionId.Overdrive, "OVERDRIVE", 800f, 960f),
            new SectionInfo(SectionId.TheResonator, "THE RESONATOR", 960f, 1000f),
        };

        /// <summary>Every section, indexed by <see cref="SectionId"/>.</summary>
        public static IReadOnlyList<SectionInfo> All => Table;

        /// <summary>World x where the level begins.</summary>
        public static float LevelStartX => Table[0].StartX;

        /// <summary>World x where the level ends.</summary>
        public static float LevelEndX => Table[Table.Length - 1].EndX;

        /// <summary>The description of one section.</summary>
        public static SectionInfo Get(SectionId id) => Table[(int)id];

        /// <summary>The section a world x lies in; positions outside the level give the first or the last section.</summary>
        public static SectionId At(float x)
        {
            for (int i = Table.Length - 1; i > 0; i--)
                if (x >= Table[i].StartX) return Table[i].Id;
            return Table[0].Id;
        }
    }
}
