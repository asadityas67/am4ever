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

            case "inspect":
                if (args.Length < 2)
                {
                    io.Err.WriteLine("am4ever inspect: a ROM file path is required");
                    PrintUsage(io.Err);
                    return ExitUsage;
                }
                return Commands.InspectCommand.Run(args[1], io);

            case "build":
                var parsed = ParseBuildArgs(args.Skip(1).ToArray(), io);
                if (parsed is null)
                    return ExitUsage;
                var ack = RiskAcknowledgement.NewForThisLaunch();
                if (parsed.IUnderstandTheRisk || AskInteractively(io))
                    ack.Accept();
                if (!ack.Accepted)
                {
                    io.Err.WriteLine($"Refused: you must accept the risk. Pass {RiskFlag} or answer 'yes' at the prompt.");
                    return ExitRiskRefused;
                }
                return Commands.BuildCommand.Run(parsed, io);

            default:
                io.Err.WriteLine($"am4ever: unknown command '{args[0]}'");
                PrintUsage(io.Err);
                return ExitUsage;
        }
    }

    static Commands.BuildArgs? ParseBuildArgs(string[] a, IConsoleIo io)
    {
        string? profile = null, outPath = null, dmi = null;
        bool risk = false;
        var positionals = new List<string>();
        for (int i = 0; i < a.Length; i++)
        {
            switch (a[i])
            {
                case RiskFlag: risk = true; break;
                case "--profile": if (++i >= a.Length) return BuildUsage(io, "--profile needs a value"); profile = a[i]; break;
                case "--out": if (++i >= a.Length) return BuildUsage(io, "--out needs a value"); outPath = a[i]; break;
                case "--dmi": if (++i >= a.Length) return BuildUsage(io, "--dmi needs a value"); dmi = a[i]; break;
                default:
                    if (a[i].StartsWith("--", StringComparison.Ordinal)) return BuildUsage(io, $"unknown option {a[i]}");
                    positionals.Add(a[i]);
                    break;
            }
        }
        if (positionals.Count > 2) return BuildUsage(io, "too many arguments");
        if (positionals.Count < 2 || profile is null || outPath is null)
            return BuildUsage(io, "build needs <your-dump> <donor> --profile <id|file> --out <file>");
        return new Commands.BuildArgs(positionals[0], positionals[1], profile, outPath, dmi, risk);
    }

    static Commands.BuildArgs? BuildUsage(IConsoleIo io, string message)
    {
        io.Err.WriteLine($"am4ever build: {message}");
        PrintUsage(io.Err);
        return null;
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
        w.WriteLine("  inspect <rom>                   report a ROM's size, AMD directories and AGESA (read-only)");
        w.WriteLine($"  build <your-dump> <donor> --profile <id|file> --out <file> [--dmi <file>] {RiskFlag}");
        w.WriteLine("                                  build a modded BIOS from your dump, a donor ROM and a profile");
        w.WriteLine("  --version                       print the version");
        w.WriteLine("  --help                          print this help");
    }

    static string Version =>
        typeof(CliApp).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown";
}
