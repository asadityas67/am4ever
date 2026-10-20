// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Cli;
using Am4ever.Tests.Rom;

namespace Am4ever.Tests.Cli;

public class InspectCommandTests
{
    [Fact] public void Inspect_MissingFile_ReturnsOne() =>
        Assert.Equal(1, CliApp.Run(new[] { "inspect", "does-not-exist.bin" }, FakeIo.Create()));

    [Fact] public void Inspect_RandomBytes_ReportsNotValid()
    {
        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, new byte[1024]);
        var io = FakeIo.Create();
        Assert.Equal(1, CliApp.Run(new[] { "inspect", path }, io));
        Assert.Contains("not a valid", io.ErrText);
    }

    [Fact] public void Inspect_NoPath_IsUsageError() =>
        Assert.Equal(64, CliApp.Run(new[] { "inspect" }, FakeIo.Create()));

    [RomFact] public void Inspect_AsrockRom_ListsDirectories()
    {
        var io = FakeIo.Create();
        Assert.Equal(0, CliApp.Run(new[] { "inspect", TestRoms.AsrockP1043! }, io));
        Assert.Contains("Bios", io.OutText);
        Assert.Contains("16 MiB", io.OutText);
    }

    [RomFact] public void Inspect_HpRom_ShowsAgesaString()
    {
        var io = FakeIo.Create();
        Assert.Equal(0, CliApp.Run(new[] { "inspect", TestRoms.HpF65! }, io));
        Assert.Contains("PinnaclePI", io.OutText);
    }
}
