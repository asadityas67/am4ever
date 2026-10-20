// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Profile;

namespace Am4ever.Core.Build;

/// <summary>
/// One typed board operation. The pipeline runs it in three phases: <see cref="Check"/> (throws on a
/// failed precondition, nothing changed), <see cref="Apply"/> (makes the edit, returns report lines),
/// and <see cref="Verify"/> (confirms the result afterwards).
/// </summary>
public interface IBoardOperation
{
    string Describe();
    void Check(BuildContext ctx);
    IReadOnlyList<string> Apply(BuildContext ctx);
    bool Verify(BuildContext ctx, out string message);
}

/// <summary>Shared base: holds the spec and a default "nothing to verify" post-check.</summary>
public abstract class BoardOperation : IBoardOperation
{
    protected BoardOperation(OperationSpec spec) => Spec = spec;

    protected OperationSpec Spec { get; }

    public virtual string Describe() => Spec.Op + (Spec.Family is null ? "" : $" ({Spec.Family})");
    public abstract void Check(BuildContext ctx);
    public abstract IReadOnlyList<string> Apply(BuildContext ctx);
    public virtual bool Verify(BuildContext ctx, out string message) { message = ""; return true; }
}
