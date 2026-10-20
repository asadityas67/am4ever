// SPDX-License-Identifier: GPL-2.0-or-later
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Am4ever.Cli;

namespace Am4ever.Tests;

/// <summary>Regression tests for the milestone 1 whole-branch review findings.</summary>
public class ReviewFixTests
{
    [Fact] public void ShellScriptsAreExecutableInGit()
    {
        var psi = new ProcessStartInfo("git", "ls-files -s -- scripts")
        { WorkingDirectory = RepoFiles.Root(), RedirectStandardOutput = true, UseShellExecute = false };
        using var p = Process.Start(psi)!;
        var lines = p.StandardOutput.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(l => l.EndsWith(".sh")).ToList();
        p.WaitForExit();
        Assert.NotEmpty(lines);
        Assert.All(lines, l => Assert.StartsWith("100755", l));
    }

    // Packages with known advisories that Avalonia pulls in transitively, and their first patched version.
    [Theory]
    [InlineData("Tmds.DBus.Protocol", "0.21.3")]   // GHSA-xrw6-gwf8-vvr9 (High)
    public void TransitivePackageIsAtLeastPatchedVersion(string package, string patched)
    {
        var assets = JsonDocument.Parse(File.ReadAllText(RepoFiles.Root("src", "Am4ever.App", "obj", "project.assets.json")));
        var resolved = assets.RootElement.GetProperty("libraries").EnumerateObject()
            .Select(lib => lib.Name.Split('/'))
            .Where(parts => parts[0] == package)
            .Select(parts => Version.Parse(parts[1]))
            .ToList();
        Assert.NotEmpty(resolved);
        Assert.All(resolved, v => Assert.True(v >= Version.Parse(patched), $"{package} {v} < {patched}"));
    }

    [Fact] public void BinaryUnderDocsWithFirmwareSignatureIsAViolation() =>
        Assert.True(RepoFiles.ViolatesFirmwarePolicy("docs/blob.P10",
            Encoding.ASCII.GetBytes("\0\0\0\0_F" + "VH\0\0")));

    [Fact] public void BinaryNamedMarkdownWithFirmwareSignatureIsAViolation() =>
        Assert.True(RepoFiles.ViolatesFirmwarePolicy("notes.md",
            Encoding.ASCII.GetBytes("$P" + "SP\0\0\0\u0001")));

    [Fact] public void TextDocMentioningSignaturesIsAllowed() =>
        Assert.False(RepoFiles.ViolatesFirmwarePolicy("docs/design.md",
            Encoding.ASCII.GetBytes("PSP directories ($P" + "SP, $B" + "HD) are parsed")));

    [Fact] public void SourceFileWithSignatureIsAViolation() =>
        Assert.True(RepoFiles.ViolatesFirmwarePolicy("src/x.cs",
            Encoding.ASCII.GetBytes("var s = \"_F" + "VH\";")));

    [Theory]
    [InlineData(false, false, true, true)]    // terminal, colour allowed, VT works
    [InlineData(false, false, false, false)]  // classic console without VT: no escapes
    [InlineData(true, false, true, false)]    // stderr redirected
    [InlineData(false, true, true, false)]    // NO_COLOR set
    public void ColourOnlyWhenTerminalSupportsIt(bool errRedirected, bool noColor, bool vt, bool expected) =>
        Assert.Equal(expected, SystemConsoleIo.DecideColor(errRedirected, noColor, vt));

    [Fact] public void ReadmeDocumentsReduceMotionOverride() =>
        Assert.Contains("AM4EVER_REDUCE_MOTION=1", File.ReadAllText(RepoFiles.Root("README.md")));
}
