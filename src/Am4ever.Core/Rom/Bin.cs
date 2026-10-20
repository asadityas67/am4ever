// SPDX-License-Identifier: GPL-2.0-or-later
using System.Buffers.Binary;

namespace Am4ever.Core.Rom;

/// <summary>Bounds-checked little-endian readers and writers over a byte buffer.</summary>
public static class Bin
{
    static void Need(byte[] b, int off, int len)
    {
        if (off < 0 || len < 0 || off + len > b.Length)
            throw new RomFormatException($"read of {len} byte(s) at {off:#0x} is outside the {b.Length}-byte buffer");
    }

    public static byte U8(byte[] b, int off) { Need(b, off, 1); return b[off]; }

    public static ushort U16(byte[] b, int off) { Need(b, off, 2); return BinaryPrimitives.ReadUInt16LittleEndian(b.AsSpan(off)); }

    public static uint U24(byte[] b, int off) { Need(b, off, 3); return (uint)(b[off] | (b[off + 1] << 8) | (b[off + 2] << 16)); }

    public static uint U32(byte[] b, int off) { Need(b, off, 4); return BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(off)); }

    public static ulong U64(byte[] b, int off) { Need(b, off, 8); return BinaryPrimitives.ReadUInt64LittleEndian(b.AsSpan(off)); }

    public static Guid Guid(byte[] b, int off) { Need(b, off, 16); return new Guid(b.AsSpan(off, 16)); }

    public static void W16(byte[] b, int off, ushort v) { Need(b, off, 2); BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(off), v); }

    public static void W24(byte[] b, int off, uint v)
    {
        Need(b, off, 3);
        b[off] = (byte)v; b[off + 1] = (byte)(v >> 8); b[off + 2] = (byte)(v >> 16);
    }

    public static void W32(byte[] b, int off, uint v) { Need(b, off, 4); BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(off), v); }

    public static void W64(byte[] b, int off, ulong v) { Need(b, off, 8); BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(off), v); }
}
