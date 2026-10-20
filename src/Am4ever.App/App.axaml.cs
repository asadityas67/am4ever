// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.App.Safety;
using Am4ever.App.Theme;
using Am4ever.Core.Safety;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace Am4ever.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // Headless test hosts have no desktop lifetime; only the real app runs the startup flow.
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var themes = new ThemeService(this, PlatformThemeSource.FromApplication(this), ThemeSettingsStore.Default());
            var warning = new RiskWarningWindow(RiskAcknowledgement.NewForThisLaunch(),
                EnvReduceMotionProvider.ForCurrentPlatform());
            var flow = new StartupFlow(warning.ShowAndWaitAsync, () => new MainWindow(themes), () => desktop.Shutdown(0));

            Dispatcher.UIThread.Post(async () =>
            {
                var main = await flow.RunAsync();
                if (main is null)
                    return;
                desktop.MainWindow = main;
                main.Closed += (_, _) => desktop.Shutdown(0);
                main.Show();
            });
        }
        base.OnFrameworkInitializationCompleted();
    }
}
