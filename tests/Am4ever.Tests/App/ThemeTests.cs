// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.App.Theme;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;

namespace Am4ever.Tests.App;

public class ThemeTests
{
    [AvaloniaFact] public void LightAndDarkControlColoursDiffer()
    {
        var app = Application.Current!;
        Assert.True(app.TryGetResource("am4.Control", ThemeVariant.Light, out var l));
        Assert.True(app.TryGetResource("am4.Control", ThemeVariant.Dark, out var d));
        Assert.Equal(Color.Parse("#F0F0F0"), ((SolidColorBrush)l!).Color);
        Assert.Equal(Color.Parse("#2D2D30"), ((SolidColorBrush)d!).Color);
    }

    [AvaloniaFact] public void ExplicitModeIgnoresPlatform()
    {
        var p = new FakePlatformTheme(ThemeVariant.Light);
        var s = new ThemeService(Application.Current!, p, ThemeSettingsStore.InMemory());
        s.SetMode(ThemeMode.Dark);
        p.Raise(ThemeVariant.Light);
        Assert.Equal(ThemeVariant.Dark, Application.Current!.RequestedThemeVariant);
    }

    [AvaloniaFact] public void ThemeService_FollowSystem_TracksPlatformChange()
    {
        var p = new FakePlatformTheme(ThemeVariant.Light);
        var s = new ThemeService(Application.Current!, p, ThemeSettingsStore.InMemory());
        s.SetMode(ThemeMode.FollowSystem);
        p.Raise(ThemeVariant.Dark);
        Assert.Equal(ThemeVariant.Dark, s.Effective);
    }

    [Fact] public void CorruptSettingsFallBackToFollowSystem()
    {
        var path = Path.GetTempFileName(); File.WriteAllText(path, "{not json");
        Assert.Equal(ThemeMode.FollowSystem, new ThemeSettingsStore(path).Load());
    }
}

sealed class FakePlatformTheme(ThemeVariant initial) : IPlatformThemeSource
{
    public ThemeVariant Current { get; private set; } = initial;
    public event EventHandler? Changed;

    public void Raise(ThemeVariant v)
    {
        Current = v;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
