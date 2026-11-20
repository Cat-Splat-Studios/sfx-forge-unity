namespace SfxShowcase.Audio
{
    /// <summary>
    /// The sixteen instruments of the song (the showcase's Song/DemoSong.asset) by index – what
    /// <c>SfxMusicPlayer.PlaySound</c>, <c>SetTrackVolume</c> and <c>SetTrackMute</c> take as "sound".
    /// </summary>
    public static class MusicKit
    {
        /// <summary>Timpani.</summary>
        public const int Timpani = 0;
        /// <summary>Snare drum.</summary>
        public const int Snare = 1;
        /// <summary>Kick drum: the only instrument that plays at intensity 0.</summary>
        public const int Kick = 2;
        /// <summary>Cowbell.</summary>
        public const int Cowbell = 3;
        /// <summary>Pizzicato strings.</summary>
        public const int Pizzicato = 4;
        /// <summary>Brass section (a held sound: release it).</summary>
        public const int Brass = 5;
        /// <summary>FM bass.</summary>
        public const int FmBass = 6;
        /// <summary>FM keys.</summary>
        public const int FmKeys = 7;
        /// <summary>Harp.</summary>
        public const int Harp = 8;
        /// <summary>Celesta.</summary>
        public const int Celesta = 9;
        /// <summary>Organ (a held sound).</summary>
        public const int Organ = 10;
        /// <summary>Wavetable sweep (a held sound).</summary>
        public const int WavetableSweep = 11;
        /// <summary>Closed hi-hat.</summary>
        public const int HiHat = 12;
        /// <summary>Bells.</summary>
        public const int Bells = 13;
        /// <summary>Choir (a held sound).</summary>
        public const int Choir = 14;
        /// <summary>String section (a held sound: release it).</summary>
        public const int Strings = 15;
    }
}
