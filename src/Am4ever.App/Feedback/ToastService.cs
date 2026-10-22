// SPDX-License-Identifier: GPL-2.0-or-later
using System.Collections.ObjectModel;
using Avalonia.Threading;

namespace Am4ever.App.Feedback;

/// <summary>
/// Holds the live toasts and auto-dismisses them. <see cref="Toasts"/> is what a <see cref="ToastHost"/>
/// binds to. Auto-dismiss goes through <see cref="ScheduleDismiss"/> so tests can disable timing;
/// in the app it is a one-shot dispatcher timer. The newest toast is last; past <see cref="MaxVisible"/>
/// the oldest is dropped.
/// </summary>
public sealed class ToastService : IToastService
{
    public ObservableCollection<Toast> Toasts { get; } = new();

    public int MaxVisible { get; init; } = 4;

    /// <summary>How an auto-dismiss is scheduled: given the toast and a dismiss callback, arrange to
    /// call it later. Defaults to a ~3.5 s one-shot <see cref="DispatcherTimer"/>; tests pass a no-op.</summary>
    public Action<Toast, Action> ScheduleDismiss { get; init; } =
        (_, dismiss) => DispatcherTimer.RunOnce(dismiss, TimeSpan.FromSeconds(3.5));

    public void Show(string message, ToastKind kind)
    {
        var toast = new Toast(message, kind);
        Toasts.Add(toast);
        while (Toasts.Count > MaxVisible) Toasts.RemoveAt(0);   // drop the oldest
        ScheduleDismiss(toast, () => Dismiss(toast));
    }

    /// <summary>Remove a toast if it is still showing; a no-op otherwise (safe to call twice, or after teardown).</summary>
    public void Dismiss(Toast toast)
    {
        if (Toasts.Contains(toast)) Toasts.Remove(toast);
    }
}
