// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.App.Theme;
using Avalonia;
using Avalonia.Controls;

namespace Am4ever.App;

public partial class MainWindow : Window
{
    readonly ThemeService _themes;

    public MainWindow() : this(null) { }

    public MainWindow(ThemeService? themes)
    {
        _themes = themes ?? new ThemeService(Application.Current!,
            PlatformThemeSource.FromApplication(Application.Current!), ThemeSettingsStore.InMemory());
        InitializeComponent();

        ExitItem.Click += (_, _) => Close();
        AboutItem.Click += (_, _) => new AboutWindow().ShowDialog(this);
        ThemeFollowItem.Click += (_, _) => _themes.SetMode(ThemeMode.FollowSystem);
        ThemeLightItem.Click += (_, _) => _themes.SetMode(ThemeMode.Light);
        ThemeDarkItem.Click += (_, _) => _themes.SetMode(ThemeMode.Dark);
        _themes.EffectiveChanged += (_, _) => SyncThemeMenu();
        SyncThemeMenu();

        var wizard = new Wizard.WizardView(new Wizard.WizardFlow());
        var lab = new Lab.BoardLabView();
        WizardHost.Content = wizard;
        ModeWizardItem.Click += (_, _) => WizardHost.Content = wizard;
        ModeBoardLabItem.Click += (_, _) => WizardHost.Content = lab;
    }

    void SyncThemeMenu()
    {
        ThemeFollowItem.IsChecked = _themes.Mode == ThemeMode.FollowSystem;
        ThemeLightItem.IsChecked = _themes.Mode == ThemeMode.Light;
        ThemeDarkItem.IsChecked = _themes.Mode == ThemeMode.Dark;
    }
}
