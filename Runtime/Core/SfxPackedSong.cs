// SPDX-License-Identifier: MIT
namespace CatSplat.SfxForge
{
    /// <summary>
    /// Reads a packed song (core/sfx_song.h) of any format this version knows, step by step: what an Inspector shows, a
    /// tool measures or a test compares. Nothing here changes the bytes, and nothing is kept between two calls.
    /// <para>A pattern is a bar. Its grid cuts every sixteenth of the bar in 1, 2 or 4 steps: 16, 32 or 64 steps a full
    /// bar. The formats 1 to 5 know 16 only; format 6 (firmware 1.6.0) keeps a grid for each pattern, and only the rows
    /// that are used - so a packed song has no fixed size from then on.</para>
    /// <para>Arguments out of range give false, 0, 5 (the velocity nothing was kept for) or -1 (no point).</para>
    /// </summary>
    public static class SfxPackedSong
    {
        const int Slots = SfxProtocol.Slots, Patterns = SfxProtocol.Patterns, Lanes = SfxProtocol.Lanes, Bars = SfxProtocol.Bars, Sixteenths = SfxProtocol.Steps;
        const int Scales = 4, DelayTimes = 6;    // (SFX_SCALE_COUNT, SFX_DELAY_TIMES of core/sfx_song.h)

        /// <summary>
        /// 1..6 for a song this version reads, else 0. Formats 1 to 5 are known by their length and their first byte.
        /// Format 6: the first byte, a length the format can have, and eight pattern blocks that end exactly where the
        /// bytes end. (Whether every value inside is in range is another question: IsPlayable.)
        /// </summary>
        public static int Format(byte[] song)
        {
            if (song == null || song.Length < 7) return 0;
            int n = song.Length;
            switch (song[0])
            {
                case 1: return n == SfxProtocol.SongBytesV1 ? 1 : 0;
                case 2: return n == SfxProtocol.SongBytesV2 ? 2 : 0;
                case 3: return n == SfxProtocol.SongBytesV3 ? 3 : 0;
                case 4: return n == SfxProtocol.SongBytesV4 ? 4 : 0;
                case 5: return n == SfxProtocol.SongBytesV5 ? 5 : 0;
                case 6: return n >= SfxProtocol.SongBytesMin && n <= SfxProtocol.SongBytesMax && Block6(song, Patterns) == n ? 6 : 0;
            }
            return 0;
        }

        /// <summary>A song this version reads: Format is not 0.</summary>
        public static bool IsValid(byte[] song) => Format(song) != 0;

        /// <summary>
        /// A song the player and the device take: IsValid, and every value in it in range - what sfx_song_unpack
        /// (core/sfx_song.h) asks, in the same order: no byte over 127; a bar of 1..16 sixteenths, a tempo of 40..240, a
        /// swing up to 100, a scale and a chain there are; a delay time, an arrangement and lanes that can be; and in
        /// format 6 no spare bit in a block's masks, no step past the grid, velocities up to 7, lengths below the grid.
        /// IsValid looks at the layout only (it is asked for every step an Inspector draws); a song that is valid and not
        /// playable was damaged on its way: the device answers such a one with BAD SONG, SfxNative.PlayerSetSong with
        /// false, and SfxLink.Refusal says so before anything is sent.
        /// </summary>
        public static bool IsPlayable(byte[] song)
        {
            int f = Format(song);
            if (f == 0) return false;
            for (int i = 0; i < song.Length; i++) if (song[i] > 127) return false;
            int bpm = song[2] << 7 | song[3];
            if (song[1] < 1 || song[1] > Sixteenths || bpm < 40 || bpm > 240 || song[4] > 100 || song[5] >= Scales || song[6] < 1 || song[6] > Patterns) return false;
            if (f >= 2 && song[7 + Slots * 3 + 3] >= DelayTimes) return false;                  // (the master's first value: the echo's time)
            if (f >= 4)
            {
                int a = Arr(song);
                if (song[a] > Bars) return false;
                for (int i = 0; i < Bars; i++) if (song[a + 1 + i] >= Patterns) return false;
                if (f == 4) { for (int t = 0; t < Slots; t++) if (song[66 + t] >= SfxAutoTarget.Count) return false; }
                else for (int l = 0; l < Lanes; l++) if (song[a + 1 + Bars + l * 2] > Slots || song[a + 2 + Bars + l * 2] >= SfxAutoTarget.Count) return false;
            }
            if (f < 6) return true;
            int o = SfxProtocol.SongHead;
            for (int p = 0; p < Patterns; p++)
            {
                if (song[o + 3] > 3 || song[o + 7] > 7) return false;                           // (a spare bit in the masks)
                int g = Sixteenths << song[o], nb = (g + 6) / 7, rows = Bits(Rows6(song, o)), row = nb + 3 * g, r = o + 8;
                for (int k = 0; k < rows; k++, r += row)
                {
                    if (song[r + nb - 1] >> (g - (nb - 1) * 7) != 0) return false;              // a step bit from the grid's last step on
                    for (int i = 0; i < g; i++) if (song[r + nb + g + i] > 7 || song[r + nb + 2 * g + i] > g - 1) return false;
                }
                o += 8 + rows * row + Bits(Lanes6(song, o)) * g;
            }
            return true;
        }

        /// <summary>The steps a sixteenth of a pattern's grid: 1, 2, 4. 0: not a song, no such pattern.</summary>
        public static int Div(byte[] song, int pattern)
        {
            int f = Format(song);
            if (f == 0 || pattern < 0 || pattern >= Patterns) return 0;
            return f < 6 ? 1 : 1 << song[Block6(song, pattern)];
        }

        /// <summary>The steps a pattern has in a full bar: 16, 32, 64. 0: not a song, no such pattern.</summary>
        public static int Grid(byte[] song, int pattern) => Sixteenths * Div(song, pattern);

        /// <summary>The sound (0..15) plays on this step (0 .. Grid - 1, in the pattern's own grid) of this pattern (0..7).</summary>
        public static bool StepOn(byte[] song, int pattern, int sound, int step)
        {
            int at = Row(song, pattern, sound, step, out int f, out int g);
            if (at < 0) return false;
            if (f == 6) return (song[at + step / 7] >> (step % 7) & 1) != 0;
            return ((song[at] | song[at + 1] << 7 | (song[at + 2] & 3) << 14) >> step & 1) != 0;
        }

        /// <summary>The note of a step: 0 = the sound as designed (drums, effects), else a MIDI note.</summary>
        public static int StepNote(byte[] song, int pattern, int sound, int step)
        {
            int at = Row(song, pattern, sound, step, out int f, out int g);
            if (at < 0) return 0;
            return (f == 6 ? song[at + (g + 6) / 7 + step] : song[at + 3 + step]) & 127;
        }

        /// <summary>How hard a step is played: 0..7, soft .. full (5 = as it always was, and where nothing is kept).</summary>
        public static int StepVelocity(byte[] song, int pattern, int sound, int step)
        {
            int at = Row(song, pattern, sound, step, out int f, out int g);
            if (at < 0 || f < 3) return 5;
            return (f == 6 ? song[at + (g + 6) / 7 + g + step] : song[at + 3 + Sixteenths + step]) & 7;
        }

        /// <summary>How many steps of the pattern's grid a step holds: 0 = by itself (to the sound's end, its next note or the bar line).</summary>
        public static int StepLength(byte[] song, int pattern, int sound, int step)
        {
            int at = Row(song, pattern, sound, step, out int f, out int g);
            if (at < 0 || f < 3) return 0;
            return f == 6 ? song[at + (g + 6) / 7 + 2 * g + step] & 127 : song[at + 3 + Sixteenths + step] >> 3 & 15;
        }

        /// <summary>A lane's (0..23) point on a step of a pattern: -1 = none there (the point before holds), else the value 0..126.</summary>
        public static int LanePoint(byte[] song, int pattern, int lane, int step)
        {
            int f = Format(song);
            if (f == 0 || pattern < 0 || pattern >= Patterns || lane < 0 || lane >= Lanes || step < 0) return -1;
            if (f == 6)
            {
                int o = Block6(song, pattern), g = Sixteenths << song[o];
                int rows = Rows6(song, o), lanes = Lanes6(song, o);
                if (step >= g || (lanes >> lane & 1) == 0) return -1;
                return (song[o + 8 + Bits(rows) * ((g + 6) / 7 + 3 * g) + Bits(lanes & ((1 << lane) - 1)) * g + step] & 127) - 1;
            }
            if (step >= Sixteenths) return -1;
            if (f == 4) return lane < Slots ? song[At(song, pattern, lane) + 3 + 2 * Sixteenths + step] - 1 : -1;     // (format 4: one lane a sound, at the end of its row)
            if (f < 5) return -1;
            return song[Body(song) + Patterns * Slots * RowBytes(song) + (pattern * Lanes + lane) * Sixteenths + step] - 1;
        }

        // ---- formats 1 to 5: a row of 16 steps for every sound of every pattern - 3 bytes of steps, 16 notes, then (format 3 on)
        // 16 accents (velocity and length), then (format 4 only) its one lane. Format 5: the lanes' points after all the rows.
        static int Arr(byte[] d) => d[0] == 4 ? 66 + Slots : 66;                // (format 4 kept one lane target for each sound before the arrangement)
        static int Body(byte[] d) => d[0] >= 5 ? Arr(d) + 1 + Bars + Lanes * 2 : d[0] == 4 ? Arr(d) + 1 + Bars : d[0] >= 2 ? 66 : 7 + Slots;
        static int RowBytes(byte[] d) => d[0] == 4 ? 3 + 3 * Sixteenths : d[0] == 3 || d[0] >= 5 ? 3 + 2 * Sixteenths : 3 + Sixteenths;
        static int At(byte[] d, int pattern, int sound) => Body(d) + (pattern * Slots + sound) * RowBytes(d);

        // ---- format 6: the head of format 5 (147 bytes), then a block a pattern: its grid, the sounds with a row (16 bits in
        // three bytes), the lanes with a row (24 bits in four), then those rows. A sound's row: the steps that are on (7 bits a
        // byte), a note, a velocity and a length for every step. A lane's row: a point for every step.
        static int Rows6(byte[] d, int o) => d[o + 1] & 127 | (d[o + 2] & 127) << 7 | (d[o + 3] & 3) << 14;
        static int Lanes6(byte[] d, int o) => d[o + 4] & 127 | (d[o + 5] & 127) << 7 | (d[o + 6] & 127) << 14 | (d[o + 7] & 7) << 21;
        static int Bits(int v) { int n = 0; for (; v != 0; v &= v - 1) n++; return n; }

        // where block `pattern` begins (pattern 8: where the last one ends) - the sum of the blocks before it. -1: a block's
        // head lies outside the bytes, or names a grid there is none of.
        static int Block6(byte[] d, int pattern)
        {
            int o = SfxProtocol.SongHead;
            for (int p = 0; p < pattern; p++)
            {
                if (o + 8 > d.Length || d[o] > 2) return -1;
                int g = Sixteenths << d[o];
                o += 8 + Bits(Rows6(d, o)) * ((g + 6) / 7 + 3 * g) + Bits(Lanes6(d, o)) * g;
            }
            return pattern < Patterns && (o + 8 > d.Length || d[o] > 2) ? -1 : o;
        }

        // where a sound's row begins; -1: not a song, out of range, or (format 6) the sound has no row in that pattern.
        // f: the song's format, g: the pattern's steps
        static int Row(byte[] song, int pattern, int sound, int step, out int f, out int g)
        {
            f = Format(song); g = Sixteenths;
            if (f == 0 || pattern < 0 || pattern >= Patterns || sound < 0 || sound >= Slots || step < 0) return -1;
            if (f < 6) return step < Sixteenths ? At(song, pattern, sound) : -1;
            int o = Block6(song, pattern), rows = Rows6(song, o);
            g = Sixteenths << song[o];
            if (step >= g || (rows >> sound & 1) == 0) return -1;
            return o + 8 + Bits(rows & ((1 << sound) - 1)) * ((g + 6) / 7 + 3 * g);
        }
    }
}
