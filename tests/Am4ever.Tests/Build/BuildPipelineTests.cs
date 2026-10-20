// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Build;
using Am4ever.Core.Profile;
using Am4ever.Core.Rom;
using Am4ever.Tests.Profile;

namespace Am4ever.Tests.Build;

public class BuildPipelineTests
{
    [Fact] public void Build_WrongDonorHash_Throws()
    {
        var adapter = ProfileLoader.LoadAdapter(SampleJson.AsrockAdapter);   // expects a specific sha256
        var profile = ProfileLoader.LoadProfile(SampleJson.MinimalHpProfile);
        Assert.Throws<RomFormatException>(() =>
            BuildPipeline.Build(new byte[16 << 20], new byte[16 << 20], profile, adapter, null));
    }

    [Fact] public void BundledProfileAndAdapterLoad()
    {
        Assert.NotNull(BundledData.LoadProfile("hp-285-g3-8434"));
        Assert.NotNull(BundledData.LoadAdapter("asrock-ab350m-pro4-p10.43"));
    }
}
