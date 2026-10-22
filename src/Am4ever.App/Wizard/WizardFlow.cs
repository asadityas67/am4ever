// SPDX-License-Identifier: GPL-2.0-or-later
using System.Security.Cryptography;
using Am4ever.Core.Board.Smbios;
using Am4ever.Core.Build;
using Am4ever.Core.Profile;
using Am4ever.Core.Rom;
using Am4ever.Core.Rom.Amd;

namespace Am4ever.App.Wizard;

public enum WizardStep { Dump, Profile, Donor, Identity, Build, Flash }

/// <summary>
/// The beginner wizard's UI-free state machine. Holds the build inputs and gates each step; the
/// Avalonia view drives it. No Avalonia dependency, so it is unit-tested directly. Steps are filled
/// in by milestone tasks 2–4; navigation and the Dump gate are here.
/// </summary>
public sealed class WizardFlow
{
    public WizardStep Step { get; private set; } = WizardStep.Dump;

    public byte[]? PrimaryDump { get; private set; }
    public byte[]? SecondDump { get; private set; }
    public BoardProfile? SelectedProfile { get; private set; }
    DonorAdapter? _adapter;
    byte[]? _donor;

    /// <summary>Bundled profiles whose ROM size matches the dump (shown at step 2).</summary>
    public IReadOnlyList<BoardProfile> MatchingProfiles()
    {
        if (PrimaryDump is null) return Array.Empty<BoardProfile>();
        var list = new List<BoardProfile>();
        foreach (var id in BundledData.ProfileIds)
            try { var p = BundledData.LoadProfile(id); if (p.Board.Match.RomSize == PrimaryDump.Length) list.Add(p); }
            catch { /* skip an unreadable bundled profile */ }
        return list;
    }

    public void SelectProfile(BoardProfile profile)
    {
        SelectedProfile = profile;
        _adapter = BundledData.LoadAdapter(profile.Donor.Adapter);
        _built = false;
    }

    public void SetDonor(byte[] donor) { _donor = donor; _built = false; }

    /// <summary>A plain-words summary of a profile for the step-2 list: model, authors, and untested/inferred items (§3/§6).</summary>
    public static string ProfileSummary(BoardProfile p)
    {
        var lines = new List<string> { $"{p.Board.Vendor} {p.Board.Model} ({p.Board.BoardId})" };
        if (p.Authors.Count > 0) lines.Add("by " + string.Join(", ", p.Authors));
        var tested = p.Status.Tested ?? Array.Empty<TestedEntry>();
        lines.Add(tested.Count > 0 ? "Tested: " + string.Join("; ", tested.Select(t => $"{t.Cpu} ({t.Result})")) : "Tested: none yet");
        foreach (var u in p.Status.Untested ?? Array.Empty<string>()) lines.Add("⚠ untested/inferred: " + u);
        return string.Join("\n", lines);
    }

    public IdentityChoice Identity { get; private set; } = IdentityChoice.Generic;
    DmiIdentity? _dmi;
    bool _built;

    public void SetIdentity(IdentityChoice choice, byte[]? dmiJson = null)
    {
        Identity = choice;
        _dmi = choice == IdentityChoice.FromDmiFile && dmiJson is not null ? DmiIdentity.FromJson(dmiJson) : null;
        _built = false;
    }

    /// <summary>Build the modded image, writing it and its report only on success (no override on failure).</summary>
    public BuildOutcome Build(string outPath, IProgress<BuildProgress>? progress = null)
    {
        if (PrimaryDump is null || _donor is null || SelectedProfile is null || _adapter is null)
            return new BuildOutcome(false, "the wizard is missing an input (dump, profile or donor)", null);
        try
        {
            var result = BuildPipeline.Build(_donor, PrimaryDump, SelectedProfile, _adapter, _dmi, progress);
            File.WriteAllBytes(outPath, result.Image);
            var reportPath = outPath + ".report.md";
            File.WriteAllText(reportPath, result.Report.ToMarkdown());
            _built = true;
            return new BuildOutcome(true, null, reportPath);
        }
        catch (Exception e) when (e is RomFormatException or RomEditException or IOException or UnauthorizedAccessException)
        {
            return new BuildOutcome(false, e.Message, null);
        }
    }

    public void SetDump(byte[] primary, byte[]? secondRead = null)
    {
        PrimaryDump = primary;
        SecondDump = secondRead;
        _built = false;                 // inputs changed: any prior build is stale
    }

    /// <summary>Whether the current step's inputs are complete and valid; <paramref name="reason"/> explains a block.</summary>
    public bool CanAdvance(out string? reason)
    {
        reason = null;
        return Step switch
        {
            WizardStep.Dump => CanAdvanceDump(out reason),
            WizardStep.Profile => CanAdvanceProfile(out reason),
            WizardStep.Donor => CanAdvanceDonor(out reason),
            WizardStep.Identity => CanAdvanceIdentity(out reason),
            WizardStep.Build => CanAdvanceBuild(out reason),
            _ => true,
        };
    }

    public bool Next()
    {
        if (Step == WizardStep.Flash || !CanAdvance(out _)) return false;
        Step = (WizardStep)((int)Step + 1);
        return true;
    }

    public bool Back()
    {
        if (Step == WizardStep.Dump) return false;
        Step = (WizardStep)((int)Step - 1);
        return true;
    }

    // Per-step gates; tasks 2–4 replace the placeholder bodies with real validation.
    bool CanAdvanceDump(out string? reason)
    {
        reason = null;
        if (PrimaryDump is null) { reason = "pick your BIOS dump"; return false; }
        RomImage rom;
        try { rom = RomImage.Load(PrimaryDump); }
        catch (RomFormatException e) { reason = e.Message; return false; }
        if (SecondDump is not null && !PrimaryDump.AsSpan().SequenceEqual(SecondDump))
        { reason = "the two reads of the chip do not match byte for byte — re-read the chip"; return false; }
        if (PrimaryDump.AsSpan().IndexOfAnyExcept(PrimaryDump[0]) < 0)
        { reason = "the dump is blank/erased (all 0x" + PrimaryDump[0].ToString("X2") + ")"; return false; }
        if (AmdDirectory.FindAll(rom).Count == 0)
        { reason = "not an AMD AM4 image (no PSP/BIOS directories found)"; return false; }
        return true;
    }
    bool CanAdvanceProfile(out string? reason)
    {
        if (SelectedProfile is null) { reason = "select a board profile"; return false; }
        reason = null; return true;
    }

    bool CanAdvanceDonor(out string? reason)
    {
        reason = null;
        if (_donor is null) { reason = "load the donor BIOS file"; return false; }
        var expected = _adapter?.Identity.Sha256;
        if (string.IsNullOrEmpty(expected)) { reason = "the profile's donor adapter has no SHA-256 to verify against"; return false; }
        var actual = Convert.ToHexString(SHA256.HashData(_donor)).ToLowerInvariant();
        if (!actual.StartsWith(expected.ToLowerInvariant(), StringComparison.Ordinal))
        { reason = $"donor SHA-256 {actual[..16]}… does not match the profile ({expected[..Math.Min(16, expected.Length)]}…)"; return false; }
        return true;
    }
    bool CanAdvanceIdentity(out string? reason) { reason = null; return true; }
    bool CanAdvanceBuild(out string? reason)
    {
        if (!_built) { reason = "the build and its checks have not completed successfully"; return false; }
        reason = null; return true;
    }
}

public enum IdentityChoice { Generic, FromDmiFile }

/// <summary>The result of the wizard's build step.</summary>
public sealed record BuildOutcome(bool Ok, string? Error, string? ReportPath);
