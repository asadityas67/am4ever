// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.App.Feedback;
using Am4ever.Core.Profile;

namespace Am4ever.App.Wizard;

/// <summary>
/// Pure mapping from a wizard event to the toast it raises (message + kind). No Avalonia and no I/O,
/// so the wording and success/failure logic are unit-tested directly; the view just forwards these
/// to the <see cref="IToastService"/>.
/// </summary>
public static class WizardToasts
{
    public static (string Message, ToastKind Kind) ForDump(int byteLength) =>
        ($"✓ Dump loaded — {byteLength / (1024 * 1024)} MiB, valid AM4", ToastKind.Success);

    public static (string Message, ToastKind Kind) ForProfile(BoardProfile p) =>
        ($"✓ Profile selected: {p.Board.Vendor} {p.Board.Model}", ToastKind.Success);

    public static (string Message, ToastKind Kind) ForDonor() =>
        ("✓ Donor verified", ToastKind.Success);

    public static (string Message, ToastKind Kind) ForIdentity(IdentityChoice c) =>
        c == IdentityChoice.FromDmiFile
            ? ("✓ Using your machine-data identity", ToastKind.Success)
            : ("Using the generic board identity", ToastKind.Info);

    public static (string Message, ToastKind Kind) ForBuild(BuildOutcome o) =>
        o.Ok ? ("✓ BIOS built!", ToastKind.Success)
             : ($"✕ Build failed: {o.Error}", ToastKind.Error);
}
