// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Rom;

namespace Am4ever.Core.Board;

/// <summary>
/// Expected-bytes-checked edits. Every patch names the bytes it expects to find and refuses if they
/// do not match, so a profile can never silently corrupt an image whose module differs from the one
/// it was authored against. <see cref="NeutraliseOverride"/> makes a donor OEM routine return early
/// (<c>xor eax,eax; ret</c>), as in the Python reference's <c>neutralise</c>.
/// </summary>
public static class BytePatch
{
    static readonly byte[] XorEaxRet = { 0x31, 0xC0, 0xC3 };   // xor eax,eax ; ret

    public static void Apply(byte[] img, int offset, ReadOnlySpan<byte> expected, ReadOnlySpan<byte> replacement)
    {
        if (replacement.Length > expected.Length)
            throw new RomEditException(
                $"byte patch at 0x{offset:X}: replacement is {replacement.Length} bytes, longer than the {expected.Length} expected");
        if (offset < 0 || offset + expected.Length > img.Length)
            throw new RomEditException($"byte patch at 0x{offset:X}: {expected.Length} bytes run outside the image");

        var found = img.AsSpan(offset, expected.Length);
        if (!found.SequenceEqual(expected))
            throw new RomEditException(
                $"byte patch at 0x{offset:X}: expected {Hex(expected)} but found {Hex(found)}");

        replacement.CopyTo(img.AsSpan(offset, replacement.Length));
    }

    public static void NeutraliseOverride(byte[] img, int offset, ReadOnlySpan<byte> expected) =>
        Apply(img, offset, expected, XorEaxRet);

    static string Hex(ReadOnlySpan<byte> bytes) => Convert.ToHexString(bytes).ToLowerInvariant();
}
