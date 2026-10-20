// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(Am4ever.Tests.App.TestAppBuilder))]

namespace Am4ever.Tests.App;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<Am4ever.App.App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
