// SPDX-License-Identifier: GPL-2.0-or-later
using System.Text;
using Am4ever.Core.Board.Cpm;
using Am4ever.Core.Rom;
using Am4ever.Core.Rom.Amd;
using Am4ever.Core.Rom.Compression;
using Am4ever.Core.Rom.Fv;

namespace Am4ever.App.Lab;

public sealed record RomSummary(long Size, int AmdDirectories, string? Agesa);
public sealed record OemModule(string Name, Guid Guid);

/// <summary>
/// Read-only expert analysis of an OEM or donor ROM: ROM/AGESA summary, the PEI module list, and each
/// CPM OEM PEIM's published tables. UI-free (unit-tested); reuses the Core parsers, never duplicates them.
/// </summary>
public sealed class BoardAnalysis
{
    const uint FvSignature = 0x4856465F;   // _ F V H
    readonly byte[] _rom;
    readonly byte[] _peiBuf;               // the ROM itself (uncompressed PEI) or the decompressed boot blob
    readonly int _fvOffset;

    BoardAnalysis(byte[] rom, byte[] peiBuf, int fvOffset) { _rom = rom; _peiBuf = peiBuf; _fvOffset = fvOffset; }

    /// <summary>Load a ROM. The PEI volume is read uncompressed at <paramref name="peiFvOffset"/> if an FV is there
    /// (HP-style), else decompressed from the boot blob at <paramref name="bootBlobOffset"/> (ASRock-style).</summary>
    public static BoardAnalysis Load(byte[] rom, int peiFvOffset, int bootBlobOffset)
    {
        RomImage.Load(rom);                // validates size + AM4-ness (throws RomFormatException otherwise)
        if (peiFvOffset + 44 <= rom.Length && Bin.U32(rom, peiFvOffset + 40) == FvSignature)
            return new BoardAnalysis(rom, rom, peiFvOffset);
        uint clen = Bin.U32(rom, bootBlobOffset + 0x14);
        var (_, pei) = PspZlib.Decompress(rom.AsSpan(bootBlobOffset, 0x100 + (int)clen));
        return new BoardAnalysis(rom, pei, 0x20);
    }

    public RomSummary Summary() => new(_rom.Length, AmdDirectory.FindAll(RomImage.Load(_rom)).Count, FindAgesa(_rom));

    public IReadOnlyList<OemModule> Modules()
    {
        var list = new List<OemModule>();
        foreach (var f in new FirmwareVolume(_peiBuf, _fvOffset).Files())
        {
            var name = f.Name();
            if (name.Length > 0) list.Add(new OemModule(name, f.Guid));
        }
        return list;
    }

    /// <summary>The CPM table tags (e.g. "$A10/0xFFFF00") published by the index-th module of that name; empty if absent.</summary>
    public IReadOnlyList<string> CpmTables(string moduleName, int index)
    {
        var img = ImageSection(moduleName, index);
        if (img is null) return Array.Empty<string>();
        return CpmImage.Tables(img)
            .Select(t => t.PlatformMask != 0 ? $"{t.Tag}/0x{t.PlatformMask:X}" : t.Tag).ToList();
    }

    internal byte[]? ImageSection(string moduleName, int index)
    {
        var hits = new FirmwareVolume(_peiBuf, _fvOffset).Files().Where(f => f.Name() == moduleName).ToList();
        if (index < 0 || index >= hits.Count) return null;
        var (s, e) = hits[index].ImageSection();
        return _peiBuf[s..e];
    }

    static string? FindAgesa(byte[] data)
    {
        int i = data.AsSpan().IndexOf("AGESA!"u8);
        if (i < 0) return null;
        var sb = new StringBuilder();
        for (int j = i; j < data.Length && j < i + 64; j++)
        {
            byte b = data[j];
            if (b == 0) sb.Append(' ');
            else if (b >= 0x20 && b < 0x7F) sb.Append((char)b);
            else break;
        }
        return sb.ToString().Trim();
    }
}
