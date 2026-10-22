// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Build;
using Am4ever.Core.Profile;
using Am4ever.Core.Rom;

namespace Am4ever.App.Lab;

/// <summary>The outcome of a composer test-build: success, or the reason it stopped (no file written).</summary>
public sealed record ComposerTestResult(bool Ok, string? Error, string? Report);

/// <summary>
/// UI-free heart of the profile composer: holds the <see cref="ProfileDraft"/>, discovers suggested
/// operations, renders the live JSON, test-builds against the chosen bundled adapter, and imports an
/// existing profile. The Board Lab view is a thin shell over this. Identity extractors are board-
/// specific, so <see cref="IdentitySupported"/> gates whether <c>smbios_identity</c> can be authored.
/// </summary>
public sealed class ComposerCore
{
    // Core identity extractors that exist today; smbios_identity is only meaningful for these boards.
    static readonly HashSet<string> KnownExtractors = new(StringComparer.Ordinal) { "hp-8434" };

    byte[]? _oem;
    byte[]? _donor;
    DonorAdapter? _adapter;

    public ProfileDraft Draft { get; private set; } = new();

    public void SetOemDump(byte[] rom)
    {
        RomImage.Load(rom);                 // throws RomFormatException if not an 8/16/32 MiB AM4 image
        _oem = rom;
        Draft.RomSize = rom.Length;
    }

    public void SetAdapterId(string id)
    {
        _adapter = BundledData.LoadAdapter(id);
        Draft.AdapterId = id;
    }

    public void SetDonor(byte[] rom) => _donor = rom;

    public IReadOnlyList<OperationSpec> Discover()
    {
        if (_oem is null || _donor is null || _adapter is null)
            return Array.Empty<OperationSpec>();
        var oem = BoardAnalysis.Load(_oem, Draft.OemPeiFvOffset ?? 0, 0);
        var donor = BoardAnalysis.Load(_donor, 0, _adapter.Volumes.BootBlobOffset);
        return ProfileDiscovery.Suggest(oem, donor, _adapter);
    }

    public void AddOperation(OperationSpec op) => Draft.Operations.Add(op);
    public void RemoveOperation(int index) { if (index >= 0 && index < Draft.Operations.Count) Draft.Operations.RemoveAt(index); }

    public void MoveOperation(int from, int to)
    {
        if (from < 0 || from >= Draft.Operations.Count || to < 0 || to >= Draft.Operations.Count) return;
        var op = Draft.Operations[from];
        Draft.Operations.RemoveAt(from);
        Draft.Operations.Insert(to, op);
    }

    public byte[] ToJson() => ProfileWriter.Write(Draft.ToProfile());

    /// <summary>Replace the draft with a loaded profile. Parses first, so a bad file leaves the draft intact
    /// (the <see cref="RomFormatException"/> propagates for the caller to show).</summary>
    public void Import(byte[] json)
    {
        var profile = ProfileLoader.LoadProfile(json);   // throws on malformed/foreign JSON — draft untouched
        Draft = ProfileDraft.FromProfile(profile);
    }

    /// <summary>Build the current draft against the loaded OEM/donor + bundled adapter; writes the image to
    /// <paramref name="outPath"/> only on success. Never throws for a bad draft — returns the reason.</summary>
    public ComposerTestResult TestBuild(string? outPath = null, IProgress<BuildProgress>? progress = null)
    {
        if (_oem is null || _donor is null || _adapter is null)
            return new ComposerTestResult(false, "load your OEM dump, the donor, and choose an adapter first", null);
        try
        {
            var result = BuildPipeline.Build(_donor, _oem, Draft.ToProfile(), _adapter, null, progress);
            if (outPath is not null)
            {
                File.WriteAllBytes(outPath, result.Image);
                File.WriteAllText(outPath + ".report.md", result.Report.ToMarkdown());
            }
            return new ComposerTestResult(true, null, result.Report.ToMarkdown());
        }
        catch (Exception e) when (e is RomFormatException or RomEditException or IOException or UnauthorizedAccessException)
        {
            return new ComposerTestResult(false, e.Message, null);
        }
    }

    /// <summary>Whether a Core identity extractor exists for this board, so <c>smbios_identity</c> is meaningful.</summary>
    public bool IdentitySupported => Draft.IdentityExtractor is { } e && KnownExtractors.Contains(e);

    // --- table-edit gate: a warning must be acknowledged on every enable ---
    public Func<bool>? ConfirmTableEdit { get; set; }
    public bool TableEditEnabled { get; private set; }

    public bool EnableTableEdit()
    {
        var ok = ConfirmTableEdit?.Invoke() ?? false;   // warns every time it is called
        if (ok) TableEditEnabled = true;
        return ok;
    }

    public void DisableTableEdit() => TableEditEnabled = false;
}
