// SPDX-License-Identifier: GPL-2.0-or-later
using System.Linq;
using Am4ever.App.Lab;
using Am4ever.Core.Build;
using Am4ever.Tests.Rom;

namespace Am4ever.Tests.Lab;

public class AdapterDetectorTests
{
    [Fact] public void Detect_NonDonor_SetsShaLeavesVolumesBlank()
    {
        var d = AdapterDetector.Detect(new byte[16 * 1024 * 1024]);
        Assert.False(string.IsNullOrEmpty(d.Sha256));
        Assert.Null(d.DxeFileGuid);              // nothing found → blank, not guessed
        Assert.Empty(d.Overrides);
    }

    [RomFact] public void Detect_RealAsrockDonor_MatchesBundledAdapter()
    {
        var want = BundledData.LoadAdapter("asrock-ab350m-pro4-p10.43");
        var got = AdapterDetector.Detect(File.ReadAllBytes(TestRoms.AsrockP1043!)).ToAdapter();
        Assert.Equal(want.Identity.Sha256, got.Identity.Sha256);
        Assert.Equal(want.Volumes.DxeFileGuid, got.Volumes.DxeFileGuid);
        Assert.Equal(want.Volumes.BootBlobOffset, got.Volumes.BootBlobOffset);
        Assert.Contains(got.Modules, m => m.Name.StartsWith("AmdCpmOem"));
    }
}
