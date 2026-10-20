// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Profile;
using Am4ever.Core.Rom;

namespace Am4ever.Tests.Profile;

public class ProfileLoaderTests
{
    [Fact] public void LoadProfile_ParsesSchemaAndOperations()
    {
        var p = ProfileLoader.LoadProfile(SampleJson.MinimalHpProfile);
        Assert.Equal("am4ever.profile/1", p.Schema);
        Assert.Equal("8434", p.Board.BoardId);
        Assert.Contains(p.Operations, o => o.Op == "port_gpio");
    }

    [Fact] public void LoadProfile_WrongSchema_Throws() =>
        Assert.Throws<RomFormatException>(() =>
            ProfileLoader.LoadProfile(SampleJson.WithSchema("am4ever.profile/999")));

    [Fact] public void LoadProfile_RejectsOversizeByteString() =>
        Assert.Throws<RomFormatException>(() =>
            ProfileLoader.LoadProfile(SampleJson.WithBytePatch(expectedHexBytes: 128)));

    [Fact] public void LoadAdapter_ParsesModulesAndOverrides()
    {
        var a = ProfileLoader.LoadAdapter(SampleJson.AsrockAdapter);
        Assert.Equal("asrock-ab350m-pro4-p10.43", a.Id);
        Assert.NotEmpty(a.Overrides);
        Assert.Equal(0xE02000, a.Volumes.BootBlobOffset);
    }
}
