// SPDX-License-Identifier: GPL-2.0-or-later
using System.IO.Compression;

namespace Am4ever.Core.Rom.Compression;

/// <summary>
/// The AMD "PSP zlib" container used for the compressed boot (PEI) volume on ComboAM4v2 ROMs:
/// a 0x100-byte header whose offset 0x14 holds the compressed length, followed by a zlib stream.
/// </summary>
public static class PspZlib
{
    const int HeaderSize = 0x100;
    const int CompressedLengthOffset = 0x14;

    public static (byte[] Header, byte[] Plain) Decompress(ReadOnlySpan<byte> blob)
    {
        if (blob.Length < HeaderSize)
            throw new RomFormatException($"PSP zlib blob is only {blob.Length} bytes, need at least {HeaderSize}");
        var header = blob[..HeaderSize].ToArray();
        uint compressedLength = Bin.U32(header, CompressedLengthOffset);
        if (HeaderSize + (long)compressedLength > blob.Length)
            throw new RomFormatException("PSP zlib compressed length runs past the blob");
        var plain = Inflate(blob.Slice(HeaderSize, (int)compressedLength));
        return (header, plain);
    }

    public static byte[] Compress(byte[] header, ReadOnlySpan<byte> plain)
    {
        if (header.Length != HeaderSize)
            throw new ArgumentException($"header must be {HeaderSize} bytes", nameof(header));
        var comp = Deflate(plain);
        var outHeader = (byte[])header.Clone();
        Bin.W32(outHeader, CompressedLengthOffset, (uint)comp.Length);
        var result = new byte[HeaderSize + comp.Length];
        outHeader.CopyTo(result, 0);
        comp.CopyTo(result, HeaderSize);
        return result;
    }

    static byte[] Inflate(ReadOnlySpan<byte> zlib)
    {
        using var input = new MemoryStream(zlib.ToArray(), writable: false);
        using var zs = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        zs.CopyTo(output);
        return output.ToArray();
    }

    static byte[] Deflate(ReadOnlySpan<byte> plain)
    {
        using var output = new MemoryStream();
        using (var zs = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
            zs.Write(plain);
        return output.ToArray();
    }
}
