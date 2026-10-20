// SPDX-License-Identifier: GPL-2.0-or-later
using System.Reflection;
using Am4ever.Core.Safety;
using Avalonia.Controls;

namespace Am4ever.App;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        var version = typeof(AboutWindow).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        VersionText.Text = "Version " + version;
        AffiliationText.Text = RiskWarning.NotAffiliated;
        OkButton.Click += (_, _) => Close();
    }
}
