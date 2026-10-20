// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Board.Smbios;
using Am4ever.Core.Build;
using Am4ever.Core.Profile;
using Am4ever.Core.Rom;

namespace Am4ever.Cli.Commands;

/// <summary>Parsed arguments for <c>am4ever build</c>.</summary>
public sealed record BuildArgs(string OemPath, string DonorPath, string Profile, string OutPath, string? DmiPath, bool IUnderstandTheRisk);

/// <summary>Builds a modded BIOS from the user's dump, a donor ROM and a board profile.</summary>
public static class BuildCommand
{
    public static int Run(BuildArgs args, IConsoleIo io)
    {
        if (!File.Exists(args.OemPath)) return Fail(io, $"OEM dump not found: {args.OemPath}");
        if (!File.Exists(args.DonorPath)) return Fail(io, $"donor ROM not found: {args.DonorPath}");

        try
        {
            var oem = File.ReadAllBytes(args.OemPath);
            var donor = File.ReadAllBytes(args.DonorPath);
            var profile = LoadProfile(args.Profile);
            var adapter = BundledData.LoadAdapter(profile.Donor.Adapter);
            DmiIdentity? dmi = args.DmiPath is null ? null : DmiIdentity.FromJson(File.ReadAllBytes(args.DmiPath));

            var result = BuildPipeline.Build(donor, oem, profile, adapter, dmi);

            File.WriteAllBytes(args.OutPath, result.Image);
            var reportPath = args.OutPath + ".report.md";
            File.WriteAllText(reportPath, result.Report.ToMarkdown());
            io.Out.WriteLine($"wrote {args.OutPath} ({result.Image.Length} bytes)");
            io.Out.WriteLine($"wrote {reportPath}");
            io.Out.WriteLine("NOTE: this M3 image carries HP's configuration and identity but not yet the EC fan hand-off (fans run full speed until milestone 4).");
            return 0;
        }
        catch (Exception e) when (e is RomFormatException or RomEditException or IOException)
        {
            return Fail(io, e.Message);
        }
    }

    static BoardProfile LoadProfile(string profileArg) =>
        File.Exists(profileArg)
            ? ProfileLoader.LoadProfile(File.ReadAllBytes(profileArg))
            : BundledData.LoadProfile(profileArg);

    static int Fail(IConsoleIo io, string message)
    {
        io.Err.WriteLine($"am4ever build: {message}");
        return 1;
    }
}
