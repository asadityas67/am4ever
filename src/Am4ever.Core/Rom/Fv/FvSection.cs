// SPDX-License-Identifier: GPL-2.0-or-later
namespace Am4ever.Core.Rom.Fv;

/// <summary>Known FFS section types.</summary>
public static class SectionType
{
    public const byte Compression = 0x01;
    public const byte GuidDefined = 0x02;
    public const byte Pe32 = 0x10;
    public const byte Te = 0x12;
    public const byte UserInterface = 0x15;
    public const byte FirmwareVolumeImage = 0x17;
    public const byte Raw = 0x19;
}

/// <summary>
/// One section inside an FFS file. For a GUID-defined section, <see cref="GuidName"/> and
/// <see cref="DataOffset"/> (which points past the GUID header to the payload) are set.
/// </summary>
public sealed record FvSection(
    int Offset, byte Type, int Size, int HeaderLength, int DataOffset, int End,
    Guid? GuidName, int? DataOffsetInGuid);
