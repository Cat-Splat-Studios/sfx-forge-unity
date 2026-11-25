// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;

namespace CatSplat.SfxForge
{
    /// <summary>Writes samples as a .wav file: 16 bits, any rate, one or two channels.</summary>
    public static class SfxWav
    {
        /// <summary>Samples (-1..1; with two channels: L R L R …) as a 16-bit WAV file.</summary>
        public static void Write(string path, float[] samples, int sampleRate, int channels = 1)
        {
            using (var w = new BinaryWriter(File.Create(path)))
            {
                int bytes = samples.Length * 2;
                w.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
                w.Write(36 + bytes);
                w.Write(
                    new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E', (byte)'f', (byte)'m', (byte)'t', (byte)' ' }
                );
                w.Write(16);
                w.Write((short)1);
                w.Write((short)channels);
                w.Write(sampleRate);
                w.Write(sampleRate * 2 * channels);
                w.Write((short)(2 * channels));
                w.Write((short)16);
                w.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
                w.Write(bytes);
                foreach (float s in samples)
                {
                    w.Write((short)Math.Round(Math.Max(-1f, Math.Min(1f, s)) * 32767f));
                }
            }
        }
    }
}
