// SPDX-License-Identifier: GPL-2.0-or-later
namespace Am4ever.Core.Rom.Fv;

/// <summary>In-place edits to a firmware volume. Edits never move data they do not own.</summary>
public static class FvEditor
{
    const int FfsHeaderLength = 24;

    /// <summary>Replace a file's body with bytes of exactly the same length, fixing checksums.</summary>
    public static void ReplaceFileBody(byte[] buffer, FfsFile file, ReadOnlySpan<byte> body)
    {
        int bodyLength = file.End - file.DataOffset;
        if (body.Length != bodyLength)
            throw new RomEditException(
                $"cannot replace file {file.Guid} body: new body is {body.Length} bytes, slot is {bodyLength}");
        body.CopyTo(buffer.AsSpan(file.DataOffset, bodyLength));
        new FfsFile(buffer, file.Offset).FixChecksums(buffer);
    }

    /// <summary>
    /// Place a new raw file in the volume's trailing free space, which must be all 0xFF and large
    /// enough. Nothing is written if it does not fit.
    /// </summary>
    public static void AppendRawFile(byte[] buffer, FirmwareVolume fv, Guid guid, ReadOnlySpan<byte> body)
    {
        int last = fv.First;
        foreach (var f in fv.Files())
            last = (f.End + 7) & ~7;

        int fileSize = FfsHeaderLength + body.Length;
        int end = last + fileSize;
        if (end > fv.End)
            throw new RomEditException(
                $"cannot append {fileSize}-byte file: only {fv.End - last} bytes free in the volume");
        for (int i = last; i < end; i++)
            if (buffer[i] != 0xFF)
                throw new RomEditException($"free space at 0x{i:X} is not erased (0xFF); refusing to overwrite");

        guid.ToByteArray().CopyTo(buffer, last);
        buffer[last + 18] = SectionType.Raw;
        buffer[last + 19] = 0;
        Bin.W24(buffer, last + 20, (uint)fileSize);
        buffer[last + 23] = 0xF8;                       // state: data valid
        body.CopyTo(buffer.AsSpan(last + FfsHeaderLength, body.Length));
        new FfsFile(buffer, last).FixChecksums(buffer);
    }

    /// <summary>
    /// Place an already-built FFS file in the volume's trailing free space (which must be all 0xFF and
    /// large enough), at the 8-byte-aligned end of the existing files. Returns the insertion offset.
    /// Unlike <see cref="AppendRawFile"/>, the file arrives complete — no header is built and no checksum fixed.
    /// </summary>
    public static int InsertFile(byte[] buffer, FirmwareVolume fv, ReadOnlySpan<byte> ffsFile)
    {
        int at = fv.First;
        foreach (var f in fv.Files())
            at = (f.End + 7) & ~7;

        int end = at + ffsFile.Length;
        if (end > fv.End)
            throw new RomEditException(
                $"cannot insert {ffsFile.Length}-byte file: only {fv.End - at} bytes free in the volume");
        for (int i = at; i < end; i++)
            if (buffer[i] != 0xFF)
                throw new RomEditException($"free space at 0x{i:X} is not erased (0xFF); refusing to overwrite");

        ffsFile.CopyTo(buffer.AsSpan(at, ffsFile.Length));
        return at;
    }
}
