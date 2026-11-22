using CatSplat.SfxForge;

namespace SfxShowcase.Audio
{
    /// <summary>
    /// One entry of the <see cref="SoundLibrary"/>: a sound effect's patch and how the game plays it.
    /// </summary>
    public readonly struct SoundDefinition
    {
        /// <summary>How long a baked clip holds a held patch when nothing else is said, seconds.</summary>
        public const float DefaultHoldSeconds = 1.5f;

        /// <summary>Which sound effect this is.</summary>
        public readonly GameSound Id;

        /// <summary>The SFX FORGE patch: the whole sound, 28 bytes.</summary>
        public readonly SfxPatch Patch;

        /// <summary>Baked to a clip, live from the voice pool, or live and held.</summary>
        public readonly SoundRoute Route;

        /// <summary>
        /// The MIDI note it is played as, so it is in tune with the song; 0 = at the patch's own frequency (noises).
        /// </summary>
        public readonly int MidiNote;

        /// <summary>Its place in the mix, 0..1.</summary>
        public readonly float Volume;

        /// <summary>For a held patch: how long a baked clip would hold it. Only stored in the preset asset.</summary>
        public readonly float HoldSeconds;

        /// <summary>The name of the sound's preset asset.</summary>
        public string Name => Id.ToString();

        /// <summary>Creates a library entry.</summary>
        public SoundDefinition(
            GameSound id,
            SfxPatch patch,
            SoundRoute route,
            int midiNote,
            float volume,
            float holdSeconds = DefaultHoldSeconds
        )
        {
            Id = id;
            Patch = patch;
            Route = route;
            MidiNote = midiNote;
            Volume = volume;
            HoldSeconds = holdSeconds;
        }
    }
}
