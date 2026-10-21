// SPDX-License-Identifier: GPL-2.0-or-later
using System.Globalization;

namespace Am4ever.Emu;

/// <summary>The result of running a DXE driver against the simulated EC.</summary>
public sealed record EcResult(int Status, long Instructions, uint Lpc, IReadOnlyDictionary<int, byte> Ec);

/// <summary>Drives the native host (<see cref="UcHost"/>) to run am4ever's EC-fan driver against a simulated EC.</summary>
public static class EcEmulator
{
    /// <summary>The native host path, or null if not built (used to skip emulator tests). Never throws.</summary>
    public static string? HostPath => UcHost.HostPath;

    public static EcResult RunEcFan(byte[] pe, bool ecPresent = true, IReadOnlyDictionary<int, byte>? initialRegs = null)
    {
        var temp = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(temp, pe);
            var args = new List<string> { "ecfan", "--pe", temp };
            if (!ecPresent) args.Add("--no-ec");
            foreach (var (reg, val) in initialRegs ?? new Dictionary<int, byte>()) { args.Add("--reg"); args.Add($"{reg:X}={val:X}"); }
            return Parse(UcHost.Run(args.ToArray()));
        }
        finally { try { File.Delete(temp); } catch { } }
    }

    static EcResult Parse(IReadOnlyList<string> lines)
    {
        int? status = null; long insns = 0; uint lpc = 0;
        var ec = new Dictionary<int, byte>();
        foreach (var line in lines)
        {
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            var key = line[..eq]; var value = line[(eq + 1)..];
            if (key == "status") status = int.Parse(value, CultureInfo.InvariantCulture);
            else if (key == "insns") insns = long.Parse(value, CultureInfo.InvariantCulture);
            else if (key == "lpc") lpc = ParseHex(value);
            else if (key.StartsWith("ec[", StringComparison.Ordinal) && key.EndsWith(']'))
                ec[(int)ParseHex(key[3..^1])] = (byte)ParseHex(value);
        }
        if (status is null) throw new EmulatorException($"emulator host output had no status line:\n{string.Join('\n', lines)}");
        return new EcResult(status.Value, insns, lpc, ec);
    }

    internal static uint ParseHex(string s) =>
        s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? uint.Parse(s[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
            : uint.Parse(s, CultureInfo.InvariantCulture);
}
