// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia;
using Avalonia.Markup.Xaml;

namespace Am4ever.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
}
