// SPDX-License-Identifier: GPL-2.0-or-later
using System.Xml.Linq;

namespace Am4ever.Tests;

public class PublishSettingsTests
{
    [Theory]
    [InlineData("src/Am4ever.App/Properties/PublishProfiles/win-x64.pubxml")]
    [InlineData("src/Am4ever.App/Properties/PublishProfiles/linux-x64.pubxml")]
    [InlineData("src/Am4ever.Cli/Properties/PublishProfiles/win-x64.pubxml")]
    [InlineData("src/Am4ever.Cli/Properties/PublishProfiles/linux-x64.pubxml")]
    public void ProfileIsPortableSingleFile(string rel)
    {
        var x = XDocument.Load(RepoFiles.Root(rel.Split('/'))).Descendants()
            .GroupBy(e => e.Name.LocalName, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Value, StringComparer.Ordinal);
        Assert.Equal("true", x["PublishSingleFile"]);
        Assert.Equal("true", x["SelfContained"]);
        Assert.Equal("true", x["IncludeNativeLibrariesForSelfExtract"]);
        Assert.Equal("false", x["PublishTrimmed"]);
        Assert.Equal("embedded", x["DebugType"]);
    }
}
