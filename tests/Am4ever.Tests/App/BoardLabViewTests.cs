// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.App.Lab;
using Avalonia.Headless.XUnit;

namespace Am4ever.Tests.App;

public class BoardLabViewTests
{
    [AvaloniaFact] public void BoardLabView_StartsEmpty_OpenOemEnabled()
    {
        var view = new BoardLabView();
        Assert.True(view.OpenOemButton.IsEnabled);
    }
}
