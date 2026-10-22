// SPDX-License-Identifier: GPL-2.0-or-later
using System.Linq;
using Am4ever.App.Lab;
using Am4ever.Core.Build;
using Am4ever.Core.Profile;
using Am4ever.Core.Rom;
using Am4ever.Tests.Rom;

namespace Am4ever.Tests.Lab;

public class ComposerCoreTests
{
    [Fact] public void IdentitySupported_IsAlwaysTrue_ForAFreshBoard()
    {
        Assert.True(new ComposerCore().IdentitySupported);
    }

    [Fact] public void AddThenRemove_LeavesExpectedOps()
    {
        var c = new ComposerCore();
        c.AddOperation(new OperationSpec("apcb_check"));
        c.AddOperation(new OperationSpec("add_ec_init"));
        c.RemoveOperation(0);
        Assert.Equal(new[] { "add_ec_init" }, c.Draft.Operations.Select(o => o.Op));
    }

    [Fact] public void EnableTableEdit_WarnsEveryTime_AndRespectsDecline()
    {
        int warnings = 0;
        var c = new ComposerCore { ConfirmTableEdit = () => { warnings++; return false; } };
        Assert.False(c.EnableTableEdit());
        Assert.False(c.EnableTableEdit());
        Assert.Equal(2, warnings);              // warned on every enable
        Assert.False(c.TableEditEnabled);       // declined → stays off
        c.ConfirmTableEdit = () => { warnings++; return true; };
        Assert.True(c.EnableTableEdit());
        Assert.True(c.TableEditEnabled);
        Assert.Equal(3, warnings);
    }

    [Fact] public void Import_Garbage_LeavesDraftUnchanged()
    {
        var c = new ComposerCore();
        c.AddOperation(new OperationSpec("apcb_check"));
        Assert.Throws<RomFormatException>(() => c.Import(System.Text.Encoding.UTF8.GetBytes("{ not valid")));
        Assert.Equal(new[] { "apcb_check" }, c.Draft.Operations.Select(o => o.Op));
    }

    [RomFact] public void TestBuild_OfImportedHpProfile_Succeeds()
    {
        var c = new ComposerCore();
        c.SetOemDump(File.ReadAllBytes(TestRoms.HpF65!));
        c.SetAdapterId("asrock-ab350m-pro4-p10.43");
        c.SetDonor(File.ReadAllBytes(TestRoms.AsrockP1043!));
        c.Import(ProfileWriter.Write(BundledData.LoadProfile("hp-285-g3-8434")));
        var r = c.TestBuild();
        Assert.True(r.Ok, r.Error);
    }
}
