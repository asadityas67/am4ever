// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.App.Safety;
using Am4ever.Core.Safety;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Microsoft.Extensions.Time.Testing;

namespace Am4ever.Tests.App;

public class RiskWarningWindowTests
{
    static RiskWarningWindow Make(bool reduce = false, FakeTimeProvider? t = null) =>
        new(RiskAcknowledgement.NewForThisLaunch(), new FixedReduceMotion(reduce), t);

    [AvaloniaFact] public void ShowsVerbatimCopy()
    {
        var w = Make(); w.Show();
        Assert.Equal(RiskWarning.Banner, w.FindControl<TextBlock>("BannerText")!.Text);
        Assert.Equal(RiskWarning.AcceptLabel, w.FindControl<CheckBox>("AcceptCheck")!.Content);
    }

    [AvaloniaFact] public void ContinueDisabledUntilTicked()
    {
        var w = Make(); w.Show();
        var c = w.FindControl<Button>("ContinueButton")!;
        Assert.False(c.IsEnabled);
        w.FindControl<CheckBox>("AcceptCheck")!.IsChecked = true;
        Assert.True(c.IsEnabled);
        Assert.False(c.IsDefault);
    }

    [AvaloniaFact] public void Warning_EnterKey_DoesNotContinueWhenUnticked()
    {
        var w = Make(); w.Show();
        w.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Assert.False(w.Accepted);
        Assert.True(w.IsVisible);
    }

    [AvaloniaFact] public void Warning_CloseButton_ExitsApp()
    {
        var w = Make();
        var shown = w.ShowAndWaitAsync();
        w.FindControl<CheckBox>("AcceptCheck")!.IsChecked = true;
        w.Close();                       // title-bar X / Alt+F4 path
        Assert.False(w.Accepted);
        Assert.True(shown.IsCompleted);
        Assert.False(shown.Result);
    }

    [AvaloniaFact] public async Task Warning_ClosedThroughStartupFlow_ShutsDownWithoutMain()
    {
        var w = Make();
        bool created = false, shut = false;
        var flow = new Am4ever.App.StartupFlow(w.ShowAndWaitAsync, () => { created = true; return new Window(); }, () => shut = true);
        var run = flow.RunAsync();
        w.Close();
        Assert.Null(await run);
        Assert.True(shut);
        Assert.False(created);
    }

    [AvaloniaFact] public void BlinksEvery500ms()
    {
        var t = new FakeTimeProvider(); var w = Make(t: t); w.Show();
        var first = w.BannerBright;
        t.Advance(TimeSpan.FromMilliseconds(500)); Dispatcher.UIThread.RunJobs();
        Assert.NotEqual(first, w.BannerBright);
        t.Advance(TimeSpan.FromMilliseconds(500)); Dispatcher.UIThread.RunJobs();
        Assert.Equal(first, w.BannerBright);
    }

    [AvaloniaFact] public void ReduceMotion_SolidWithThickBorder()
    {
        var t = new FakeTimeProvider(); var w = Make(reduce: true, t: t); w.Show();
        Assert.True(w.BannerBright);
        t.Advance(TimeSpan.FromSeconds(2)); Dispatcher.UIThread.RunJobs();
        Assert.True(w.BannerBright);
        Assert.Equal(new Thickness(6), w.BannerBorderThickness);
    }
}

public class ReduceMotionTests
{
    [Fact] public void LinuxReduceMotion_MissingGsettings_ReturnsFalse() =>
        Assert.False(new LinuxReduceMotionProvider(gsettingsPath: "/nonexistent/gsettings").ReduceMotion);

    [Fact] public void EnvOverrideWins() =>
        Assert.True(new EnvReduceMotionProvider(new FixedReduceMotion(false), env: _ => "1").ReduceMotion);
}

sealed class FixedReduceMotion(bool reduce) : IReduceMotionProvider
{
    public bool ReduceMotion => reduce;
}
