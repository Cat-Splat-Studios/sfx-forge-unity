// SPDX-License-Identifier: MIT
//
// A sound as text ("SFXF1:" and 56 hex digits) and as a .syx file: SfxPatchText writes and reads both,
// SfxTextResult says what reading came to.
using System.Text;

namespace CatSplat.SfxForge
{
    /// <summary>What reading a patch's text or a .syx file came to.</summary>
    public enum SfxTextResult
    {
        Ok,
        Empty,
        NotOurs,
        NewerText,
        BadLength,
        BadCharacter,
        NewerPatch,
        BadPatch,
        NoPatchInFile,
        TooLarge,
    }

    /// <summary>
    /// A sound as text - "SFXF1:" and its 28 bytes as 56 hex digits, 62 characters to paste into a chat, a commit, a
    /// comment - and as a .syx file: the load message the link sends, which any SysEx tool can send to the device.
    /// The 1 is this text form's number; the patch's own format is its first byte.
    /// </summary>
    public static class SfxPatchText
    {
        /// <summary>What a sound's text begins with.</summary>
        public const string Prefix = "SFXF1:";

        /// <summary>How long a sound's text is: the prefix and two hex digits a byte, 62 characters.</summary>
        public const int Length = 6 + 2 * SfxPatch.Bytes;

        /// <summary>The largest file ParseSyx looks at.</summary>
        public const int MaxFile = 65536;
        const string Hex = "0123456789ABCDEF";

        /// <summary>The patch as text: "SFXF1:" and its 28 bytes as 56 hex digits.</summary>
        public static string ToText(SfxPatch patch)
        {
            byte[] b = patch.Pack();
            var s = new StringBuilder(Length);
            s.Append(Prefix);
            foreach (byte x in b)
            {
                s.Append(Hex[x >> 4]);
                s.Append(Hex[x & 15]);
            }

            return s.ToString();
        }

        static int Digit(char c) =>
            c >= '0' && c <= '9' ? c - '0'
            : c >= 'A' && c <= 'F' ? c - 'A' + 10
            : c >= 'a' && c <= 'f' ? c - 'a' + 10
            : -1;

        // what a text picks up on its way through a chat or a document: spaces and line breaks; characters that cannot
        // be seen (zero-width ones, the soft hyphen, the word joiner, marks of writing direction); quotes, brackets and
        // the marks of bold and code around it
        static bool Dropped(char c) =>
            char.IsWhiteSpace(c)
            || (c >= '\u200B' && c <= '\u200F')
            || (c >= '\u202A' && c <= '\u202E')
            || (c >= '\u2060' && c <= '\u2069')
            || c == '\uFEFF'
            || c == '\u00AD'
            || c == '`'
            || c == '"'
            || c == '\''
            || c == '<'
            || c == '>'
            || c == '('
            || c == ')'
            || c == '['
            || c == ']'
            || c == '{'
            || c == '}'
            || c == '*'
            || c == '_'
            || (c >= '\u2018' && c <= '\u201F')
            || c == '\u00AB'
            || c == '\u00BB'
            || c == '\u2039'
            || c == '\u203A';

        static bool EndsASentence(char c) => c == '.' || c == ',' || c == ';' || c == ':' || c == '!' || c == '?';

        /// <summary>
        /// Reads a text. The patch may stand in a sentence: what is in front of "SFXF1:" is passed over, and so is what
        /// follows its 56 digits unless that is another hex digit with no space in front of it. Spaces, line breaks,
        /// quotes, brackets and characters that cannot be seen are read through wherever they stand. Anything but Ok:
        /// patch is left empty, and Message says why.
        /// </summary>
        public static SfxTextResult Parse(string text, out SfxPatch patch)
        {
            patch = default;
            if (text == null)
            {
                return SfxTextResult.Empty;
            }

            // the text without what is read through, and for every character kept: did a space stand in front of it
            var clean = new StringBuilder(text.Length);

            // (a space stood in front of this character)
            var gap = new System.Collections.Generic.List<bool>(text.Length);
            bool space = false;
            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c))
                {
                    space = true;
                }

                if (!Dropped(c))
                {
                    clean.Append(c);
                    gap.Add(space);
                    space = false;
                }
            }

            // what ends a sentence behind it is the sentence's
            while (clean.Length > 0 && EndsASentence(clean[clean.Length - 1]))
            {
                clean.Length--;
            }

            if (clean.Length == 0)
            {
                return SfxTextResult.Empty;
            }

            string t = clean.ToString();

            // "SFXF", a number, a colon - the first place that reads so (a label in front may itself say "sfx forge")
            int start = -1;
            int colon = 0;
            bool newer = false;
            for (
                int at = t.IndexOf("SFXF", System.StringComparison.OrdinalIgnoreCase);
                at >= 0;
                at = at + 1 < t.Length ? t.IndexOf("SFXF", at + 1, System.StringComparison.OrdinalIgnoreCase) : -1
            )
            {
                colon = at + 4;
                while (colon < t.Length && t[colon] >= '0' && t[colon] <= '9')
                {
                    colon++;
                }

                if (colon == at + 4 || colon >= t.Length || t[colon] != ':')
                {
                    continue;
                }

                if (colon != at + 5 || t[at + 4] != '1')
                {
                    newer = true;
                    continue;
                }

                start = at;
                break;
            }

            if (start < 0)
            {
                return newer ? SfxTextResult.NewerText : SfxTextResult.NotOurs;
            }

            // the digits: 56 of them
            const int Digits = 2 * SfxPatch.Bytes;
            string digits = t.Substring(colon + 1);
            if (digits.Length > Digits)
            {
                // (more digits than a patch has; after a space it is the next word: "for", "and")
                if (Digit(digits[Digits]) >= 0 && !gap[colon + 1 + Digits])
                {
                    return SfxTextResult.BadLength;
                }

                digits = digits.Substring(0, Digits); // (the sentence goes on)
            }

            if (digits.Length < Digits)
            {
                foreach (char c in digits)
                {
                    if (Digit(c) < 0)
                    {
                        return SfxTextResult.BadCharacter;
                    }
                }

                return SfxTextResult.BadLength;
            }

            // the bytes, and the patch they are
            var b = new byte[SfxPatch.Bytes];
            for (int i = 0; i < b.Length; i++)
            {
                int hi = Digit(digits[i * 2]);
                int lo = Digit(digits[i * 2 + 1]);
                if (hi < 0 || lo < 0)
                {
                    return SfxTextResult.BadCharacter;
                }

                b[i] = (byte)(hi * 16 + lo);
            }

            if (SfxPatch.IsNewer(b, 0))
            {
                return SfxTextResult.NewerPatch;
            }

            return SfxPatch.TryUnpack(b, 0, out patch) ? SfxTextResult.Ok : SfxTextResult.BadPatch;
        }

        /// <summary>True when Parse reads the text as a patch (SfxTextResult.Ok).</summary>
        public static bool TryParse(string text, out SfxPatch patch) => Parse(text, out patch) == SfxTextResult.Ok;

        /// <summary>Why a text or a file was refused, in words. Null for Ok.</summary>
        public static string Message(SfxTextResult result)
        {
            switch (result)
            {
                case SfxTextResult.Empty:
                    return "nothing to paste";
                case SfxTextResult.NotOurs:
                    return "not an SFX Forge patch: there is no SFXF1: in it";
                case SfxTextResult.NewerText:
                    return "a patch text from a newer SFX Forge: update the package to read it";
                case SfxTextResult.BadLength:
                    return "cut off or too long: a patch is 56 hex digits after SFXF1:";
                case SfxTextResult.BadCharacter:
                    return "damaged: a character that is no hex digit";
                case SfxTextResult.NewerPatch:
                    return "a patch in a newer format than this version reads: update the package";
                case SfxTextResult.BadPatch:
                    return "damaged: the checksum does not fit";
                case SfxTextResult.NoPatchInFile:
                    return "no SFX Forge patch in this file";
                case SfxTextResult.TooLarge:
                    return "too large to be a patch file";
                default:
                    return null;
            }
        }

        /// <summary>
        /// The patch as a .syx file's bytes: the load message for a pad (SfxProtocol.BuildLoad), 35 bytes.
        /// </summary>
        public static byte[] ToSyx(SfxPatch patch, int pad = 0) =>
            SfxProtocol.BuildLoad(
                pad < 0 ? 0
                    : pad >= SfxProtocol.Slots ? SfxProtocol.Slots - 1
                    : pad,
                patch
            );

        /// <summary>
        /// Reads a .syx file: one or more SysEx messages. The first that is a patch is taken - a dump (the device's
        /// EXPORT), a load, or the load of a song's sound (pad is then that sound, 0..15). Realtime bytes (F8..FF)
        /// inside a message are passed over, as on a MIDI cable. A patch message of ours that does not read - its
        /// checksum does not fit - and no good one after it: BadPatch. A newer patch and no readable one: NewerPatch.
        /// Nothing of ours: NoPatchInFile.
        /// </summary>
        public static SfxTextResult ParseSyx(byte[] file, out SfxPatch patch, out int pad)
        {
            patch = default;
            pad = 0;
            if (file == null || file.Length == 0)
            {
                return SfxTextResult.NoPatchInFile;
            }

            if (file.Length > MaxFile)
            {
                return SfxTextResult.TooLarge;
            }

            bool newer = false;
            bool damaged = false;
            int at = 0;
            while (at < file.Length)
            {
                // the next message: from F0 to F7, without the realtime bytes inside it
                if (file[at] != 0xF0)
                {
                    at++;
                    continue;
                }

                int end = at + 1;
                int realtime = 0;
                while (end < file.Length && file[end] != 0xF7 && file[end] != 0xF0)
                {
                    if (file[end] >= 0xF8)
                    {
                        realtime++;
                    }

                    end++;
                }

                // (a message without its end)
                if (end >= file.Length || file[end] != 0xF7)
                {
                    at = end;
                    continue;
                }

                var m = new byte[end - at + 1 - realtime];
                for (int i = at, k = 0; i <= end; i++)
                {
                    if (file[i] < 0xF8)
                    {
                        m[k++] = file[i];
                    }
                }

                at = end + 1;

                // a patch for a pad, or for a song's sound: taken
                if (SfxProtocol.TryParsePatch(m, out pad, out patch))
                {
                    return SfxTextResult.Ok;
                }

                bool ours = SfxProtocol.IsOurs(m) && (m[4] == SfxProtocol.Dump || m[4] == SfxProtocol.Load);
                if (
                    ours
                    && m.Length == 7 + SfxPatch.Bytes
                    && m[4] == SfxProtocol.Load
                    && m[5] >= SfxProtocol.Slots
                    && m[5] < 2 * SfxProtocol.Slots
                    && SfxPatch.TryUnpack(m, 6, out patch)
                )
                {
                    pad = m[5] - SfxProtocol.Slots;
                    return SfxTextResult.Ok;
                }

                // a patch message of ours that was not taken (newer, or damaged): remembered, should no good one follow
                if (ours && SfxProtocol.NewerIn(m) != null)
                {
                    newer = true;
                }
                else if (ours && m.Length == 7 + SfxPatch.Bytes && m[5] < 2 * SfxProtocol.Slots)
                {
                    damaged = true;
                }
            }

            patch = default;
            pad = 0;
            return newer ? SfxTextResult.NewerPatch
                : damaged ? SfxTextResult.BadPatch
                : SfxTextResult.NoPatchInFile;
        }
    }
}
