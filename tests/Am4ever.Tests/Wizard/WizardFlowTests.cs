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

    [Fact] public void Step2_OffersBundledProfileForSize()
    {
        var w = new WizardFlow(); w.SetDump(Rom16());
        Assert.Contains(w.MatchingProfiles(), p => p.Board.BoardId == "8434");
    }

    [RomFact] public void Step3_WrongDonorHash_Blocks()
    {
        var w = new WizardFlow();
        w.SetDump(File.ReadAllBytes(TestRoms.HpF65!)); Assert.True(w.Next());      // Dump -> Profile
        w.SelectProfile(w.MatchingProfiles()[0]); Assert.True(w.Next());          // Profile -> Donor
        w.SetDonor(Rom16());
        Assert.False(w.CanAdvance(out var r));
        Assert.Contains("SHA-256", r, StringComparison.OrdinalIgnoreCase);
    }

    [RomFact] public void Step3_CorrectDonorHash_Advances()
    {
        var w = new WizardFlow();
        w.SetDump(File.ReadAllBytes(TestRoms.HpF65!)); w.Next();
        w.SelectProfile(w.MatchingProfiles()[0]); w.Next();
        w.SetDonor(File.ReadAllBytes(TestRoms.AsrockP1043!));
        Assert.True(w.CanAdvance(out _));
    }

    [Fact] public void Step4_GenericIdentity_IsDefault() => Assert.Equal(IdentityChoice.Generic, new WizardFlow().Identity);

    [Fact] public void Step5_BuildFailure_NoImageNoOverride()
    {
        var w = new WizardFlow(); w.SetDump(Rom16());
        w.SelectProfile(w.MatchingProfiles()[0]);
        w.SetDonor(Rom16());                                   // wrong donor: hash check fails first
        var outPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".bin");
        var o = w.Build(outPath);
        Assert.False(o.Ok);
        Assert.False(File.Exists(outPath));                    // no image, no "build anyway"
    }

    [Fact] public void FlashChecklist_MentionsVoltageAndBackup()
    {
        Assert.Contains("3.3", FlashChecklist.Text);
        Assert.Contains("back up", FlashChecklist.Text, StringComparison.OrdinalIgnoreCase);
    }

    [RomFact] public void Step5_RealBuild_WritesImageAndReport()
    {
        var w = new WizardFlow();
        w.SetDump(File.ReadAllBytes(TestRoms.HpF65!)); w.Next();
        w.SelectProfile(w.MatchingProfiles()[0]); w.Next();
        w.SetDonor(File.ReadAllBytes(TestRoms.AsrockP1043!)); w.Next();
        w.SetIdentity(IdentityChoice.Generic); w.Next();
        var outPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".bin");
        try
        {
            var o = w.Build(outPath);
            Assert.True(o.Ok, o.Error);
            Assert.Equal(16 * 1024 * 1024, new FileInfo(outPath).Length);
            Assert.True(File.Exists(o.ReportPath!));
        }
        finally { File.Delete(outPath); if (File.Exists(outPath + ".report.md")) File.Delete(outPath + ".report.md"); }
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
