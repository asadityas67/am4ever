// SPDX-License-Identifier: GPL-2.0-or-later
namespace Am4ever.Core.Rom;

/// <summary>A whole SPI flash image (8/16/32 MiB) held as a mutable byte buffer.</summary>
public sealed class RomImage
{
    const ulong AddressModeMask = 0x3FFF_FFFF_FFFF_FFFF;

    RomImage(byte[] data) => Data = data;

    /// <summary>The raw image. Editors mutate this in place.</summary>
    public byte[] Data { get; }

    public int Size => Data.Length;

    public static RomImage Load(byte[] data)
    {
        if (data.Length is not (8 * 1024 * 1024 or 16 * 1024 * 1024 or 32 * 1024 * 1024))
            throw new RomFormatException(
                $"not a valid AM4 ROM: size {data.Length} bytes, expected 8, 16 or 32 MiB");
        return new RomImage(data);
    }

    /// <summary>
    /// Translate an AMD directory location (a flat offset, or an address mapped at the top of the
    /// 4 GiB space like 0xFFxxxxxx) into a file offset. Throws if it lands outside the image.
    /// </summary>
    public int ToOffset(ulong location)
    {
        location &= AddressModeMask;
        // An 0xFFxxxxxx-style address is mapped at the top of the 4 GiB space; anything else is a
        // flat file offset and must already be inside the image. We reject out-of-range offsets
        // rather than masking them, so a malformed location is caught instead of silently wrapped.
        long off = location >= 0xFF000000
            ? (long)location - (0x1_0000_0000 - Size)
            : (long)location;
        if (off < 0 || off >= Size)
            throw new RomFormatException($"location 0x{location:X} maps outside the {Size}-byte image");
        return (int)off;
    }
}
