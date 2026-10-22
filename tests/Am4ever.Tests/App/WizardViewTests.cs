// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.App.Feedback;
using Am4ever.App.Wizard;
using Avalonia.Headless.XUnit;

namespace Am4ever.Tests.App;

public class WizardViewTests
{
    [AvaloniaFact] public void WizardView_StartsOnDumpStep_NextDisabledUntilValid()
    {
        var view = new WizardView(new WizardFlow());
        Assert.Contains("Your BIOS", view.StepLabel.Text);
        Assert.False(view.NextButton.IsEnabled);        // no dump picked yet
        Assert.False(view.BackButton.IsEnabled);        // first step
    }

    [AvaloniaFact] public void BuildStep_HasAProgressBar()
    {
        var view = new WizardView(new WizardFlow(), new ToastService { ScheduleDismiss = (_, _) => { } });
        Assert.NotNull(view.BuildProgressBar);
    }

    [AvaloniaFact] public void FailedBuildOutcome_RaisesErrorToast_AndBarNotFull()
    {
        var toasts = new ToastService { ScheduleDismiss = (_, _) => { } };
        var view = new WizardView(new WizardFlow(), toasts);
        view.ReportBuildOutcome(new BuildOutcome(false, "no inputs", null));   // the seam the view also calls after a real build
        var t = Assert.Single(toasts.Toasts);
        Assert.Equal("✕", t.Glyph);
        Assert.True(view.BuildProgressBar.Value < 1.0);
    }
}
