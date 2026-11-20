namespace SfxShowcase.Audio
{
    /// <summary>
    /// The notes the game's sounds are tuned to. The song is in E minor, so every pitched effect is played as a note of
    /// E minor pentatonic (E G A B D) and sounds like part of the band. Values are MIDI note numbers (69 = A 440 Hz).
    /// </summary>
    public static class Pitch
    {
        /// <summary>E, 82 Hz.</summary>
        public const int E2 = 40;
        /// <summary>D, 147 Hz.</summary>
        public const int D3 = 50;
        /// <summary>E, 165 Hz.</summary>
        public const int E3 = 52;
        /// <summary>E, 330 Hz.</summary>
        public const int E4 = 64;
        /// <summary>G, 392 Hz.</summary>
        public const int G4 = 67;
        /// <summary>B, 494 Hz.</summary>
        public const int B4 = 71;
        /// <summary>E, 659 Hz.</summary>
        public const int E5 = 76;
        /// <summary>B, 988 Hz.</summary>
        public const int B5 = 83;
        /// <summary>E, 1319 Hz.</summary>
        public const int E6 = 88;

        const int SemitonesPerOctave = 12;
        static readonly int[] PentatonicSteps = { 0, 3, 5, 7, 10 };   // E G A B D, as semitones above E

        /// <summary>
        /// How many semitones above the root the given step of the minor pentatonic scale lies:
        /// 0 → 0 (E), 1 → 3 (G), 2 → 5 (A), 3 → 7 (B), 4 → 10 (D), 5 → 12 (E, an octave up) …
        /// </summary>
        public static int PentatonicSemitones(int step)
        {
            if (step < 0) step = 0;
            return step / PentatonicSteps.Length * SemitonesPerOctave + PentatonicSteps[step % PentatonicSteps.Length];
        }
    }
}
