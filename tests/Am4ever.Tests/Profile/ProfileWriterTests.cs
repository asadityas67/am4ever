// SPDX-License-Identifier: GPL-2.0-or-later
using System.Linq;
using Am4ever.Core.Build;
using Am4ever.Core.Profile;

namespace Am4ever.Tests.Profile;

public class ProfileWriterTests
{
    [Fact] public void Write_RoundTripsThroughLoader()
    {
        var original = BundledData.LoadProfile("hp-285-g3-8434");
        var json = ProfileWriter.Write(original);
        var reloaded = ProfileLoader.LoadProfile(json);
        // compare by shape, not record identity (record lists compare by reference)
        Assert.Equal(original.Operations.Select(o => o.Op), reloaded.Operations.Select(o => o.Op));
        Assert.Equal(original.Board.BoardId, reloaded.Board.BoardId);
        Assert.Equal(original.Donor.Adapter, reloaded.Donor.Adapter);
        // idempotent: writing the reloaded profile yields identical bytes
        Assert.Equal(json, ProfileWriter.Write(reloaded));
    }
}
