// SPDX-License-Identifier: GPL-2.0-or-later
using System.Text;
using Am4ever.Core.Board.Cpm;
using Am4ever.Emu;
using Am4ever.Core.Rom;
using Am4ever.Core.Rom.Amd;
using Am4ever.Core.Rom.Compression;
using Am4ever.Core.Rom.Fv;

namespace Am4ever.App.Lab;

public sealed record RomSummary(long Size, int AmdDirectories, string? Agesa);
public sealed record OemModule(string Name, Guid Guid);
public sealed record Diff(bool SameLength, int FirstDiff, int DiffCount);

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

    /// <summary>Byte-compare this ROM's module against another ROM's same-named module.</summary>
    public Diff CompareModule(BoardAnalysis other, string moduleName, int index)
    {
        var a = ImageSection(moduleName, index);
        var b = other.ImageSection(moduleName, index);
        if (a is null || b is null) return new Diff(false, -1, -1);     // module missing in one ROM
        int min = Math.Min(a.Length, b.Length), first = -1, count = 0;
        for (int i = 0; i < min; i++)
            if (a[i] != b[i]) { if (first < 0) first = i; count++; }
        bool same = a.Length == b.Length;
        count += Math.Abs(a.Length - b.Length);
        if (!same && first < 0) first = min;                           // identical prefix, differ in length
        return new Diff(same, first, count);
    }

    /// <summary>Whether the native emulator host is available to run a module.</summary>
    public static bool EmulatorAvailable => PeiEmulator.HostPath is not null;

    /// <summary>Run a module in the emulator under a CPU family, or null if the host is unavailable / the module is absent.</summary>
    public PeiResult? Emulate(string moduleName, int index, uint cpuid, int? overrideOffset = null)
    {
        if (!EmulatorAvailable) return null;
        var img = ImageSection(moduleName, index);
        return img is null ? null : PeiEmulator.Run(img, cpuid, overrideOffset);
    }

    /// <summary>The HD-audio codec id (e.g. 0x10EC0671) found in the AmdSocAm4* PEIMs' codec-list
    /// entries, or null if none. Used by the composer to seed a <c>verb_table</c> operation.</summary>
    public uint? AudioCodec()
    {
        foreach (var f in new FirmwareVolume(_peiBuf, _fvOffset).Files())
        {
            if (!f.Name().StartsWith("AmdSocAm4", StringComparison.Ordinal)) continue;
            for (int i = f.Offset; i + 4 <= f.End; i++)
                if ((Bin.U32(_peiBuf, i) >> 16) == 0x10EC) return Bin.U32(_peiBuf, i);   // Realtek codec id
        }
        return null;
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
