// SPDX-License-Identifier: GPL-2.0-or-later
using System.Reflection;
using Am4ever.Core.Safety;

namespace Am4ever.Cli;

/// <summary>The <c>am4ever</c> command line.</summary>
public static class CliApp
{
    public const int ExitOk = 0;
    public const int ExitRiskRefused = 2;
    public const int ExitUsage = 64;

    const string RiskFlag = "--i-understand-the-risk";
    const string Red = "\u001b[31m";
    const string Reset = "\u001b[0m";

    public static int Run(string[] args, IConsoleIo io)
    {
        PrintWarning(io);

        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            PrintUsage(io.Out);
            return ExitOk;
        }

        switch (args[0])
        {
            case "--version":
                io.Out.WriteLine($"am4ever {Version}");
                return ExitOk;

            case "build":
                var flags = args.Skip(1).ToList();
                var unknown = flags.Where(f => f != RiskFlag).ToList();
                if (unknown.Count > 0)
                {
                    io.Err.WriteLine($"am4ever build: unknown option {unknown[0]}");
                    PrintUsage(io.Err);
                    return ExitUsage;
                }
                var ack = RiskAcknowledgement.NewForThisLaunch();
                if (flags.Contains(RiskFlag) || AskInteractively(io))
                    ack.Accept();
                if (!ack.Accepted)
                {
                    io.Err.WriteLine($"Refused: you must accept the risk. Pass {RiskFlag} or answer 'yes' at the prompt.");
                    return ExitRiskRefused;
                }
                io.Out.WriteLine("build: not implemented yet (milestone 3)");
                return ExitOk;

            default:
                io.Err.WriteLine($"am4ever: unknown command '{args[0]}'");
                PrintUsage(io.Err);
                return ExitUsage;
        }
    }

    static bool AskInteractively(IConsoleIo io)
    {
        if (!io.StdinIsTerminal)
            return false;              // scripts and CI: never wait for input
        io.Err.Write("Type 'yes' to accept the risk and continue: ");
        var line = io.In.ReadLine();
        return string.Equals(line?.Trim(), "yes", StringComparison.OrdinalIgnoreCase);
    }

    static void PrintWarning(IConsoleIo io)
    {
        var lines = new[] { RiskWarning.Banner }.Concat(RiskWarning.Details);
        foreach (var line in lines)
            io.Err.WriteLine(io.SupportsColor ? Red + line + Reset : line);
        io.Err.WriteLine();
    }

    static void PrintUsage(TextWriter w)
    {
        w.WriteLine("usage: am4ever <command> [options]");
        w.WriteLine();
        w.WriteLine("commands:");
        w.WriteLine($"  build {RiskFlag}   build a modded BIOS (milestone 3)");
        w.WriteLine("  --version                       print the version");
        w.WriteLine("  --help                          print this help");
    }

    static string Version =>
        typeof(CliApp).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown";
}
