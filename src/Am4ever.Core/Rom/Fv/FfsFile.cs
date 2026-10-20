// SPDX-License-Identifier: GPL-2.0-or-later
using System.Text;

namespace Am4ever.Core.Rom.Fv;

/// <summary>A file inside a firmware volume (FFSv2).</summary>
public sealed class FfsFile
{
    const byte AttribLargeFile = 0x01;
    const byte AttribChecksum = 0x40;

    readonly byte[] _buffer;

    public FfsFile(byte[] buffer, int offset)
    {
        _buffer = buffer;
        Offset = offset;
        Guid = Bin.Guid(buffer, offset);
        Type = Bin.U8(buffer, offset + 18);
        Attributes = Bin.U8(buffer, offset + 19);
        if ((Attributes & AttribLargeFile) != 0)
        {
            Size = (long)Bin.U64(buffer, offset + 24);
            HeaderLength = 32;
        }
        else
        {
            Size = Bin.U24(buffer, offset + 20);
            HeaderLength = 24;
        }
        DataOffset = offset + HeaderLength;
        End = offset + (int)Size;
    }

    public int Offset { get; }
    public Guid Guid { get; }
    public byte Type { get; }
    public byte Attributes { get; }
    public long Size { get; }
    public int HeaderLength { get; }
    public int DataOffset { get; }
    public int End { get; }

    public IReadOnlyList<FvSection> Sections()
    {
        if (End > _buffer.Length)
            throw new RomFormatException($"FFS file at {Offset:#0x} ends past the buffer");
        return SectionWalker.Walk(_buffer, DataOffset, End);
    }

    /// <summary>The UI-section name, or "" if the file has none.</summary>
    public string Name()
    {
        foreach (var s in Sections())
            if (s.Type == SectionType.UserInterface)
                return Encoding.Unicode.GetString(_buffer, s.DataOffset, s.End - s.DataOffset).TrimEnd('\0');
        return "";
    }

    /// <summary>The (start, end) of the file's PE32 or TE image section.</summary>
    public (int Start, int End) ImageSection()
    {
        foreach (var s in Sections())
            if (s.Type is SectionType.Pe32 or SectionType.Te)
                return (s.DataOffset, s.End);
        throw new RomFormatException($"FFS file {Guid} has no PE32/TE image section");
    }

    /// <summary>Recompute the header checksum and, when enabled, the data checksum.</summary>
    public void FixChecksums(byte[] buffer)
    {
        int o = Offset;
        buffer[o + 16] = 0;
        buffer[o + 17] = 0;
        byte state = buffer[o + 23];
        buffer[o + 23] = 0;
        buffer[o + 16] = (byte)((0x100 - Sum(buffer, o, HeaderLength)) & 0xFF);
        buffer[o + 17] = (Attributes & AttribChecksum) != 0
            ? (byte)((0x100 - Sum(buffer, DataOffset, End - DataOffset)) & 0xFF)
            : (byte)0xAA;
        buffer[o + 23] = state;
    }

    static int Sum(byte[] b, int start, int len)
    {
        int s = 0;
        for (int i = 0; i < len; i++) s += b[start + i];
        return s & 0xFF;
    }
}
