// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Tests.Rom;

namespace Am4ever.Tests.Build;

/// <summary>
/// Builds a decompressed-DXE-shaped buffer: an inner FFSv2 volume (one filler file plus trailing free
/// space) at an 8-byte-aligned offset, as the DXE LZMA section decompresses to. The FFS first-file
/// alignment is absolute, so the inner volume must start 8-aligned (the real decompressed blob is).
/// Returns the buffer and the inner volume's offset. The AddEcInit op reads that offset directly.
/// </summary>
public static class SyntheticDxe
{
    public static (byte[] Dxe, int InnerFvOffset) WithInnerVolume(int freeSpace)
    {
        var inner = SyntheticFv.WithFreeSpace(freeSpace, out _);
        const int innerOffset = 8;                       // stands in for the type-0x17 section header, 8-aligned
        var dxe = new byte[innerOffset + inner.Length];
        inner.CopyTo(dxe, innerOffset);
        return (dxe, innerOffset);
    }
}
