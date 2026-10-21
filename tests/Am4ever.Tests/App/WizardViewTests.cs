// SPDX-License-Identifier: GPL-2.0-or-later
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
}
