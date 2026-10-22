// SPDX-License-Identifier: GPL-2.0-or-later
using System.Security.Cryptography;
using Am4ever.Core.Board;
using Am4ever.Core.Board.Cpm;
using Am4ever.Core.Board.Smbios;
using Am4ever.Core.Profile;
using Am4ever.Core.Rom;
using Am4ever.Core.Rom.Compression;
using Am4ever.Core.Rom.Fv;

namespace Am4ever.Core.Build;

/// <summary>
/// Runs a board profile against the user's own OEM dump and a donor ROM, producing a modded image.
/// Mirrors <c>build_hp285g3.py</c>: decompress the donor PEI volume, run the per-family CPM/verb
/// operations, recompress, apply the SMBIOS identity in the DXE volume, and verify. Inputs are never
/// mutated. The EC-fan DXE driver (<c>add_ec_init</c>) is Milestone 4; an M3 image omits it.
/// </summary>
public static class BuildPipeline
{
    const int BootWrapperSize = 0x30;         // FFS header (0x18) + GUID-defined section header (0x18)
    static readonly Guid SmbiosFileGuid = Guid.Parse("daf4bf89-ce71-4917-b522-c89d32fbc59f");

    static readonly string[] PeiOps =
        { "neutralise_override", "byte_patch", "port_gpio", "port_gevent", "empty_tables", "dxio_layout", "ddi_layout", "verb_table" };

    public static BuildResult Build(byte[] donorRom, byte[] oemRom, BoardProfile profile, DonorAdapter adapter, DmiIdentity? dmiOverride, IProgress<BuildProgress>? progress = null)
    {
        var report = new BuildReport();

        // Count the phases up front so progress is honest and the final report lands exactly on Total.
        int apcbCount = profile.Operations.Count(o => o.Op == "apcb_check");
        int peiCount = profile.Operations.Count(o => PeiOps.Contains(o.Op));
        var dxeSpecs = profile.Operations.Where(o => o.Op is "smbios_identity" or "add_ec_init").ToList();
        int total = 1 /*donor*/ + apcbCount + 1 /*PEI decompress*/ + peiCount + 1 /*boot repack*/ + (dxeSpecs.Count > 0 ? dxeSpecs.Count + 1 : 0);
        int done = 0;
        void Tick(string phase) => progress?.Report(new BuildProgress(++done, total, phase));

        if (adapter.Identity.Sha256 is { Length: > 0 } expected)
        {
            var actual = Convert.ToHexString(SHA256.HashData(donorRom)).ToLowerInvariant();
            if (!actual.StartsWith(expected.ToLowerInvariant(), StringComparison.Ordinal))
                throw new RomFormatException($"donor ROM sha256 {actual[..16]}… does not match the adapter ({expected[..Math.Min(16, expected.Length)]}…)");
        }
        Tick("Checking donor");

        var work = (byte[])donorRom.Clone();
        var vol = adapter.Volumes;

        RunApcbChecks(profile, work, oemRom, report, Tick);

        // --- PEI phase ---
        var (header, pei) = PspZlib.Decompress(work.AsSpan(vol.BootBlobOffset, vol.BootBlobLength));
        Tick("Reading boot volume");
        var donorFv = new FirmwareVolume(pei, vol.BootFvOffset);
        var oemFv = new FirmwareVolume(oemRom, profile.OemPeiFvOffset ?? throw new RomFormatException("profile is missing oem_pei_fv_offset"));

        var donorSections = SectionMap(donorFv, adapter.Modules);
        var oemSections = SectionMap(oemFv, profile.OemModules ?? Array.Empty<ModuleEntry>());

        var donorCopies = donorSections.ToDictionary(kv => kv.Key, kv => pei[kv.Value.Start..kv.Value.End]);
        var oemCopies = oemSections.ToDictionary(kv => kv.Key, kv => oemRom[kv.Value.Start..kv.Value.End]);

        IReadOnlyList<uint>? verbs = null;
        var verbOp = profile.Operations.FirstOrDefault(o => o.Op == "verb_table");
        if (verbOp is not null)
        {
            uint romBase = (uint)(0x1_0000_0000 - oemRom.Length);
            verbs = VerbTable.ExtractHp(oemRom, profile.OemPeiFvOffset.Value, verbOp.NewCodec
                ?? throw new RomFormatException("verb_table is missing new_codec"), romBase);
        }

        var ctx = new BuildContext(donorCopies, oemCopies) { HpVerbs = verbs };
        CaptureBaseGpio(ctx, profile);

        var peiSpecs = profile.Operations.Where(o => PeiOps.Contains(o.Op)).ToList();
        var peiOps = peiSpecs.Select(OperationFactory.Create).ToList();
        report.Section("Boot (PEI) volume");
        for (int i = 0; i < peiOps.Count; i++) { peiOps[i].Check(ctx); report.Add(peiOps[i].Apply(ctx)); Tick($"Porting {peiSpecs[i].Op}"); }
        foreach (var op in peiOps)
            if (!op.Verify(ctx, out var msg))
                throw new RomEditException($"post-check failed: {msg}");

        // write the edited sections back and fix each PEIM file's checksums
        foreach (var (family, (start, _, fileOffset)) in donorSections)
        {
            donorCopies[family].CopyTo(pei, start);
            new FfsFile(pei, fileOffset).FixChecksums(pei);
        }

        RepackBootVolume(work, vol, header, pei, report);
        Tick("Recompressing boot volume");

        // --- DXE phase: SMBIOS identity and/or EC fan driver, one recompression ---
        if (dxeSpecs.Count > 0)
            ApplyDxe(work, vol, profile, dmiOverride, report, Tick);

        return new BuildResult(work, report);
    }

    static void RunApcbChecks(BoardProfile profile, byte[] donor, byte[] oem, BuildReport report, Action<string> tick)
    {
        var ops = profile.Operations.Where(o => o.Op == "apcb_check").ToList();
        if (ops.Count == 0) return;
        int donorApcb = First(Apcb.FindAll(RomImage.Load(donor)));
        int oemApcb = First(Apcb.FindAll(RomImage.Load(oem)));
        var ctx = new BuildContext(new(), new()) { OemRom = oem, DonorRom = donor, OemApcb = oemApcb, DonorApcb = donorApcb };
        report.Section("Memory (APCB) check");
        foreach (var spec in ops)
        {
            var op = OperationFactory.Create(spec);
            op.Check(ctx);
            report.Add(op.Apply(ctx));
            tick("Checking memory map");
        }
    }

    static void CaptureBaseGpio(BuildContext ctx, BoardProfile profile)
    {
        foreach (var op in profile.Operations.Where(o => o.Op == "port_gpio" && o.Merge == "family_delta" && o.BaseFamily is not null))
        {
            var baseImg = ctx.DonorPeim(op.BaseFamily);
            var table = CpmImage.FindTable(baseImg, "$A10", op.BasePlatformMask);
            ctx.BaseGpio[op.BaseFamily!] = GpioTable.Read(baseImg, table).Entries;
        }
    }

    static void RepackBootVolume(byte[] work, VolumeMap vol, byte[] header, byte[] pei, BuildReport report)
    {
        var blob = PspZlib.Compress(header, pei);
        int ffsOffset = vol.BootBlobOffset - BootWrapperSize;
        int oldFileSize = (int)Bin.U24(work, ffsOffset + 20);
        int oldEnd = ffsOffset + oldFileSize;
        int newFileSize = BootWrapperSize + blob.Length;
        int newEnd = ffsOffset + newFileSize;
        if (newEnd > work.Length - 0x1000)
            throw new RomEditException("recompressed boot volume does not fit the flash image");

        blob.CopyTo(work, vol.BootBlobOffset);
        for (int i = newEnd; i < oldEnd; i++) work[i] = 0xFF;          // clear any shrinkage
        Bin.W24(work, ffsOffset + 20, (uint)newFileSize);             // FFS file size
        Bin.W24(work, ffsOffset + 0x18, (uint)(newFileSize - 0x18));  // GUID-defined section size
        new FfsFile(work, ffsOffset).FixChecksums(work);
        report.Add($"boot volume recompressed: 0x{oldFileSize:X} -> 0x{newFileSize:X} bytes");
    }

    static void ApplyDxe(byte[] work, VolumeMap vol, BoardProfile profile, DmiIdentity? dmiOverride, BuildReport report, Action<string> tick)
    {
        var dmi = dmiOverride is null ? DmiIdentity.HpDefault : DmiIdentity.HpDefault.Merge(dmiOverride);
        var dxeFv = new FirmwareVolume(work, vol.DxeFvOffset);
        var dxeFile = dxeFv.FindByGuid(Guid.Parse(vol.DxeFileGuid ?? throw new RomFormatException("adapter is missing dxe_file_guid")));
        var lzmaSection = dxeFile.Sections().FirstOrDefault(s => s.Type == SectionType.GuidDefined)
            ?? throw new RomFormatException("DXE container has no GUID-defined LZMA section");

        var dec = GuidedSection.DecompressLzmaGuided(work, lzmaSection);
        var fvImage = SectionWalker.Walk(dec, 0, dec.Length).FirstOrDefault(s => s.Type == SectionType.FirmwareVolumeImage)
            ?? throw new RomFormatException("decompressed DXE blob has no firmware-volume-image section");

        report.Section("DXE volume");
        report.Add(ApplyDxeOperations(dec, fvImage.DataOffset, profile, dmi, tick));
        GuidedSection.ReplaceLzmaGuided(work, dxeFile, lzmaSection, dec);
        report.Add("DXE volume recompressed");
        tick("Recompressing DXE volume");
    }

    /// <summary>
    /// Run the DXE-phase operations (smbios_identity, add_ec_init) on an already-decompressed DXE blob,
    /// in profile order, and return their report lines. The SMBIOS static-data location is resolved only
    /// when a smbios_identity operation is present, so add_ec_init can run on a board with no AMI SMBIOS file.
    /// </summary>
    public static IReadOnlyList<string> ApplyDxeOperations(byte[] dec, int innerFvOffset, BoardProfile profile, DmiIdentity dmi, Action<string>? tick = null)
    {
        var innerFv = new FirmwareVolume(dec, innerFvOffset);
        int start = 0, end = 0;
        FfsFile? smbiosFile = null;
        if (profile.Operations.Any(o => o.Op == "smbios_identity"))
        {
            (start, end) = SmbiosTable.LocateStaticData(innerFv);
            smbiosFile = innerFv.FindByGuid(SmbiosFileGuid);
        }

        var ctx = new BuildContext(new(), new())
        {
            DxeVolume = dec,
            DxeInnerFvOffset = innerFvOffset,
            SmbiosStart = start,
            SmbiosEnd = end,
            Dmi = dmi,
        };
        var lines = new List<string>();
        foreach (var spec in profile.Operations.Where(o => o.Op is "smbios_identity" or "add_ec_init"))
        {
            var op = OperationFactory.Create(spec);
            op.Check(ctx);
            lines.AddRange(op.Apply(ctx));
            if (spec.Op == "smbios_identity")
                new FfsFile(dec, smbiosFile!.Offset).FixChecksums(dec);   // editing the static data disturbs the file checksum
            tick?.Invoke($"Writing {spec.Op}");
        }
        return lines;
    }

    static Dictionary<string, (int Start, int End, int FileOffset)> SectionMap(FirmwareVolume fv, IReadOnlyList<ModuleEntry> modules)
    {
        var byName = new Dictionary<string, List<FfsFile>>();
        foreach (var f in fv.Files())
        {
            if (!byName.TryGetValue(f.Name(), out var list)) byName[f.Name()] = list = new List<FfsFile>();
            list.Add(f);
        }
        var map = new Dictionary<string, (int, int, int)>();
        foreach (var m in modules)
        {
            if (!byName.TryGetValue(m.Name, out var list) || m.Index >= list.Count)
                throw new RomFormatException($"module {m.Name}#{m.Index} (family {m.Family}) not found in the volume");
            var (s, e) = list[m.Index].ImageSection();
            map[m.Family] = (s, e, list[m.Index].Offset);
        }
        return map;
    }

    static int First(IReadOnlyList<int> xs) =>
        xs.Count > 0 ? xs[0] : throw new RomFormatException("no APCB block found");
}
