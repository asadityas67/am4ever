// SPDX-License-Identifier: GPL-2.0-or-later
namespace Am4ever.Core.Rom.Fv;

/// <summary>Walks the section list inside an FFS file's data area.</summary>
public static class SectionWalker
{
    public static IReadOnlyList<FvSection> Walk(byte[] buf, int start, int end)
    {
        var list = new List<FvSection>();
        int o = start;
        while (o + 4 <= end)
        {
            o = (o + 3) & ~3;
            if (o + 4 > end) break;
            int size = (int)Bin.U24(buf, o);
            byte type = Bin.U8(buf, o + 3);
            int hl = 4;
            if (size == 0xFFFFFF)
            {
                size = (int)Bin.U32(buf, o + 4);
                hl = 8;
            }
            if (size < hl) break;
            int secEnd = o + size;
            if (secEnd > end) break;

            Guid? guidName = null;
            int dataOffset = o + hl;
            int? dataOffsetInGuid = null;
            if (type == SectionType.GuidDefined)
            {
                guidName = Bin.Guid(buf, o + hl);
                dataOffsetInGuid = Bin.U16(buf, o + hl + 16);
                dataOffset = o + dataOffsetInGuid.Value;
            }
            list.Add(new FvSection(o, type, size, hl, dataOffset, secEnd, guidName, dataOffsetInGuid));
            o += size;
        }
        return list;
    }
}

/// <summary>An FFSv2 firmware volume: a header followed by FFS files.</summary>
public sealed class FirmwareVolume
{
    const uint Signature = 0x4856465F;    // "_FVH" at offset 40

    readonly byte[] _buffer;

    public FirmwareVolume(byte[] buffer, int offset)
    {
        _buffer = buffer;
        Offset = offset;
        if (Bin.U32(buffer, offset + 40) != Signature)
            throw new RomFormatException($"no firmware volume signature at {offset:#0x}");
        Length = (long)Bin.U64(buffer, offset + 32);
        int headerLength = Bin.U16(buffer, offset + 48);
        int ext = Bin.U16(buffer, offset + 52);
        int first = offset + headerLength;
        if (ext != 0)
        {
            int extSize = (int)Bin.U32(buffer, offset + ext + 16);
            first = offset + ext + extSize;
        }
        First = (first + 7) & ~7;
        End = offset + (int)Length;
        if (End > buffer.Length)
            throw new RomFormatException($"firmware volume at {offset:#0x} ends past the buffer");
    }

    public byte[] Buffer => _buffer;
    public int Offset { get; }
    public long Length { get; }
    public int First { get; }
    public int End { get; }

    public IEnumerable<FfsFile> Files()
    {
        int o = First;
        while (o + 24 <= End)
        {
            if (IsErased(o))
                yield break;
            var file = new FfsFile(_buffer, o);
            if (file.Size < 24)
                throw new RomFormatException($"FFS file at {o:#0x} has an invalid size {file.Size}");
            if (file.End > End)
                throw new RomFormatException($"FFS file at {o:#0x} runs past the volume end");
            yield return file;
            o = (file.End + 7) & ~7;
        }
    }

    public FfsFile FindByGuid(Guid guid)
    {
        foreach (var f in Files())
            if (f.Guid == guid)
                return f;
        throw new RomFormatException($"no FFS file with GUID {guid} in the volume at {Offset:#0x}");
    }

    public IReadOnlyList<FfsFile> FindByName(string name) =>
        Files().Where(f => f.Name() == name).ToList();

    bool IsErased(int o)
    {
        for (int i = 0; i < 24; i++)
            if (_buffer[o + i] != 0xFF)
                return false;
        return true;
    }
}
