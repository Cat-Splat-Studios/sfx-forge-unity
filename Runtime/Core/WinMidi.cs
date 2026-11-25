// SPDX-License-Identifier: GPL-3.0-or-later
// MIDI in and out on Windows (winmm), SysEx included. No dependency; other platforms report no ports.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace CatSplat.SfxForge
{
    /// <summary>The system's MIDI ports by name. Only Windows has them here: elsewhere the lists are empty.</summary>
    public static class MidiPorts
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || SFX_WINDOWS
        /// <summary>MIDI works on this platform (Windows).</summary>
        public static readonly bool Supported = true;

        /// <summary>The names of the MIDI inputs; a port's index in the list is what TryOpen takes.</summary>
        public static string[] Inputs()
        {
            var list = new List<string>();
            uint n = Mm.midiInGetNumDevs();
            for (uint i = 0; i < n; i++)
            {
                list.Add(
                    Mm.midiInGetDevCapsW((UIntPtr)i, out var c, (uint)Marshal.SizeOf(typeof(Mm.InCaps))) == 0
                        ? c.name
                        : "?"
                );
            }

            return list.ToArray();
        }

        /// <summary>The names of the MIDI outputs; a port's index in the list is what TryOpen takes.</summary>
        public static string[] Outputs()
        {
            var list = new List<string>();
            uint n = Mm.midiOutGetNumDevs();
            for (uint i = 0; i < n; i++)
            {
                list.Add(
                    Mm.midiOutGetDevCapsW((UIntPtr)i, out var c, (uint)Marshal.SizeOf(typeof(Mm.OutCaps))) == 0
                        ? c.name
                        : "?"
                );
            }

            return list.ToArray();
        }
#else
        /// <summary>MIDI works on this platform (Windows).</summary>
        public static readonly bool Supported = false;

        /// <summary>The names of the MIDI inputs; a port's index in the list is what TryOpen takes.</summary>
        public static string[] Inputs() => new string[0];

        /// <summary>The names of the MIDI outputs; a port's index in the list is what TryOpen takes.</summary>
        public static string[] Outputs() => new string[0];
#endif
    }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN || SFX_WINDOWS
    static class Mm
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct InCaps
        {
            public ushort mid;
            public ushort pid;
            public uint version;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string name;
            public uint support;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct OutCaps
        {
            public ushort mid;
            public ushort pid;
            public uint version;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string name;
            public ushort technology;
            public ushort voices;
            public ushort notes;
            public ushort channelMask;
            public uint support;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Hdr
        {
            public IntPtr data;
            public uint bufferLength;
            public uint bytesRecorded;
            public IntPtr user;
            public uint flags;
            public IntPtr next;
            public IntPtr reserved;
            public uint offset;
            public IntPtr r0;
            public IntPtr r1;
            public IntPtr r2;
            public IntPtr r3;
            public IntPtr r4;
            public IntPtr r5;
            public IntPtr r6;
            public IntPtr r7;
        }

        public const uint CallbackFunction = 0x30000;
        public const uint MimData = 0x3C3;
        public const uint MimLongData = 0x3C4;
        public const uint HdrDone = 1;
        public delegate void InProc(IntPtr handle, uint msg, IntPtr instance, IntPtr p1, IntPtr p2);

        const string W = "winmm.dll";

        [DllImport(W)]
        public static extern uint midiInGetNumDevs();

        [DllImport(W)]
        public static extern uint midiOutGetNumDevs();

        [DllImport(W, CharSet = CharSet.Unicode)]
        public static extern uint midiInGetDevCapsW(UIntPtr id, out InCaps caps, uint size);

        [DllImport(W, CharSet = CharSet.Unicode)]
        public static extern uint midiOutGetDevCapsW(UIntPtr id, out OutCaps caps, uint size);

        [DllImport(W)]
        public static extern uint midiInOpen(out IntPtr handle, uint id, InProc proc, IntPtr instance, uint flags);

        [DllImport(W)]
        public static extern uint midiInStart(IntPtr handle);

        [DllImport(W)]
        public static extern uint midiInStop(IntPtr handle);

        [DllImport(W)]
        public static extern uint midiInReset(IntPtr handle);

        [DllImport(W)]
        public static extern uint midiInClose(IntPtr handle);

        [DllImport(W)]
        public static extern uint midiInPrepareHeader(IntPtr handle, IntPtr hdr, uint size);

        [DllImport(W)]
        public static extern uint midiInUnprepareHeader(IntPtr handle, IntPtr hdr, uint size);

        [DllImport(W)]
        public static extern uint midiInAddBuffer(IntPtr handle, IntPtr hdr, uint size);

        [DllImport(W)]
        public static extern uint midiOutOpen(out IntPtr handle, uint id, IntPtr proc, IntPtr instance, uint flags);

        [DllImport(W)]
        public static extern uint midiOutClose(IntPtr handle);

        [DllImport(W)]
        public static extern uint midiOutReset(IntPtr handle);

        [DllImport(W)]
        public static extern uint midiOutShortMsg(IntPtr handle, uint msg);

        [DllImport(W)]
        public static extern uint midiOutPrepareHeader(IntPtr handle, IntPtr hdr, uint size);

        [DllImport(W)]
        public static extern uint midiOutUnprepareHeader(IntPtr handle, IntPtr hdr, uint size);

        [DllImport(W)]
        public static extern uint midiOutLongMsg(IntPtr handle, IntPtr hdr, uint size);

        public static readonly int HdrSize = Marshal.SizeOf(typeof(Hdr));
        public static readonly int OffBytesRecorded = (int)Marshal.OffsetOf(typeof(Hdr), "bytesRecorded");
        public static readonly int OffFlags = (int)Marshal.OffsetOf(typeof(Hdr), "flags");
    }

    /// <summary>
    /// One MIDI input. Messages arrive on a system thread and wait in a queue: take them with TryRead.
    /// </summary>
    public sealed class MidiIn : IDisposable
    {
        const int BufferCount = 4;
        const int BufferBytes = 1024;
        static readonly Mm.InProc Proc = OnMidi; // (kept alive: the system holds a pointer to it)
        static readonly ConcurrentDictionary<IntPtr, MidiIn> OpenPorts = new ConcurrentDictionary<IntPtr, MidiIn>();
        static int nextKey;

        readonly ConcurrentQueue<byte[]> queue = new ConcurrentQueue<byte[]>();
        readonly List<byte> sysex = new List<byte>();
        readonly IntPtr[] headers = new IntPtr[BufferCount];
        IntPtr handle;
        IntPtr key;
        volatile bool closing;

        /// <summary>The port's name.</summary>
        public string Name { get; private set; }

        MidiIn() { }

        /// <summary>
        /// Opens the input with this index in MidiPorts.Inputs(). Null: no such port, or it would not open.
        /// </summary>
        public static MidiIn TryOpen(int index)
        {
            var names = MidiPorts.Inputs();
            if (index < 0 || index >= names.Length)
            {
                return null;
            }

            var port = new MidiIn { Name = names[index], key = (IntPtr)Interlocked.Increment(ref nextKey) };
            OpenPorts[port.key] = port;
            if (Mm.midiInOpen(out port.handle, (uint)index, Proc, port.key, Mm.CallbackFunction) != 0)
            {
                OpenPorts.TryRemove(port.key, out _);
                return null;
            }

            for (int i = 0; i < BufferCount; i++)
            {
                IntPtr h = Marshal.AllocHGlobal(Mm.HdrSize + BufferBytes);
                Marshal.StructureToPtr(new Mm.Hdr { data = h + Mm.HdrSize, bufferLength = BufferBytes }, h, false);
                Mm.midiInPrepareHeader(port.handle, h, (uint)Mm.HdrSize);
                Mm.midiInAddBuffer(port.handle, h, (uint)Mm.HdrSize);
                port.headers[i] = h;
            }

            Mm.midiInStart(port.handle);
            return port;
        }

        /// <summary>The next message: three bytes or fewer, or a whole SysEx (F0 … F7).</summary>
        public bool TryRead(out byte[] message) => queue.TryDequeue(out message);

#if UNITY_5_3_OR_NEWER
        [AOT.MonoPInvokeCallback(typeof(Mm.InProc))]
#endif
        static void OnMidi(IntPtr handle, uint msg, IntPtr instance, IntPtr p1, IntPtr p2)
        {
            try
            {
                if (!OpenPorts.TryGetValue(instance, out var port))
                {
                    return;
                }

                if (msg == Mm.MimData)
                {
                    uint m = (uint)p1.ToInt64();
                    byte status = (byte)m;

                    // clock, active sensing: not ours
                    if (status >= 0xF8)
                    {
                        return;
                    }

                    int length = (status & 0xF0) == 0xC0 || (status & 0xF0) == 0xD0 ? 2 : 3;
                    var b = new byte[length];
                    for (int i = 0; i < length; i++)
                    {
                        b[i] = (byte)(m >> (8 * i));
                    }

                    port.queue.Enqueue(b);
                }
                else if (msg == Mm.MimLongData)
                {
                    int n = Marshal.ReadInt32(p1, Mm.OffBytesRecorded);
                    IntPtr data = Marshal.ReadIntPtr(p1);
                    for (int i = 0; i < n; i++)
                    {
                        byte v = Marshal.ReadByte(data, i);
                        if (v == 0xF0)
                        {
                            port.sysex.Clear();
                        }

                        port.sysex.Add(v);
                        if (v == 0xF7)
                        {
                            port.queue.Enqueue(port.sysex.ToArray());
                            port.sysex.Clear();
                        }
                        else if (port.sysex.Count > 8192)
                        {
                            // a SysEx that never ends: drop it (the longest of ours is 251 bytes)
                            port.sysex.Clear();
                        }
                    }

                    if (!port.closing)
                    {
                        Mm.midiInAddBuffer(handle, p1, (uint)Mm.HdrSize);
                    }
                }
            }
            catch
            {
                // never let an exception cross into the system's thread
            }
        }

        public void Dispose()
        {
            if (handle == IntPtr.Zero)
            {
                return;
            }

            closing = true;
            Mm.midiInStop(handle);
            Mm.midiInReset(handle); // hands every buffer back
            for (int i = 0; i < BufferCount; i++)
            {
                Mm.midiInUnprepareHeader(handle, headers[i], (uint)Mm.HdrSize);
                Marshal.FreeHGlobal(headers[i]);
            }

            Mm.midiInClose(handle);
            handle = IntPtr.Zero;
            OpenPorts.TryRemove(key, out _);
        }
    }

    /// <summary>One MIDI output.</summary>
    public sealed class MidiOut : IDisposable
    {
        IntPtr handle;

        /// <summary>The port's name.</summary>
        public string Name { get; private set; }

        MidiOut() { }

        /// <summary>
        /// Opens the output with this index in MidiPorts.Outputs(). Null: no such port, or it would not open.
        /// </summary>
        public static MidiOut TryOpen(int index)
        {
            var names = MidiPorts.Outputs();
            if (index < 0 || index >= names.Length)
            {
                return null;
            }

            var port = new MidiOut { Name = names[index] };
            return Mm.midiOutOpen(out port.handle, (uint)index, IntPtr.Zero, IntPtr.Zero, 0) == 0 ? port : null;
        }

        /// <summary>A short message (a note, a controller). False: the port did not take it.</summary>
        public bool Send(byte status, byte data1, byte data2) =>
            handle != IntPtr.Zero && Mm.midiOutShortMsg(handle, (uint)(status | data1 << 8 | data2 << 16)) == 0;

        /// <summary>A whole SysEx message, F0 … F7. Waits until the driver has taken it (a few milliseconds).</summary>
        public bool SendSysEx(byte[] message)
        {
            if (handle == IntPtr.Zero || message == null || message.Length < 2)
            {
                return false;
            }

            IntPtr h = Marshal.AllocHGlobal(Mm.HdrSize + message.Length);
            try
            {
                Marshal.StructureToPtr(
                    new Mm.Hdr
                    {
                        data = h + Mm.HdrSize,
                        bufferLength = (uint)message.Length,
                        bytesRecorded = (uint)message.Length,
                    },
                    h,
                    false
                );
                Marshal.Copy(message, 0, h + Mm.HdrSize, message.Length);
                if (Mm.midiOutPrepareHeader(handle, h, (uint)Mm.HdrSize) != 0)
                {
                    return false;
                }

                bool sent = Mm.midiOutLongMsg(handle, h, (uint)Mm.HdrSize) == 0;
                for (int i = 0; sent && i < 500 && (Marshal.ReadInt32(h, Mm.OffFlags) & Mm.HdrDone) == 0; i++)
                {
                    Thread.Sleep(1);
                }

                // (65 = still playing)
                for (int i = 0; i < 500 && Mm.midiOutUnprepareHeader(handle, h, (uint)Mm.HdrSize) != 0; i++)
                {
                    Thread.Sleep(1);
                }

                return sent;
            }
            finally
            {
                Marshal.FreeHGlobal(h);
            }
        }

        public void Dispose()
        {
            if (handle == IntPtr.Zero)
            {
                return;
            }

            Mm.midiOutReset(handle);
            Mm.midiOutClose(handle);
            handle = IntPtr.Zero;
        }
    }
#else
    // Not Windows: the same classes, with nothing behind them - no port opens, nothing arrives, nothing is sent.

    /// <summary>One MIDI input (a stand-in: MIDI is for Windows only).</summary>
    public sealed class MidiIn : IDisposable
    {
        /// <summary>The port's name.</summary>
        public string Name => "";

        /// <summary>
        /// Opens the input with this index in MidiPorts.Inputs(). Null: no such port, or it would not open.
        /// </summary>
        public static MidiIn TryOpen(int index) => null;

        /// <summary>Nothing ever arrives.</summary>
        public bool TryRead(out byte[] message)
        {
            message = null;
            return false;
        }

        public void Dispose() { }
    }

    /// <summary>One MIDI output.</summary>
    public sealed class MidiOut : IDisposable
    {
        /// <summary>The port's name.</summary>
        public string Name => "";

        /// <summary>
        /// Opens the output with this index in MidiPorts.Outputs(). Null: no such port, or it would not open.
        /// </summary>
        public static MidiOut TryOpen(int index) => null;

        /// <summary>A short message (a note, a controller). False: the port did not take it.</summary>
        public bool Send(byte status, byte data1, byte data2) => false;

        /// <summary>Nothing is sent.</summary>
        public bool SendSysEx(byte[] message) => false;

        public void Dispose() { }
    }
#endif
}
