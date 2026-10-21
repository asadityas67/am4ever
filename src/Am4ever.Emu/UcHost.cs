// SPDX-License-Identifier: GPL-2.0-or-later
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Am4ever.Emu;

/// <summary>Locates and runs the out-of-process native emulator host (shared by EcEmulator and PeiEmulator).</summary>
public static class UcHost
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

    /// <summary>Run the host with the given arguments; return stdout lines. Throws <see cref="EmulatorException"/> on failure.</summary>
    public static IReadOnlyList<string> Run(params string[] args)
    {
        var host = HostPath ?? throw new EmulatorException("native emulator host not built (native/build)");
        var psi = new ProcessStartInfo(host) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var proc = Process.Start(psi) ?? throw new EmulatorException($"could not start {host}");
        var outTask = proc.StandardOutput.ReadToEndAsync();
        var errTask = proc.StandardError.ReadToEndAsync();
        if (!proc.WaitForExit(30_000)) { try { proc.Kill(true); } catch { } throw new EmulatorException("emulator host timed out"); }
        string stdout = outTask.GetAwaiter().GetResult();
        string stderr = errTask.GetAwaiter().GetResult();
        if (proc.ExitCode != 0)
            throw new EmulatorException($"emulator host exited {proc.ExitCode}: {stderr.Trim()}");
        return stdout.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }
}
