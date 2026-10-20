// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Safety;

namespace Am4ever.Tests.Core;

public class RiskWarningTests
{
    [Fact] public void BannerIsVerbatim() => Assert.Equal(
        "WARNING: You are responsible for what you do. Flashing a modified BIOS can permanently kill your PC if you are not careful.",
        RiskWarning.Banner);

    [Fact] public void DetailsCoverWarrantyBackupProgrammerAffiliation()
    {
        var d = string.Join("\n", RiskWarning.Details);
        Assert.Contains("no warranty", d);
        Assert.Contains("GPL-2.0", d);
        Assert.Contains("backup", d);
        Assert.Contains("programmer", d);
        Assert.Contains(RiskWarning.NotAffiliated, d);
    }

    [Fact] public void LabelsAreVerbatim()
    {
        Assert.Equal("I understand and accept the risk", RiskWarning.AcceptLabel);
        Assert.Equal("Continue", RiskWarning.ContinueLabel);
        Assert.Equal("Exit", RiskWarning.ExitLabel);
    }

    [Fact] public void EveryLaunchStartsUnaccepted()
    {
        var first = RiskAcknowledgement.NewForThisLaunch(); first.Accept();
        Assert.False(RiskAcknowledgement.NewForThisLaunch().Accepted);
    }
}
