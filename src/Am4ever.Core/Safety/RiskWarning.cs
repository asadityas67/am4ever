// SPDX-License-Identifier: GPL-2.0-or-later
namespace Am4ever.Core.Safety;

/// <summary>The risk warning shown on every launch (design §6.0). Copy is verbatim.</summary>
public static class RiskWarning
{
    public const string Banner =
        "WARNING: You are responsible for what you do. Flashing a modified BIOS can permanently kill your PC if you are not careful.";

    public const string NotAffiliated =
        "am4ever is not affiliated with AMD, AMI, ASRock, Biostar, HP, Dell or Lenovo.";

    public const string AcceptLabel = "I understand and accept the risk";
    public const string ContinueLabel = "Continue";
    public const string ExitLabel = "Exit";

    public static IReadOnlyList<string> Details { get; } = new[]
    {
        "am4ever comes with no warranty (GPL-2.0). You use it at your own risk.",
        "Keep your original BIOS backup somewhere safe before you flash anything.",
        "Only flash with an SPI programmer you know how to use, and know how to restore your backup.",
        NotAffiliated,
    };
}

/// <summary>
/// The user's acceptance of the risk warning. It lives only for the current launch:
/// there is deliberately no way to save or restore it.
/// </summary>
public sealed class RiskAcknowledgement
{
    RiskAcknowledgement() { }

    public bool Accepted { get; private set; }

    public void Accept() => Accepted = true;

    public static RiskAcknowledgement NewForThisLaunch() => new();
}
