// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.App.Feedback;
using Am4ever.App.Lab;
using Avalonia.Headless.XUnit;

namespace Am4ever.Tests.App;

public class BoardLabViewTests
{
    [AvaloniaFact] public void BoardLabView_StartsEmpty_OpenOemEnabled()
    {
        var view = new BoardLabView();
        Assert.True(view.OpenOemButton.IsEnabled);
    }

    [AvaloniaFact] public void Composer_TestDisabledUntilDumpAndAdapterSet()
    {
        var view = new BoardLabView(new ToastService { ScheduleDismiss = (_, _) => { } });
        Assert.NotNull(view.TestButton);
        Assert.False(view.TestButton.IsEnabled);        // no OEM dump / adapter yet
    }

    [AvaloniaFact] public void Inspect_HexAndDisasmButtonsExist_DisabledUntilModuleSelected()
    {
        var view = new BoardLabView(new ToastService { ScheduleDismiss = (_, _) => { } });
        Assert.NotNull(view.HexButton);
        Assert.NotNull(view.DisasmButton);
        Assert.False(view.HexButton.IsEnabled);
        Assert.False(view.DisasmButton.IsEnabled);
    }

    [AvaloniaFact] public void AdapterAuthoring_DetectDisabledUntilDonorLoaded()
    {
        var view = new BoardLabView(new ToastService { ScheduleDismiss = (_, _) => { } });
        Assert.NotNull(view.DetectAdapterButton);
        Assert.False(view.DetectAdapterButton.IsEnabled);
    }

    [AvaloniaFact] public void TableEditToggle_On_RaisesWarning()
    {
        var view = new BoardLabView(new ToastService { ScheduleDismiss = (_, _) => { } });
        view.TableEditToggle.IsChecked = true;          // user flips it on
        Assert.True(view.LastTableEditWarningShown);    // a warning was shown
    }
}
