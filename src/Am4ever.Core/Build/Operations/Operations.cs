// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Board;
using Am4ever.Core.Board.Cpm;
using Am4ever.Core.Board.Ec;
using Am4ever.Core.Board.Smbios;
using Am4ever.Core.Profile;
using Am4ever.Core.Rom;
using Am4ever.Core.Rom.Fv;

namespace Am4ever.Core.Build.Operations;

/// <summary>Make a donor OEM-PEIM runtime override routine return early (xor eax,eax; ret).</summary>
public sealed class NeutraliseOverrideOp(OperationSpec spec) : BoardOperation(spec)
{
    public override void Check(BuildContext ctx)
    {
        if (Spec.Offset is null || Spec.Expected is null)
            throw new RomFormatException("neutralise_override needs an offset and expected bytes");
        var img = ctx.DonorPeim(Spec.Family);
        OpGuard.RequireRange(img, Spec.Offset.Value, Spec.Expected.Length, "neutralise_override");
        if (!img.AsSpan(Spec.Offset.Value, Spec.Expected.Length).SequenceEqual(Spec.Expected))
            throw new RomEditException($"neutralise_override at 0x{Spec.Offset:X}: expected bytes not found");
    }

    public override IReadOnlyList<string> Apply(BuildContext ctx)
    {
        BytePatch.NeutraliseOverride(ctx.DonorPeim(Spec.Family), Spec.Offset!.Value, Spec.Expected!);
        return new[] { $"override routine @0x{Spec.Offset:X} -> xor eax,eax; ret" };
    }

    public override bool Verify(BuildContext ctx, out string message)
    {
        var img = ctx.DonorPeim(Spec.Family);
        int o = Spec.Offset!.Value;
        bool ok = o >= 0 && o + 3 <= img.Length && img.AsSpan(o, 3).SequenceEqual(new byte[] { 0x31, 0xC0, 0xC3 });
        message = ok ? "" : $"override at 0x{Spec.Offset:X} was not neutralised";
        return ok;
    }
}

/// <summary>Escape-hatch expected-bytes patch of a module.</summary>
public sealed class BytePatchOp(OperationSpec spec) : BoardOperation(spec)
{
    public override void Check(BuildContext ctx)
    {
        if (Spec.Offset is null || Spec.Expected is null || Spec.New is null)
            throw new RomFormatException("byte_patch needs offset, expected and new bytes");
        var img = ctx.DonorPeim(Spec.Family);
        OpGuard.RequireRange(img, Spec.Offset.Value, Spec.Expected.Length, "byte_patch");
        if (!img.AsSpan(Spec.Offset.Value, Spec.Expected.Length).SequenceEqual(Spec.Expected))
            throw new RomEditException($"byte_patch at 0x{Spec.Offset:X}: expected bytes not found");
    }

    public override IReadOnlyList<string> Apply(BuildContext ctx)
    {
        BytePatch.Apply(ctx.DonorPeim(Spec.Family), Spec.Offset!.Value, Spec.Expected!, Spec.New!);
        return new[] { $"byte patch @0x{Spec.Offset:X} ({Spec.New!.Length} bytes)" };
    }
}

/// <summary>Install the OEM GPIO table ($A10), optionally merging the donor's per-family deltas.</summary>
public sealed class PortGpioOp(OperationSpec spec) : BoardOperation(spec)
{
    public override void Check(BuildContext ctx) =>
        CpmImage.FindTable(ctx.DonorPeim(Spec.Family), "$A10", Spec.PlatformMask);

    public override IReadOnlyList<string> Apply(BuildContext ctx)
    {
        var donor = ctx.DonorPeim(Spec.Family);
        var hp = ctx.HpPeim(Spec.Family);
        var dt = CpmImage.FindTable(donor, "$A10", Spec.PlatformMask);
        var ht = CpmImage.FindTable(hp, "$A10", Spec.HpPlatformMask);
        var (hpEntries, hpOrder) = GpioTable.Read(hp, ht);

        if (Spec.Merge == "family_delta")
        {
            if (Spec.BaseFamily is null || !ctx.BaseGpio.TryGetValue(Spec.BaseFamily, out var baseEntries))
                throw new RomFormatException($"port_gpio family_delta needs a captured base from family '{Spec.BaseFamily}'");
            var (familyEntries, _) = GpioTable.Read(donor, dt);
            var (merged, order, notes) = GpioTable.Merge(hpEntries, hpOrder, baseEntries, familyEntries, Spec.Family ?? "");
            GpioTable.Write(donor, dt, merged, order);
            return notes.Prepend($"$A10 pmask=0x{Spec.PlatformMask:X}: OEM table + {Spec.Family} deltas ({merged.Count} pins)").ToList();
        }

        GpioTable.Write(donor, dt, hpEntries, hpOrder);
        return new[] { $"$A10 pmask=0x{Spec.PlatformMask:X}: OEM GPIO table ({hpEntries.Count} pins)" };
    }
}

/// <summary>Copy the OEM GEVENT/SCI table ($A11) over every donor instance.</summary>
public sealed class PortGeventOp(OperationSpec spec) : BoardOperation(spec)
{
    public override void Check(BuildContext ctx)
    {
        CpmImage.FindTable(ctx.HpPeim(Spec.Family), "$A11");
        if (CpmImage.Tables(ctx.DonorPeim(Spec.Family)).All(t => t.Tag != "$A11"))
            throw new RomFormatException("port_gevent: donor has no $A11 table");
    }

    public override IReadOnlyList<string> Apply(BuildContext ctx)
    {
        var donor = ctx.DonorPeim(Spec.Family);
        var hp = ctx.HpPeim(Spec.Family);
        var ht = CpmImage.FindTable(hp, "$A11");
        var lines = new List<string>();
        foreach (var dt in CpmImage.Tables(donor).Where(t => t.Tag == "$A11"))
        {
            CpmTables.CopyBody(donor, dt, hp, ht);
            lines.Add($"$A11 @0x{dt.Offset:X}: OEM GEVENT table");
        }
        return lines;
    }
}

/// <summary>Empty the device/clock/secondary tables the OEM leaves unused.</summary>
public sealed class EmptyTablesOp(OperationSpec spec) : BoardOperation(spec)
{
    public override void Check(BuildContext ctx) { }

    public override IReadOnlyList<string> Apply(BuildContext ctx)
    {
        var donor = ctx.DonorPeim(Spec.Family);
        var lines = new List<string>();
        foreach (var et in Spec.Tables ?? Array.Empty<EmptyTableSpec>())
            foreach (var t in CpmImage.Tables(donor).Where(t => t.Tag == et.Tag))
            {
                CpmTables.Empty(donor, t, et.Term);
                lines.Add($"{et.Tag} @0x{t.Offset:X}: emptied (OEM leaves this unused)");
            }
        return lines;
    }
}

/// <summary>Rewrite the DXIO port topology ($A26) per the profile's descriptor edits.</summary>
public sealed class DxioLayoutOp(OperationSpec spec) : BoardOperation(spec)
{
    public override void Check(BuildContext ctx) =>
        CpmImage.FindTable(ctx.DonorPeim(Spec.Family), "$A26", Spec.PlatformMask);

    public override IReadOnlyList<string> Apply(BuildContext ctx)
    {
        var donor = ctx.DonorPeim(Spec.Family);
        var t = CpmImage.FindTable(donor, "$A26", Spec.PlatformMask);
        var dx = new DxioTable(donor, t, Spec.DescriptorSize ?? 0x1A4);
        var lines = new List<string> { $"$A26 @0x{t.Offset:X} pmask=0x{Spec.PlatformMask:X}" };
        if (Spec.Confidence is not null || Spec.Evidence is not null)
            lines.Add($"  [{Spec.Confidence ?? "verified"}] {Spec.Evidence}");
        foreach (var e in Spec.Dxio ?? Array.Empty<DxioEdit>())
        {
            var before = dx.Describe(e.Desc);
            dx.Set(e.Desc, e.Template, e.Engine,
                e.Lanes is { Length: 2 } ? ((byte)e.Lanes[0], (byte)e.Lanes[1]) : null,
                e.Device, e.Function, e.ClkReq);
            lines.Add($"  desc{e.Desc}: {before}  ->  {dx.Describe(e.Desc)}");
        }
        return lines;
    }
}

/// <summary>Set the APU display (DDI) block: copy the OEM block, or keep the donor's.</summary>
public sealed class DdiLayoutOp(OperationSpec spec) : BoardOperation(spec)
{
    public override void Check(BuildContext ctx) =>
        CpmImage.FindTable(ctx.DonorPeim(Spec.Family), "$A26", Spec.PlatformMask);

    public override IReadOnlyList<string> Apply(BuildContext ctx)
    {
        var donor = ctx.DonorPeim(Spec.Family);
        var dt = CpmImage.FindTable(donor, "$A26", Spec.PlatformMask);
        if (Spec.DdiKeep == true)
            return new[] { $"$A26 pmask=0x{Spec.PlatformMask:X} displays: kept donor block ({Spec.Evidence})" };
        var hp = ctx.HpPeim(Spec.Family);
        var ht = CpmImage.FindTable(hp, "$A26", Spec.DdiFromPlatformMask);
        DdiBlock.Copy(donor, dt, hp, ht, Spec.DescriptorSize ?? 0x1A4, 0x94);   // OEM is PinnaclePI (0x94)
        var evidence = Spec.Evidence is null ? "" : $" [{Spec.Confidence ?? "verified"}] {Spec.Evidence}";
        return new[] { $"$A26 pmask=0x{Spec.PlatformMask:X} displays: copied OEM block{evidence}" };
    }
}

/// <summary>Install the OEM codec's verb table in place of the donor's.</summary>
public sealed class VerbTableOp(OperationSpec spec) : BoardOperation(spec)
{
    public override void Check(BuildContext ctx)
    {
        if (ctx.HpVerbs is null) throw new RomFormatException("verb_table: no OEM verb list was extracted");
        if (Spec.OldCodecs is null || Spec.NewCodec is null)
            throw new RomFormatException("verb_table needs old_codecs and new_codec");
    }

    public override IReadOnlyList<string> Apply(BuildContext ctx)
    {
        VerbTable.Replace(ctx.DonorPeim(Spec.Family), Spec.OldCodecs!.ToArray(), Spec.NewCodec!.Value, ctx.HpVerbs!);
        return new[] { $"verb table: donor codecs -> OEM 0x{Spec.NewCodec:X} ({ctx.HpVerbs!.Count} verbs)" };
    }
}

/// <summary>Apply the OEM SMBIOS identity to the DXE volume's static-data table.</summary>
public sealed class SmbiosIdentityOp(OperationSpec spec) : BoardOperation(spec)
{
    public override void Check(BuildContext ctx)
    {
        if (ctx.DxeVolume is null) throw new RomFormatException("smbios_identity: no DXE volume in the build context");
    }

    public override IReadOnlyList<string> Apply(BuildContext ctx)
    {
        SmbiosTable.ApplyIdentity(ctx.DxeVolume!, ctx.SmbiosStart, ctx.SmbiosEnd, ctx.Dmi ?? DmiIdentity.HpDefault, out var report);
        return report.Prepend("SMBIOS identity:").ToList();
    }
}

/// <summary>Add the HP EC fan hand-off driver to the DXE volume's free space.</summary>
public sealed class AddEcInitOp(OperationSpec spec) : BoardOperation(spec)
{
    public override void Check(BuildContext ctx)
    {
        if (ctx.DxeVolume is null)
            throw new RomFormatException("add_ec_init: no DXE volume in the build context");
        var fv = new FirmwareVolume(ctx.DxeVolume, ctx.DxeInnerFvOffset);
        if (fv.Files().Any(f => f.Guid == EcFanDriver.FileGuid))
            throw new RomEditException("add_ec_init: the EC fan driver is already present in the DXE volume");
    }

    public override IReadOnlyList<string> Apply(BuildContext ctx)
    {
        var driver = EcFanDriver.Build();
        var fv = new FirmwareVolume(ctx.DxeVolume!, ctx.DxeInnerFvOffset);
        int at = FvEditor.InsertFile(ctx.DxeVolume!, fv, driver);
        return new[] { $"EC fan driver ({driver.Length} bytes) added at +0x{at - ctx.DxeInnerFvOffset:X} in the DXE volume" };
    }
}

/// <summary>Check-only: confirm the OEM and donor DRAM SPD address maps match.</summary>
public sealed class ApcbCheckOp(OperationSpec spec) : BoardOperation(spec)
{
    public override void Check(BuildContext ctx)
    {
        if (ctx.OemRom is null || ctx.DonorRom is null)
            throw new RomFormatException("apcb_check: OEM and donor ROMs are not in the build context");
        if (!Apcb.SpdMapsCompatible(ctx.OemRom, ctx.OemApcb, ctx.DonorRom, ctx.DonorApcb, out var report))
            throw new RomEditException($"apcb_check failed: {report}. The donor's memory SPD map differs from the OEM board's.");
    }

    public override IReadOnlyList<string> Apply(BuildContext ctx)
    {
        Apcb.SpdMapsCompatible(ctx.OemRom!, ctx.OemApcb, ctx.DonorRom!, ctx.DonorApcb, out var report);
        return new[] { $"APCB check: {report}" };
    }
}

/// <summary>Shared operation guards.</summary>
static class OpGuard
{
    public static void RequireRange(byte[] img, int offset, int length, string op)
    {
        if (offset < 0 || length < 0 || (long)offset + length > img.Length)
            throw new RomEditException($"{op} at 0x{offset:X}: {length} byte(s) fall outside the {img.Length}-byte module");
    }
}

/// <summary>Small helpers shared by the CPM table operations.</summary>
static class CpmTables
{
    public static void CopyBody(byte[] dst, CpmTable dt, byte[] src, CpmTable st)
    {
        if (dt.Size != st.Size)
            throw new RomEditException($"table {dt.Tag} size mismatch: donor 0x{dt.Size:X} vs OEM 0x{st.Size:X}");
        Array.Copy(src, st.BodyOffset, dst, dt.BodyOffset, dt.Size - CpmTable.HeaderLength);
    }

    public static void Empty(byte[] img, CpmTable t, byte[] terminator)
    {
        for (int i = t.BodyOffset; i < t.End; i++) img[i] = 0;
        terminator.CopyTo(img, t.BodyOffset);
    }
}
