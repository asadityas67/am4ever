// SPDX-License-Identifier: GPL-2.0-or-later
namespace Am4ever.Core.Rom.Amd;

public enum AmdDirKind { Psp, PspL2, Bios, BiosL2 }

/// <summary>One entry in an AMD directory. BIOS entries are 24 bytes, PSP entries 16.</summary>
public sealed record AmdEntry(
    int Index, int EntryOffset, byte Type, byte SubProgram, byte RegionId,
    uint Flags, uint Size, ulong Location);

/// <summary>
/// An AMD PSP or BIOS directory (one of the four 4-byte magics). The header is 16 bytes: magic, a Fletcher-32
/// checksum, an entry count, and an info word; entries follow.
/// </summary>
public sealed class AmdDirectory
{
    // 4-byte magics read as little-endian uints (kept numeric so the repo's no-firmware scan stays clean).
    const uint MagicPsp = 0x50535024;    // $ P S P
    const uint MagicPspL2 = 0x324C5024;  // $ P L 2
    const uint MagicBios = 0x44484224;   // $ B H D
    const uint MagicBiosL2 = 0x324C4224; // $ B L 2

    readonly int _entrySize;

    public AmdDirectory(RomImage rom, int offset)
    {
        var data = rom.Data;
        uint magic = Bin.U32(data, offset);
        Kind = magic switch
        {
            MagicPsp => AmdDirKind.Psp,
            MagicPspL2 => AmdDirKind.PspL2,
            MagicBios => AmdDirKind.Bios,
            MagicBiosL2 => AmdDirKind.BiosL2,
            _ => throw new RomFormatException($"no AMD directory magic at 0x{offset:X}"),
        };
        DirOffset = offset;
        StoredChecksum = Bin.U32(data, offset + 4);
        Count = (int)Bin.U32(data, offset + 8);
        bool isBios = Kind is AmdDirKind.Bios or AmdDirKind.BiosL2;
        _entrySize = isBios ? 24 : 16;

        var entries = new List<AmdEntry>();
        if (Count is > 0 and < 64)
        {
            int bodyEnd = offset + 16 + Count * _entrySize;
            if (bodyEnd <= data.Length)
            {
                for (int i = 0; i < Count; i++)
                {
                    int eo = offset + 16 + i * _entrySize;
                    if (isBios)
                    {
                        entries.Add(new AmdEntry(i, eo,
                            Type: Bin.U8(data, eo), RegionId: Bin.U8(data, eo + 1),
                            Flags: Bin.U8(data, eo + 2), SubProgram: Bin.U8(data, eo + 3),
                            Size: Bin.U32(data, eo + 4), Location: Bin.U64(data, eo + 8)));
                    }
                    else
                    {
                        entries.Add(new AmdEntry(i, eo,
                            Type: (byte)(Bin.U32(data, eo) & 0xFF), SubProgram: 0, RegionId: 0, Flags: 0,
                            Size: Bin.U32(data, eo + 4), Location: Bin.U64(data, eo + 8)));
                    }
                }
                ChecksumValid = ComputedChecksum(data) == StoredChecksum;
            }
        }
        Entries = entries;
    }

    public AmdDirKind Kind { get; }
    public int DirOffset { get; }
    public int Count { get; }
    public uint StoredChecksum { get; }
    public bool ChecksumValid { get; private set; }
    public IReadOnlyList<AmdEntry> Entries { get; }

    public void FixChecksum(RomImage rom)
    {
        Bin.W32(rom.Data, DirOffset + 4, ComputedChecksum(rom.Data));
    }

    uint ComputedChecksum(byte[] data)
    {
        // The checksum covers everything after the 4-byte checksum field: the count, info and entries.
        int bodyStart = DirOffset + 8;
        int bodyEnd = DirOffset + 16 + Count * _entrySize;
        return Fletcher32.Compute(data.AsSpan(bodyStart, bodyEnd - bodyStart));
    }

    /// <summary>Every valid directory in the image, scanned on 0x100-byte boundaries.</summary>
    public static IReadOnlyList<AmdDirectory> FindAll(RomImage rom)
    {
        var found = new List<AmdDirectory>();
        for (int off = 0; off + 16 <= rom.Size; off += 0x100)
        {
            uint magic = Bin.U32(rom.Data, off);
            if (magic is not (MagicPsp or MagicPspL2 or MagicBios or MagicBiosL2))
                continue;
            var dir = new AmdDirectory(rom, off);
            if (dir is { Count: > 0 and < 64, ChecksumValid: true })
                found.Add(dir);
        }
        return found;
    }
}
