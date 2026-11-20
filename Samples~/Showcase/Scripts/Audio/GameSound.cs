namespace SfxShowcase.Audio
{
    /// <summary>
    /// Every sound effect of the game, by name. Each has one patch in <see cref="SoundLibrary"/>; the numbers in the
    /// comments are the rows of the audio table in DESIGN.md.
    /// </summary>
    public enum GameSound
    {
        /// <summary>1 – the player jumps.</summary>
        Jump,
        /// <summary>2 – the player lands.</summary>
        Land,
        /// <summary>3 – a step of the left foot.</summary>
        FootstepLeft,
        /// <summary>3 – a step of the right foot (a slightly higher tick).</summary>
        FootstepRight,
        /// <summary>4 – the player tucks into a roll.</summary>
        TuckStart,
        /// <summary>5 – the hum of rolling on the ground (held; follows speed).</summary>
        RollHum,
        /// <summary>6 – the rev of the Wind-Up (held; follows charge).</summary>
        WindUpRev,
        /// <summary>7 – the Wind-Up is released into a Burst.</summary>
        Burst,
        /// <summary>8 – a Chime is collected (pitch follows the combo).</summary>
        Chime,
        /// <summary>9 – one ping of the spill when carried Chimes scatter.</summary>
        ChimeScatter,
        /// <summary>10 – a Woofer fires.</summary>
        Woofer,
        /// <summary>11 – the player is hit.</summary>
        Hurt,
        /// <summary>12 – a Clipper is popped.</summary>
        ClipperPop,
        /// <summary>13 – a Tuning Fork is struck.</summary>
        TuningFork,
        /// <summary>14 – the player runs through the Resonator.</summary>
        Goal,
        /// <summary>15 – the wind at high speed (held; follows speed).</summary>
        Wind,
        /// <summary>16 – a hard brake on the ground.</summary>
        Skid,
        /// <summary>17 – the player reappears at a Tuning Fork.</summary>
        Respawn,
        /// <summary>18 – Jump on the title card or the results panel.</summary>
        UiConfirm,
    }
}
