// SPDX-License-Identifier: GPL-2.0-or-later
using System.Linq;
using Am4ever.Core.Build;
using Am4ever.Core.Profile;

namespace Am4ever.Tests.Profile;

public class AdapterWriterTests
{
    [Fact] public void Write_RoundTripsThroughLoader()
    {
        var original = BundledData.LoadAdapter("asrock-ab350m-pro4-p10.43");
        var json = AdapterWriter.Write(original);
        var reloaded = ProfileLoader.LoadAdapter(json);
        Assert.Equal(original.Id, reloaded.Id);
        Assert.Equal(original.Identity.Sha256, reloaded.Identity.Sha256);
        Assert.Equal(original.Overrides.Select(o => o.Offset), reloaded.Overrides.Select(o => o.Offset));
        Assert.Equal(original.Volumes.DxeFileGuid, reloaded.Volumes.DxeFileGuid);
        Assert.Equal(json, AdapterWriter.Write(reloaded));   // idempotent
    }
}
