// SPDX-License-Identifier: GPL-2.0-or-later
using System.Text;
using Am4ever.Core.Board.Smbios;
using Am4ever.Core.Build;
using Am4ever.Core.Profile;
using Am4ever.Core.Rom;
using Am4ever.Tests.Board;

namespace Am4ever.Tests.Build;

/// <summary>Regression tests for the M3 whole-branch review findings (hardening against bad profiles / --dmi).</summary>
public class M3HardeningTests
{
    // Finding #1: an operation offset past the module must refuse cleanly, not throw a raw range error.
    [Fact] public void Neutralise_OffsetPastImage_ThrowsRomEdit()
    {
        var ctx = SyntheticBuild.ContextWithOverrideSite(0x10, new byte[] { 0x81, 0xEC, 0xC4, 0, 0, 0 });
        var op = OperationFactory.Create(new OperationSpec("neutralise_override")
        { Offset = 0x10000, Expected = new byte[] { 1, 2, 3 } });
        Assert.Throws<RomEditException>(() => op.Check(ctx));
    }

    // Finding #3: a malformed --dmi document must surface as the project's own error type.
    [Fact] public void DmiFromJson_Malformed_ThrowsRomFormat() =>
        Assert.Throws<RomFormatException>(() => DmiIdentity.FromJson(Encoding.UTF8.GetBytes("{not json")));

    [Fact] public void ApplyIdentity_BadUuid_ThrowsRomFormat()
    {
        var (img, start, end) = SyntheticSmbios.OneType1("x", "y");
        Assert.Throws<RomFormatException>(() =>
            SmbiosTable.ApplyIdentity(img, start, end, new DmiIdentity { SystemUuid = "not-a-uuid" }, out _));
    }

    // Finding #4: an inferred DXIO mapping must record its evidence in the build report.
    [Fact] public void DxioLayout_InferredMapping_ReportsEvidence()
    {
        var (img, _) = SyntheticCpm.OneDxioTable(descriptors: 6, descSize: 0x1A4);
        var ctx = new BuildContext(new() { ["X"] = img }, new());
        var op = OperationFactory.Create(new OperationSpec("dxio_layout")
        {
            Family = "X",
            PlatformMask = 0,
            Dxio = new[] { new DxioEdit(2, null, null, new[] { 0, 0 }, null, null, 1) },
            Confidence = "inferred",
            Evidence = "mirrored GPP numbering",
        });
        op.Check(ctx);
        Assert.Contains(op.Apply(ctx), l => l.Contains("mirrored GPP numbering"));
    }
}
