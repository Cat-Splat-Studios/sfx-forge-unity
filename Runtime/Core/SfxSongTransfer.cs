// SPDX-License-Identifier: MIT
using System;

namespace CatSplat.SfxForge
{
    /// <summary>
    /// A song arriving piece by piece (the SysEx messages F0 7D 56 42 05 ...): give it every message with Take, and Song
    /// is the packed song once all its pieces are here. Both shapes of the message are taken:
    /// <para>firmware 1.6.0 on: <c>05 &lt;piece lo&gt; &lt;piece hi&gt; &lt;format&gt; &lt;pieces lo&gt; &lt;pieces hi&gt; &lt;1..240 bytes&gt;</c> - a
    /// song of any length in up to 160 pieces, every piece but the last 240 bytes;</para>
    /// <para>before: <c>05 &lt;piece&gt; 39 &lt;200 bytes, the last piece 99&gt;</c> - a format-5 song of 7699 bytes.</para>
    /// The pieces may come in any order (a browser's Web MIDI output can swap two neighbours) and more than once (the
    /// emulator delivers everything twice): a piece that is here already, byte for byte, is left alone. A piece that is
    /// here already with OTHER bytes belongs to another song and begins a new transfer - as does a piece that names
    /// another count of pieces or another format, or has the other shape. A piece of a format above this version's
    /// (SfxProtocol.SongVersion) is not collected at all: SfxProtocol.NewerIn says what it is.
    /// <para>After a song was whole its pieces stay here, until Reset or a new transfer, so that its late twins do not
    /// begin one. What comes then is such a twin - or a piece of another song of as many pieces, which may differ from
    /// the one before in a late piece only. So the pieces are counted again: once every piece has come again and one
    /// of them had other bytes, that is the next song, and it is whole. (A twin is told by a count: the same bytes in a
    /// piece that has come fewer times than the others.) The firmware's receiver keeps the same rule.</para>
    /// </summary>
    public sealed class SfxSongTransfer
    {
        byte[][] pieces;                 // the pieces that are here (null: not yet)
        int count, size, format, have;   // of the transfer that is collecting: its pieces, their size (200 or 240), the format they name (0: the old shape), how many are here
        byte[] song;
        int[] came, cameAgain;           // how often each piece came; and again, since the song was whole
        bool[] again;                    // since the song was whole: the pieces that came again
        int most, againHave;             // the most a piece of the whole song came; how many came again
        bool whole, other;               // the song was whole; since then a piece came with other bytes

        /// <summary>The whole song, its own length. Null until all its pieces are here, and again once a piece of another song came.</summary>
        public byte[] Song => song;

        /// <summary>Forgets what was collected: before asking for a song again, or after a transfer broke off.</summary>
        public void Reset() { pieces = null; came = cameAgain = null; again = null; count = size = format = have = most = againHave = 0; whole = other = false; song = null; }

        /// <summary>One message. True: it was a piece, and it made the song whole (Song has it).</summary>
        public bool Take(byte[] m) => TakePiece(m) == Whole;

        internal const int NotAPiece = 0, Twin = 1, Taken = 2, Whole = 3;

        /// <summary>The message is a piece of a song this class collects: either shape, a count and a length that can be, a format up to this version's.</summary>
        public static bool IsPiece(byte[] m) => Read(m, out _, out _, out _, out _, out _, out _);

        static bool Read(byte[] m, out int k, out int K, out int pieceSize, out int n, out int at, out int f)
        {
            k = K = pieceSize = n = at = f = 0;
            if (!SfxProtocol.IsOurs(m) || m[4] != SfxProtocol.SongChunk || m.Length < 9) return false;
            if (m[6] == SfxProtocol.OldSongPieces)                                   // the shape of before: its count of pieces stands where the new one has the piece number's high bits
            {
                k = m[5]; K = SfxProtocol.OldSongPieces; pieceSize = SfxProtocol.OldSongPieceBytes; n = m.Length - 8; at = 7; f = 0;
                return k < K && n == Math.Min(pieceSize, SfxProtocol.SongBytesV5 - k * pieceSize);
            }
            if (m[6] > 1 || m.Length < 12) return false;
            k = m[5] | m[6] << 7; K = m[8] | m[9] << 7; pieceSize = SfxProtocol.SongPieceBytes; n = m.Length - 11; at = 10; f = m[7];
            if (m[5] > 127 || m[8] > 127 || m[9] > 1 || K < 1 || K > SfxProtocol.SongPiecesMax || k >= K || n < 1 || n > pieceSize || (k < K - 1 && n != pieceSize)) return false;
            if (k == 0 && f <= SfxProtocol.SongVersion && m[at] != f) return false;      // (the format its pieces name is not the song's first byte)
            return f >= 1 && f <= SfxProtocol.SongVersion;                           // (a later format: nothing of it is collected)
        }

        // NotAPiece, Twin (here already, byte for byte: nothing new), Taken, Whole (taken, and the song is complete)
        internal int TakePiece(byte[] m)
        {
            if (!Read(m, out int k, out int K, out int pieceSize, out int n, out int at, out int f)) return NotAPiece;   // (checked before anything is allocated)
            if (pieces == null || K != count || pieceSize != size || f != format) Begin(K, pieceSize, f);
            byte[] here = pieces[k];
            bool same = here != null && here.Length == n;
            for (int i = 0; same && i < n; i++) same = here[i] == m[at + i];
            if (whole)                                                               // the song before was whole: its twin, or a piece of the next
            {
                if (same && !other && came[k] < most) { came[k]++; return Twin; }    // the twin this piece still owed
                if (!same)
                {
                    if (again[k]) ClearAgain();                                      // (a second piece with other bytes in the same place: yet another song begins)
                    pieces[k] = Piece(m, at, n); other = true; song = null;
                }
                if (!again[k]) { again[k] = true; againHave++; }
                cameAgain[k]++;
                if (againHave < count) return Taken;
                bool next = other;
                Array.Copy(cameAgain, came, count);
                Complete();
                if (!next) return Twin;                                              // every piece again and all the same: the same song, sent once more
                Join();
                return Whole;
            }
            if (here != null)
            {
                if (same) { came[k]++; return Twin; }
                Begin(K, pieceSize, f);                                              // other bytes: another song
            }
            pieces[k] = Piece(m, at, n); came[k] = 1; have++;
            if (have < count) return Taken;
            Complete(); Join();
            return Whole;
        }

        static byte[] Piece(byte[] m, int at, int n) { var piece = new byte[n]; Array.Copy(m, at, piece, 0, n); return piece; }

        void Join()
        {
            int length = 0, to = 0;
            for (int i = 0; i < count; i++) length += pieces[i].Length;
            song = new byte[length];
            for (int i = 0; i < count; i++) { pieces[i].CopyTo(song, to); to += pieces[i].Length; }
        }

        void Complete() { whole = true; most = 0; for (int i = 0; i < count; i++) most = Math.Max(most, came[i]); ClearAgain(); }
        void ClearAgain() { Array.Clear(again, 0, count); Array.Clear(cameAgain, 0, count); againHave = 0; other = false; }

        void Begin(int K, int pieceSize, int f)
        {
            pieces = new byte[K][]; came = new int[K]; cameAgain = new int[K]; again = new bool[K];
            count = K; size = pieceSize; format = f; have = most = againHave = 0; whole = other = false; song = null;
        }
    }
}
