// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Cli;

namespace Am4ever.Tests.Cli;

public class BuildCommandTests
{
    [Fact] public void Build_WithoutRiskFlag_Refuses()
    {
        var io = FakeIo.Create();
        var rc = CliApp.Run(new[] { "build", "oem.bin", "donor.bin", "--profile", "hp-285-g3-8434", "--out", "o.bin" }, io);
        Assert.NotEqual(0, rc);
        Assert.Contains("risk", io.ErrText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact] public void Build_MissingArgs_UsageError()
    {
        var io = FakeIo.Create();
        Assert.Equal(64, CliApp.Run(new[] { "build" }, io));
    }
}
