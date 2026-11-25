// SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace CatSplat.SfxForge
{
    /// <summary>
    /// A song arriving piece by piece (the SysEx messages F0 7D 56 42 05 ...): give it every message with Take, and
    /// Song is the packed song once all its pieces are here. Both shapes of the message are taken:
    /// <para>firmware 1.6.0 on: <c>05 &lt;piece lo&gt; &lt;piece hi&gt; &lt;format&gt; &lt;pieces lo&gt;
    /// &lt;pieces hi&gt; &lt;1..240 bytes&gt;</c> - a song of any length in up to 160 pieces, every piece but the
    /// last 240 bytes;</para>
    /// <para>before: <c>05 &lt;piece&gt; 39 &lt;200 bytes, the last piece 99&gt;</c> - a format-5 song of
    /// 7699 bytes.</para>
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
        byte[][] pieces; // the pieces that are here (null: not yet)

        // of the transfer that is collecting: how many pieces it has, their size (200 or 240),
        // the format they name (0: the old shape), how many of them are here
        int count;
        int size;
        int format;
        int piecesHere;

        // the whole song (see Song)
        byte[] song;

        // how often each piece came; and again, since the song was whole
        int[] came;
        int[] cameAgain;
        bool[] again; // since the song was whole: the pieces that came again

        // the most a piece of the whole song came; how many pieces came again since it was whole
        int most;
        int piecesAgain;

        // the song was whole; since then a piece came with other bytes
        bool whole;
        bool otherBytesSeen;

        /// <summary>
        /// The whole song, its own length. Null until all its pieces are here, and again once a piece of
        /// another song came.
        /// </summary>
        public byte[] Song => song;

        /// <summary>
        /// Forgets what was collected: before asking for a song again, or after a transfer broke off.
        /// </summary>
        public void Reset()
        {
            pieces = null;
            came = null;
            cameAgain = null;
            again = null;
            count = 0;
            size = 0;
            format = 0;
            piecesHere = 0;
            most = 0;
            piecesAgain = 0;
            whole = false;
            otherBytesSeen = false;
            song = null;
        }

        /// <summary>One message. True: it was a piece, and it made the song whole (Song has it).</summary>
        public bool Take(byte[] m) => TakePiece(m) == Whole;

        internal const int NotAPiece = 0;
        internal const int Twin = 1;
        internal const int Taken = 2;
        internal const int Whole = 3;

        /// <summary>
        /// The message is a piece of a song this class collects: either shape, a count and a length that can be, a
        /// format up to this version's.
        /// </summary>
        public static bool IsPiece(byte[] m) => Read(m, out _, out _, out _, out _, out _, out _);

        // Reads one message. False: not a piece this class collects.
        // index: which piece this is. pieceCount: how many the song has. pieceSize: the bytes of a full piece.
        // byteCount: the song bytes in this piece, which begin at m[dataAt]. namedFormat: the format the piece names
        // (0: the old shape names none).
        static bool Read(
            byte[] m,
            out int index,
            out int pieceCount,
            out int pieceSize,
            out int byteCount,
            out int dataAt,
            out int namedFormat
        )
        {
            index = 0;
            pieceCount = 0;
            pieceSize = 0;
            byteCount = 0;
            dataAt = 0;
            namedFormat = 0;
            if (!SfxProtocol.IsOurs(m) || m[4] != SfxProtocol.SongChunk || m.Length < 9)
            {
                return false;
            }

            // the shape of before: its count of pieces stands where the new one has the piece number's high bits
            if (m[6] == SfxProtocol.OldSongPieces)
            {
                index = m[5];
                pieceCount = SfxProtocol.OldSongPieces;
                pieceSize = SfxProtocol.OldSongPieceBytes;
                byteCount = m.Length - 8;
                dataAt = 7;
                namedFormat = 0;
                return index < pieceCount
                    && byteCount == Math.Min(pieceSize, SfxProtocol.SongBytesV5 - index * pieceSize);
            }

            if (m[6] > 1 || m.Length < 12)
            {
                return false;
            }

            index = m[5] | m[6] << 7;
            pieceCount = m[8] | m[9] << 7;
            pieceSize = SfxProtocol.SongPieceBytes;
            byteCount = m.Length - 11;
            dataAt = 10;
            namedFormat = m[7];
            if (
                m[5] > 127
                || m[8] > 127
                || m[9] > 1
                || pieceCount < 1
                || pieceCount > SfxProtocol.SongPiecesMax
                || index >= pieceCount
                || byteCount < 1
                || byteCount > pieceSize
                || (index < pieceCount - 1 && byteCount != pieceSize)
            )
            {
                return false;
            }

            // (the format its pieces name is not the song's first byte)
            if (index == 0 && namedFormat <= SfxProtocol.SongVersion && m[dataAt] != namedFormat)
            {
                return false;
            }

            // (a later format: nothing of it is collected)
            return namedFormat >= 1 && namedFormat <= SfxProtocol.SongVersion;
        }

        // NotAPiece, Twin (here already, byte for byte: nothing new), Taken, Whole (taken, and the song is complete)
        internal int TakePiece(byte[] m)
        {
            // (checked before anything is allocated)
            if (
                !Read(
                    m,
                    out int index,
                    out int pieceCount,
                    out int pieceSize,
                    out int byteCount,
                    out int dataAt,
                    out int namedFormat
                )
            )
            {
                return NotAPiece;
            }

            if (pieces == null || pieceCount != count || pieceSize != size || namedFormat != format)
            {
                Begin(pieceCount, pieceSize, namedFormat);
            }

            byte[] here = pieces[index];
            bool same = here != null && here.Length == byteCount;
            for (int i = 0; same && i < byteCount; i++)
            {
                same = here[i] == m[dataAt + i];
            }

            // the song before was whole: its twin, or a piece of the next
            if (whole)
            {
                // the twin this piece still owed
                if (same && !otherBytesSeen && came[index] < most)
                {
                    came[index]++;
                    return Twin;
                }

                if (!same)
                {
                    // (a second piece with other bytes in the same place: yet another song begins)
                    if (again[index])
                    {
                        ClearAgain();
                    }

                    pieces[index] = Piece(m, dataAt, byteCount);
                    otherBytesSeen = true;
                    song = null;
                }

                if (!again[index])
                {
                    again[index] = true;
                    piecesAgain++;
                }

                cameAgain[index]++;
                if (piecesAgain < count)
                {
                    return Taken;
                }

                bool next = otherBytesSeen;
                Array.Copy(cameAgain, came, count);
                Complete();

                // every piece again and all the same: the same song, sent once more
                if (!next)
                {
                    return Twin;
                }

                Join();
                return Whole;
            }

            if (here != null)
            {
                if (same)
                {
                    came[index]++;
                    return Twin;
                }

                Begin(pieceCount, pieceSize, namedFormat); // other bytes: another song
            }

            pieces[index] = Piece(m, dataAt, byteCount);
            came[index] = 1;
            piecesHere++;
            if (piecesHere < count)
            {
                return Taken;
            }

            Complete();
            Join();
            return Whole;
        }

        static byte[] Piece(byte[] m, int dataAt, int byteCount)
        {
            var piece = new byte[byteCount];
            Array.Copy(m, dataAt, piece, 0, byteCount);
            return piece;
        }

        void Join()
        {
            int length = 0;
            int to = 0;
            for (int i = 0; i < count; i++)
            {
                length += pieces[i].Length;
            }

            song = new byte[length];
            for (int i = 0; i < count; i++)
            {
                pieces[i].CopyTo(song, to);
                to += pieces[i].Length;
            }
        }

        void Complete()
        {
            whole = true;
            most = 0;
            for (int i = 0; i < count; i++)
            {
                most = Math.Max(most, came[i]);
            }

            ClearAgain();
        }

        void ClearAgain()
        {
            Array.Clear(again, 0, count);
            Array.Clear(cameAgain, 0, count);
            piecesAgain = 0;
            otherBytesSeen = false;
        }

        void Begin(int pieceCount, int pieceSize, int namedFormat)
        {
            pieces = new byte[pieceCount][];
            came = new int[pieceCount];
            cameAgain = new int[pieceCount];
            again = new bool[pieceCount];
            count = pieceCount;
            size = pieceSize;
            format = namedFormat;
            piecesHere = 0;
            most = 0;
            piecesAgain = 0;
            whole = false;
            otherBytesSeen = false;
            song = null;
        }
    }
}
