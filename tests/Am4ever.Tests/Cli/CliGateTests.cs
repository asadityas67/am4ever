// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Cli;
using Am4ever.Core.Safety;

namespace Am4ever.Tests.Cli;

public class CliGateTests
{
    [Fact] public void EveryRunPrintsBannerInRed()
    {
        var io = FakeIo.Create(color: true);
        CliApp.Run(new[] { "--version" }, io);
        Assert.Contains("\u001b[31m" + RiskWarning.Banner, io.ErrText);
    }

    [Fact] public void Build_WithFlag_Proceeds() =>
        Assert.Equal(0, CliApp.Run(new[] { "build", "--i-understand-the-risk" }, FakeIo.Create()));

    [Fact] public void Build_NonInteractiveWithoutFlag_Refuses()
    {
        var io = FakeIo.Create(stdinIsTerminal: false, stdin: "");
        Assert.Equal(2, CliApp.Run(new[] { "build" }, io));
        Assert.Contains("--i-understand-the-risk", io.ErrText);
    }

    [Fact] public void Build_InteractiveYes_Proceeds() =>
        Assert.Equal(0, CliApp.Run(new[] { "build" }, FakeIo.Create(stdinIsTerminal: true, stdin: "yes\n")));

    [Fact] public void Build_InteractiveAnythingElse_Refuses() =>
        Assert.Equal(2, CliApp.Run(new[] { "build" }, FakeIo.Create(stdinIsTerminal: true, stdin: "y\n")));

    [Fact] public void UnknownCommand_IsUsageError() =>
        Assert.Equal(64, CliApp.Run(new[] { "frobnicate" }, FakeIo.Create()));
}
