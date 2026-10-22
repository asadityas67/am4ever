// SPDX-License-Identifier: GPL-2.0-or-later
using System.Linq;
using Am4ever.App.Lab;
using Am4ever.Core.Build;
using Am4ever.Core.Profile;
using Am4ever.Tests.Rom;

namespace Am4ever.Tests.Lab;

public class ProfileDiscoveryTests
{
    [RomFact] public void Suggest_IncludesGpioAndVerbForTheDonorFamilies()
    {
        var adapter = BundledData.LoadAdapter("asrock-ab350m-pro4-p10.43");
        var profile = BundledData.LoadProfile("hp-285-g3-8434");
        var oem = BoardAnalysis.Load(File.ReadAllBytes(TestRoms.HpF65!), profile.OemPeiFvOffset!.Value, 0);
        var donor = BoardAnalysis.Load(File.ReadAllBytes(TestRoms.AsrockP1043!), 0, adapter.Volumes.BootBlobOffset);
        var ops = ProfileDiscovery.Suggest(oem, donor, adapter);
        Assert.Contains(ops, o => o.Op == "port_gpio");
        Assert.Contains(ops, o => o.Op == "verb_table" && o.NewCodec is > 0);
        Assert.Contains(ops, o => o.Op == "neutralise_override");
    }
}
