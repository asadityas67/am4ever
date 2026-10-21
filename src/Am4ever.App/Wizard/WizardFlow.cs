// SPDX-License-Identifier: GPL-2.0-or-later
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

    public void SetDump(byte[] primary, byte[]? secondRead = null)
    {
        PrimaryDump = primary;
        SecondDump = secondRead;
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
        if (PrimaryDump is null) { reason = "pick your BIOS dump"; return false; }
        reason = null; return true;   // Task 2 adds size / AM4 / blank / two-dump validation
    }
    bool CanAdvanceProfile(out string? reason) { reason = "no profile selected"; return false; }
    bool CanAdvanceDonor(out string? reason) { reason = "donor not verified"; return false; }
    bool CanAdvanceIdentity(out string? reason) { reason = null; return true; }
    bool CanAdvanceBuild(out string? reason) { reason = "build not completed"; return false; }
}
