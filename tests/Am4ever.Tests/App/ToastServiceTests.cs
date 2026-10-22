// SPDX-License-Identifier: GPL-2.0-or-later
using System.Linq;
using Am4ever.App.Feedback;

namespace Am4ever.Tests.App;

public class ToastServiceTests
{
    [Fact] public void Show_AddsToast_AndManualDismissRemoves()
    {
        var s = new ToastService { ScheduleDismiss = (_, _) => { } };   // no auto-dismiss in the test
        s.Show("built", ToastKind.Success);
        var t = Assert.Single(s.Toasts);
        Assert.Equal("✓", t.Glyph);
        s.Dismiss(t);
        Assert.Empty(s.Toasts);
    }

    [Fact] public void Dismiss_IsIdempotent_AfterAlreadyRemoved()
    {
        var s = new ToastService { ScheduleDismiss = (_, _) => { } };
        s.Show("x", ToastKind.Info);
        var t = s.Toasts[0];
        s.Dismiss(t); s.Dismiss(t);       // second is a safe no-op
        Assert.Empty(s.Toasts);
    }

    [Fact] public void Show_OverCap_DropsOldest()
    {
        var s = new ToastService { MaxVisible = 2, ScheduleDismiss = (_, _) => { } };
        s.Show("a", ToastKind.Info); s.Show("b", ToastKind.Info); s.Show("c", ToastKind.Info);
        Assert.Equal(new[] { "b", "c" }, s.Toasts.Select(t => t.Message));
    }
}
