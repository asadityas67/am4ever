// SPDX-License-Identifier: GPL-2.0-or-later
using SevenZip;
using SevenZip.Compression.LZMA;

namespace Am4ever.Core.Rom.Compression;

#pragma warning disable CA1031  // we deliberately translate any decoder failure on untrusted input

/// <summary>
/// The EDK2 "LZMA alone" container used for compressed firmware-volume sections: 5 LZMA property
/// bytes, then the uncompressed size as a little-endian UInt64, then the raw LZMA stream.
/// Backed by the public-domain 7-Zip LZMA SDK (lzma-sdk package).
/// </summary>
public static class EdkLzma
{
    const int HeaderSize = 13;          // 5 property bytes + 8-byte size
    const int DictionarySize = 0x0100_0000;
    // No AM4 ROM is bigger than 32 MiB, so a decompressed section far above that is a corrupt header.
    const ulong MaxDecompressedSize = 64UL * 1024 * 1024;

    public static byte[] Decompress(ReadOnlySpan<byte> lzma)
    {
        if (lzma.Length < HeaderSize)
            throw new RomFormatException($"LZMA blob is only {lzma.Length} bytes, need at least {HeaderSize}");
        var bytes = lzma.ToArray();
        ulong size = Bin.U64(bytes, 5);
        if (size > MaxDecompressedSize)
            throw new RomFormatException($"LZMA header claims {size} bytes, which exceeds the {MaxDecompressedSize}-byte sanity limit");

        try
        {
            var decoder = new Decoder();
            decoder.SetDecoderProperties(bytes[..5]);
            using var input = new MemoryStream(bytes, HeaderSize, bytes.Length - HeaderSize, writable: false);
            using var output = new MemoryStream((int)size);
            decoder.Code(input, output, bytes.Length - HeaderSize, (long)size, null);
            if (output.Length != (long)size)
                throw new RomFormatException($"LZMA stream decoded to {output.Length} bytes, header claimed {size}");
            return output.ToArray();
        }
        // The 7-Zip SDK's DataErrorException/InvalidParamException are internal and derive from
        // ApplicationException; a malformed stream can also surface as an index/argument/EOF error.
        catch (Exception e) when (e is ApplicationException
                                    or IndexOutOfRangeException or ArgumentException or EndOfStreamException)
        {
            throw new RomFormatException($"corrupt LZMA stream: {e.Message}");
        }
    }

    public static byte[] Compress(ReadOnlySpan<byte> plain)
    {
        var plainBytes = plain.ToArray();
        var encoder = new Encoder();
        encoder.SetCoderProperties(
            new[]
            {
                CoderPropID.DictionarySize, CoderPropID.PosStateBits, CoderPropID.LitContextBits,
                CoderPropID.LitPosBits, CoderPropID.NumFastBytes, CoderPropID.MatchFinder, CoderPropID.EndMarker,
            },
            new object[] { DictionarySize, 2, 3, 0, 273, "bt4", false });

        using var propStream = new MemoryStream();
        encoder.WriteCoderProperties(propStream);
        var props = propStream.ToArray();          // 5 bytes: 0x5D, dictionary size LE

        using var input = new MemoryStream(plainBytes, writable: false);
        using var stream = new MemoryStream();
        encoder.Code(input, stream, plainBytes.Length, -1, null);
        var comp = stream.ToArray();

        var result = new byte[HeaderSize + comp.Length];
        props.CopyTo(result, 0);
        Bin.W64(result, 5, (ulong)plainBytes.Length);   // the true uncompressed size
        comp.CopyTo(result, HeaderSize);
        return result;
    }
}

#pragma warning restore CA1031
