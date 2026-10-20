// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.App;
using Am4ever.Core.Safety;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Styling;

namespace Am4ever.Tests.App;

public class StartupFlowTests
{
    [Fact] public async Task Declined_ShutsDownAndNeverCreatesMain()
    {
        bool created = false, shut = false;
        var w = await new StartupFlow(() => Task.FromResult(false), () => { created = true; return null!; }, () => shut = true).RunAsync();
        Assert.Null(w); Assert.True(shut); Assert.False(created);
    }

    [AvaloniaFact] public async Task Accepted_CreatesMain()
    {
        var w = await new StartupFlow(() => Task.FromResult(true), () => new MainWindow(), () => { }).RunAsync();
        Assert.IsType<MainWindow>(w);
    }
}

public class MainWindowTests
{
    [AvaloniaFact] public void ThemeMenuSwitchesTheme()
    {
        var m = new MainWindow(); m.Show();
        m.FindControl<MenuItem>("ThemeDarkItem")!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Assert.Equal(ThemeVariant.Dark, Application.Current!.RequestedThemeVariant);
    }

    [AvaloniaFact] public void AboutShowsNotAffiliatedAndLicence()
    {
        var a = new AboutWindow(); a.Show();
        var text = string.Join("\n", a.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
        Assert.Contains(RiskWarning.NotAffiliated, text);
        Assert.Contains("GPL-2.0-or-later", text);
    }
}
