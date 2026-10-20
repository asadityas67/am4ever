// SPDX-License-Identifier: GPL-2.0-or-later
namespace Am4ever.Core.Board.Cpm;

/// <summary>
/// One AMD CPM configuration table (a 4-byte tag beginning "$A", e.g. $A10 GPIO, $A11 GEVENT,
/// $A26 DXIO). The 16-byte header holds a 16-bit size, two revision bytes, a platform mask and an
/// attributes word; the table body follows at <see cref="BodyOffset"/>.
/// </summary>
public sealed record CpmTable(
    int Offset, string Tag, int Size, byte FeatureRevision, byte ContentRevision,
    uint PlatformMask, uint Attributes)
{
    public const int HeaderLength = 0x10;

    /// <summary>Offset of the first body byte (entries/descriptors start here).</summary>
    public int BodyOffset => Offset + HeaderLength;

    /// <summary>One past the last byte of the table.</summary>
    public int End => Offset + Size;
}
