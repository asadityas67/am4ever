// SPDX-License-Identifier: GPL-2.0-or-later
namespace Am4ever.Core.Rom.Amd;

/// <summary>
/// The Fletcher-32 variant AMD uses for PSP/BIOS directory checksums: two 16-bit sums seeded at
/// 0xFFFF, over 16-bit little-endian words, folded in blocks of 359 words. Input length must be even.
/// </summary>
public static class Fletcher32
{
    public static uint Compute(ReadOnlySpan<byte> data)
    {
        if ((data.Length & 1) != 0)
            throw new ArgumentException("Fletcher-32 input length must be even", nameof(data));
        uint c0 = 0xFFFF, c1 = 0xFFFF;
        int words = data.Length / 2;
        int i = 0;
        while (i < words)
        {
            int end = Math.Min(i + 359, words);
            for (; i < end; i++)
            {
                uint w = (uint)(data[i * 2] | (data[i * 2 + 1] << 8));
                c0 += w;
                c1 += c0;
            }
            c0 = (c0 & 0xFFFF) + (c0 >> 16);
            c1 = (c1 & 0xFFFF) + (c1 >> 16);
        }
        c0 = (c0 & 0xFFFF) + (c0 >> 16);
        c1 = (c1 & 0xFFFF) + (c1 >> 16);
        return (c1 << 16) | c0;
    }
}
