// SPDX-License-Identifier: GPL-2.0-or-later
using System.Globalization;

namespace Am4ever.Emu;

/// <summary>What a CPM OEM PEIM did under one CPU family in the emulator.</summary>
public sealed record PeiResult(uint Entry, int PpiCount, int NotifyCount, IReadOnlyList<string> Tables, int WritesOutside, bool? OverrideOk);

/// <summary>Drives the native host to run a 32-bit CPM OEM PEIM under one CPU family.</summary>
public static class PeiEmulator
{
    public static string? HostPath => UcHost.HostPath;

    public static PeiResult Run(byte[] teImage, uint cpuid, int? overrideOffset = null)
    {
        var temp = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(temp, teImage);
            var args = new List<string> { "pei", "--te", temp, "--cpuid", $"0x{cpuid:X}" };
            if (overrideOffset is int off) { args.Add("--override"); args.Add($"0x{off:X}"); }
            return Parse(UcHost.Run(args.ToArray()));
        }
        finally { try { File.Delete(temp); } catch { } }
    }

    static PeiResult Parse(IReadOnlyList<string> lines)
    {
        uint entry = 0; int ppis = 0, notifies = 0, writes = 0; bool? overrideOk = null;
        var tables = new List<string>();
        foreach (var line in lines)
        {
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            var key = line[..eq]; var value = line[(eq + 1)..];
            switch (key)
            {
                case "entry": entry = EcEmulator.ParseHex(value); break;
                case "ppis": ppis = int.Parse(value, CultureInfo.InvariantCulture); break;
                case "notifies": notifies = int.Parse(value, CultureInfo.InvariantCulture); break;
                case "writes_outside": writes = int.Parse(value, CultureInfo.InvariantCulture); break;
                case "tables": if (value.Length > 0) tables.AddRange(value.Split(' ', StringSplitOptions.RemoveEmptyEntries)); break;
                default: if (key.StartsWith("override[", StringComparison.Ordinal)) overrideOk = value == "ok"; break;
            }
        }
        return new PeiResult(entry, ppis, notifies, tables, writes, overrideOk);
    }
}
