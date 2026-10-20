// SPDX-License-Identifier: GPL-2.0-or-later
namespace Am4ever.Tests;

public class RepoHygieneTests
{
    // Built from pieces so this source file doesn't itself contain the signatures.
    static readonly string[] FirmwareSignatures =
        { "_F" + "VH", "$P" + "SP", "$B" + "HD", "$P" + "L2", "$B" + "L2" };

    [Fact] public void NoTrackedFileExceeds256KiB() =>
        Assert.DoesNotContain(RepoFiles.Tracked(), f => new FileInfo(f).Length > 262144);

    [Fact] public void NoTrackedNonDocFileContainsFirmwareSignatures() =>
        Assert.DoesNotContain(RepoFiles.Tracked(), f => !RepoFiles.IsDoc(f) && RepoFiles.ContainsAny(f, FirmwareSignatures));

    [Fact] public void EveryCsFileHasSpdxHeader() =>
        Assert.DoesNotContain(RepoFiles.Tracked(), f => f.EndsWith(".cs")
            && !File.ReadLines(f).First().Contains("SPDX-License-Identifier: GPL-2.0-or-later"));

    [Fact] public void ReadmeStatesNotAffiliated() =>
        Assert.Contains("am4ever is not affiliated with AMD, AMI, ASRock, Biostar, HP, Dell or Lenovo.",
            File.ReadAllText(RepoFiles.Root("README.md")));

    [Fact] public void IssueTemplateAsksForBuildReportNotRoms()
    {
        var t = File.ReadAllText(RepoFiles.Root(".github", "ISSUE_TEMPLATE", "bug_report.md"));
        Assert.Contains("build report", t);
        Assert.Contains("Do not attach BIOS/ROM images", t);
    }

    [Fact] public void ContributingForbidsRoms() =>
        Assert.Contains("Never attach or commit BIOS/ROM images", File.ReadAllText(RepoFiles.Root("CONTRIBUTING.md")));
}
