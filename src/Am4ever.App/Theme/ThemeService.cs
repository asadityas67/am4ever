// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia;
using Avalonia.Platform;
using Avalonia.Styling;

namespace Am4ever.App.Theme;

public enum ThemeMode { FollowSystem, Light, Dark }

/// <summary>The OS light/dark setting.</summary>
public interface IPlatformThemeSource
{
    ThemeVariant Current { get; }
    event EventHandler? Changed;
}

/// <summary><see cref="IPlatformThemeSource"/> backed by Avalonia's platform settings.</summary>
public sealed class PlatformThemeSource : IPlatformThemeSource
{
    readonly IPlatformSettings? _settings;

    PlatformThemeSource(IPlatformSettings? settings)
    {
        _settings = settings;
        if (_settings is not null)
            _settings.ColorValuesChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    public static PlatformThemeSource FromApplication(Application app) => new(app.PlatformSettings);

    public ThemeVariant Current =>
        _settings?.GetColorValues().ThemeVariant == PlatformThemeVariant.Dark ? ThemeVariant.Dark : ThemeVariant.Light;

    public event EventHandler? Changed;
}

/// <summary>Applies and remembers the theme mode chosen in View > Theme.</summary>
public sealed class ThemeService
{
    readonly Application _app;
    readonly IPlatformThemeSource _platform;
    readonly ThemeSettingsStore _store;

    public ThemeService(Application app, IPlatformThemeSource platform, ThemeSettingsStore store)
    {
        _app = app;
        _platform = platform;
        _store = store;
        Mode = store.Load();
        Apply();
        _platform.Changed += (_, _) =>
        {
            if (Mode == ThemeMode.FollowSystem)
                EffectiveChanged?.Invoke(this, EventArgs.Empty);
        };
    }

    public ThemeMode Mode { get; private set; }

    /// <summary>The variant actually in use: the explicit choice, or the OS setting.</summary>
    public ThemeVariant Effective => Mode switch
    {
        ThemeMode.Light => ThemeVariant.Light,
        ThemeMode.Dark => ThemeVariant.Dark,
        _ => _platform.Current,
    };

    public event EventHandler? EffectiveChanged;

    public void SetMode(ThemeMode mode)
    {
        Mode = mode;
        Apply();
        _store.Save(mode);
        EffectiveChanged?.Invoke(this, EventArgs.Empty);
    }

    void Apply() => _app.RequestedThemeVariant = Mode switch
    {
        ThemeMode.Light => ThemeVariant.Light,
        ThemeMode.Dark => ThemeVariant.Dark,
        _ => ThemeVariant.Default,
    };
}
