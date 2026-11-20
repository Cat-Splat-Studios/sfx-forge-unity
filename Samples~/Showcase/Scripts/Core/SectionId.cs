namespace SfxShowcase.Core
{
    /// <summary>The eight sections of the level First Harmonic, left to right. The value is the index into <see cref="Sections.All"/>.</summary>
    public enum SectionId
    {
        /// <summary>0 – 90 m: running and jumping.</summary>
        SoftStart = 0,
        /// <summary>90 – 220 m: the first slopes.</summary>
        RollingSines = 1,
        /// <summary>220 – 340 m: the first loop.</summary>
        FullCycle = 2,
        /// <summary>340 – 460 m: Woofers and terraces.</summary>
        WooferSteps = 3,
        /// <summary>460 – 600 m: the hazard stretch.</summary>
        StaticField = 4,
        /// <summary>600 – 800 m: the high route and the low route.</summary>
        SplitSignal = 5,
        /// <summary>800 – 960 m: the speed finale.</summary>
        Overdrive = 6,
        /// <summary>960 – 1000 m: the goal.</summary>
        TheResonator = 7,
    }
}
