// SPDX-License-Identifier: GPL-2.0-or-later
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Am4ever.Emu;

/// <summary>The result of running a DXE driver against the simulated EC.</summary>
public sealed record EcResult(int Status, long Instructions, uint Lpc, IReadOnlyDictionary<int, byte> Ec);

/// <summary>
/// Drives the out-of-process native emulator host (<c>uc_host</c>) to run am4ever's EC-fan driver
/// against a simulated ACPI EC. The host runs in its own process because Unicorn cannot execute
/// inside a CET-enabled .NET process (see native/README.md).
/// </summary>
public static class EcEmulator
{
    static readonly string Rid = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win-x64" : "linux-x64";
    static readonly string Exe = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "uc_host.exe" : "uc_host";

    /// <summary>Resolved path of the native host, or null if it has not been built. Never throws.</summary>
    public static string? HostPath
    {
        get
        {
            var env = Environment.GetEnvironmentVariable("AM4EVER_UC_HOST");
            if (!string.IsNullOrEmpty(env) && File.Exists(env)) return env;
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            {
                var p = Path.Combine(dir.FullName, "native", "out", Rid, Exe);
                if (File.Exists(p)) return p;
            }
            return null;
        }
    }

    public static EcResult RunEcFan(byte[] pe, bool ecPresent = true, IReadOnlyDictionary<int, byte>? initialRegs = null)
    {
        var host = HostPath ?? throw new EmulatorException("native emulator host not built (native/build)");
        var temp = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(temp, pe);
            var psi = new ProcessStartInfo(host) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            psi.ArgumentList.Add("ecfan");
            psi.ArgumentList.Add("--pe");
            psi.ArgumentList.Add(temp);
            if (!ecPresent) psi.ArgumentList.Add("--no-ec");
            foreach (var (reg, val) in initialRegs ?? new Dictionary<int, byte>())
            {
                psi.ArgumentList.Add("--reg");
                psi.ArgumentList.Add($"{reg:X}={val:X}");
            }

            using var proc = Process.Start(psi) ?? throw new EmulatorException($"could not start {host}");
            string stdout = proc.StandardOutput.ReadToEnd();
            string stderr = proc.StandardError.ReadToEnd();
            if (!proc.WaitForExit(30_000)) { try { proc.Kill(true); } catch { } throw new EmulatorException("emulator host timed out"); }
            if (proc.ExitCode != 0)
                throw new EmulatorException($"emulator host exited {proc.ExitCode}: {stderr.Trim()}");
            return Parse(stdout);
        }
        finally { try { File.Delete(temp); } catch { } }
    }

    static EcResult Parse(string stdout)
    {
        int? status = null; long insns = 0; uint lpc = 0;
        var ec = new Dictionary<int, byte>();
        foreach (var raw in stdout.Split('\n'))
        {
            var line = raw.Trim();
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            var key = line[..eq];
            var value = line[(eq + 1)..];
            if (key == "status") status = int.Parse(value, CultureInfo.InvariantCulture);
            else if (key == "insns") insns = long.Parse(value, CultureInfo.InvariantCulture);
            else if (key == "lpc") lpc = ParseHex(value);
            else if (key.StartsWith("ec[", StringComparison.Ordinal) && key.EndsWith(']'))
                ec[(int)ParseHex(key[3..^1])] = (byte)ParseHex(value);
        }
        if (status is null)
            throw new EmulatorException($"emulator host output had no status line:\n{stdout}");
        return new EcResult(status.Value, insns, lpc, ec);
    }

    static uint ParseHex(string s) =>
        s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? uint.Parse(s[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
            : uint.Parse(s, CultureInfo.InvariantCulture);
}
