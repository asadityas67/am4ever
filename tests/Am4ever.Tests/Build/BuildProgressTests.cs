// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Build;
using Am4ever.Tests.Rom;

namespace Am4ever.Tests.Build;

public class BuildProgressTests
{
    [RomFact] public void Build_ReportsMonotonicProgressEndingAtTotal()
    {
        var oem = File.ReadAllBytes(TestRoms.HpF65!);
        var donor = File.ReadAllBytes(TestRoms.AsrockP1043!);
        var items = new List<BuildProgress>();
        BuildPipeline.Build(donor, oem, BundledData.LoadProfile("hp-285-g3-8434"),
            BundledData.LoadAdapter("asrock-ab350m-pro4-p10.43"), null,
            new SyncProgress<BuildProgress>(items.Add));

        Assert.NotEmpty(items);
        Assert.All(items, p => Assert.False(string.IsNullOrWhiteSpace(p.Phase)));
        Assert.All(items, p => Assert.InRange(p.Done, 1, p.Total));
        for (int i = 1; i < items.Count; i++) Assert.True(items[i].Done >= items[i - 1].Done);
        Assert.Equal(items[^1].Total, items[^1].Done);        // ends full
    }
}
