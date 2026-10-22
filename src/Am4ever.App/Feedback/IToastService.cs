// SPDX-License-Identifier: GPL-2.0-or-later
namespace Am4ever.App.Feedback;

/// <summary>Raises transient toast notifications. The wizard calls this; a <see cref="ToastHost"/> shows them.</summary>
public interface IToastService
{
    void Show(string message, ToastKind kind);
}
