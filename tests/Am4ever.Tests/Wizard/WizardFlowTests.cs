// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.App.Wizard;
using Am4ever.Tests.Rom;

namespace Am4ever.Tests.Wizard;

public class WizardFlowTests
{
    static byte[] Rom16() => new byte[16 * 1024 * 1024];

    [Fact] public void Step1_TwoDumpsDiffer_Blocks()
    {
        var a = Rom16(); var b = Rom16(); b[0] = 1;
        var w = new WizardFlow(); w.SetDump(a, b);
        Assert.False(w.CanAdvance(out var r));
        Assert.Contains("match", r, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public void Step1_RejectsNonAm4AndBlank()
    {
        var w = new WizardFlow();
        w.SetDump(new byte[4 * 1024 * 1024]);
        Assert.False(w.CanAdvance(out _));                               // wrong size
        w.SetDump(Enumerable.Repeat((byte)0xFF, 16 * 1024 * 1024).ToArray());
        Assert.False(w.CanAdvance(out var r));                           // blank / no AMD dirs
        Assert.NotNull(r);
    }

    [RomFact] public void Step1_ValidDump_Advances()
    {
        var w = new WizardFlow();
        w.SetDump(File.ReadAllBytes(TestRoms.HpF65!));
        Assert.True(w.CanAdvance(out _));
    }

    [Fact] public void Steps_InOrder() =>
        Assert.Equal(new[] { WizardStep.Dump, WizardStep.Profile, WizardStep.Donor, WizardStep.Identity, WizardStep.Build, WizardStep.Flash },
            Enum.GetValues<WizardStep>());

    [Fact] public void Next_GatedByValidation()
    {
        var w = new WizardFlow();
        Assert.False(w.Next());                 // no dump set yet
        Assert.Equal(WizardStep.Dump, w.Step);
    }

    [Fact] public void Back_PreservesState()
    {
        var w = new WizardFlow();
        w.SetDump(Rom16());                     // not a valid AM4 image, but the path/bytes are retained
        w.Back();                               // already at Dump: no-op, below-floor guarded
        Assert.Equal(WizardStep.Dump, w.Step);
        Assert.NotNull(w.PrimaryDump);
    }
}
