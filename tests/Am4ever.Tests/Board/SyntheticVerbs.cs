// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Rom;

namespace Am4ever.Tests.Board;

/// <summary>
/// Builds a synthetic HD-Audio codec list: one block per codec {UInt32 CodecId, verbs..., 0xFFFFFFFF},
/// the whole list ended by a CodecId of 0xFFFFFFFF. The list starts at offset 0 and fills the buffer.
/// </summary>
public static class SyntheticVerbs
{
    public const int ListStart = 0;
    const uint End = 0xFFFFFFFF;

    public static byte[] List(params (uint CodecId, uint[] Verbs)[] codecs)
    {
        var words = new List<uint>();
        foreach (var (id, verbs) in codecs)
        {
            words.Add(id);
            words.AddRange(verbs);
            words.Add(End);
        }
        words.Add(End);                 // list terminator
        var buf = new byte[words.Count * 4];
        for (int i = 0; i < words.Count; i++) Bin.W32(buf, i * 4, words[i]);
        return buf;
    }
}
