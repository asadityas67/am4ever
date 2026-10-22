// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.App.Feedback;
using Am4ever.App.Wizard;
using Am4ever.Core.Build;

namespace Am4ever.Tests.Wizard;

public class WizardToastsTests
{
    [Fact] public void ForBuild_SuccessAndFailure()
    {
        Assert.Equal(ToastKind.Success, WizardToasts.ForBuild(new BuildOutcome(true, null, "x.report.md")).Kind);
        var fail = WizardToasts.ForBuild(new BuildOutcome(false, "donor sha mismatch", null));
        Assert.Equal(ToastKind.Error, fail.Kind);
        Assert.Contains("donor sha mismatch", fail.Message);
    }

    [Fact] public void ForDump_StatesSizeInMiB()
    {
        var (msg, kind) = WizardToasts.ForDump(16 << 20);
        Assert.Equal(ToastKind.Success, kind);
        Assert.Contains("16 MiB", msg);
    }

    [Fact] public void ForProfile_NamesTheBoard()
    {
        var p = BundledData.LoadProfile("hp-285-g3-8434");
        Assert.Contains("285", WizardToasts.ForProfile(p).Message);
    }
}
