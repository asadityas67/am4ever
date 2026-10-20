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

    // A well-formed build command whose files do not exist: the risk gate fires before any file I/O,
    // so the refusal cases return 2; once past the gate the build fails on the missing dump (rc 1).
    static readonly string[] BuildArgs = { "build", "your.bin", "donor.bin", "--profile", "hp-285-g3-8434", "--out", "o.bin" };

    static string[] WithFlag => BuildArgs.Append("--i-understand-the-risk").ToArray();

    [Fact] public void Build_WithFlag_Proceeds() =>
        Assert.Equal(1, CliApp.Run(WithFlag, FakeIo.Create()));          // past the gate, then missing dump

    [Fact] public void Build_NonInteractiveWithoutFlag_Refuses()
    {
        var io = FakeIo.Create(stdinIsTerminal: false, stdin: "");
        Assert.Equal(2, CliApp.Run(BuildArgs, io));
        Assert.Contains("--i-understand-the-risk", io.ErrText);
    }

    [Fact] public void Build_InteractiveYes_Proceeds() =>
        Assert.Equal(1, CliApp.Run(BuildArgs, FakeIo.Create(stdinIsTerminal: true, stdin: "yes\n")));

    [Fact] public void Build_InteractiveAnythingElse_Refuses() =>
        Assert.Equal(2, CliApp.Run(BuildArgs, FakeIo.Create(stdinIsTerminal: true, stdin: "y\n")));

    [Fact] public void UnknownCommand_IsUsageError() =>
        Assert.Equal(64, CliApp.Run(new[] { "frobnicate" }, FakeIo.Create()));
}
