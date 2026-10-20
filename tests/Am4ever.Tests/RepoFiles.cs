// SPDX-License-Identifier: GPL-2.0-or-later
using System.Diagnostics;
using System.Text;

namespace Am4ever.Tests;

/// <summary>Helpers for tests that inspect the repository itself.</summary>
public static class RepoFiles
{
    static readonly Lazy<string> RootDir = new(() =>
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "am4ever.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("am4ever.sln not found above " + AppContext.BaseDirectory);
    });

    public static string Root(params string[] parts) => Path.Combine(new[] { RootDir.Value }.Concat(parts).ToArray());

    /// <summary>Absolute paths of every file git tracks or has staged.</summary>
    public static IReadOnlyList<string> Tracked()
    {
        var psi = new ProcessStartInfo("git", "ls-files -z")
        {
            WorkingDirectory = RootDir.Value,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        using var p = Process.Start(psi)!;
        var output = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0) throw new InvalidOperationException("git ls-files failed");
        return output.Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Select(rel => Path.Combine(RootDir.Value, rel.Replace('/', Path.DirectorySeparatorChar)))
            .Where(File.Exists)
            .ToList();
    }

    public static bool IsDoc(string path)
    {
        var rel = Path.GetRelativePath(RootDir.Value, path).Replace('\\', '/');
        return rel.StartsWith("docs/", StringComparison.Ordinal) || rel.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>True if a tracked file at <paramref name="relPath"/> with these bytes breaks the no-firmware rule.</summary>
    public static bool ViolatesFirmwarePolicy(string relPath, byte[] bytes)
    {
        var rel = relPath.Replace('\\', '/');
        var isDoc = rel.StartsWith("docs/", StringComparison.Ordinal) || rel.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
        // Docs may name the signatures in prose, but only text is exempt: firmware always contains NUL bytes.
        if (isDoc && bytes.AsSpan().IndexOf((byte)0) < 0)
            return false;
        return FirmwareSignatures.Any(n => bytes.AsSpan().IndexOf(Encoding.ASCII.GetBytes(n)) >= 0);
    }

    static readonly string[] FirmwareSignatures =
        { "_F" + "VH", "$P" + "SP", "$B" + "HD", "$P" + "L2", "$B" + "L2" };

    public static bool ContainsAny(string path, params string[] needles)
    {
        var bytes = File.ReadAllBytes(path);
        return needles.Any(n => bytes.AsSpan().IndexOf(Encoding.ASCII.GetBytes(n)) >= 0);
    }
}
