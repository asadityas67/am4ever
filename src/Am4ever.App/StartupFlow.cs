// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia.Controls;

namespace Am4ever.App;

/// <summary>
/// Launch order: the risk warning first, always. Only if it is accepted is the main
/// window created; otherwise the app shuts down.
/// </summary>
public sealed class StartupFlow(Func<Task<bool>> showWarning, Func<Window> createMain, Action shutdown)
{
    public async Task<Window?> RunAsync()
    {
        if (!await showWarning())
        {
            shutdown();
            return null;
        }
        return createMain();
    }
}
