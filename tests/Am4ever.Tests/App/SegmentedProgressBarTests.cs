// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.App.Controls;
using Avalonia.Headless.XUnit;

namespace Am4ever.Tests.App;

public class SegmentedProgressBarTests
{
    [AvaloniaFact] public void FilledSegments_MapsAndClampsValue()
    {
        var bar = new SegmentedProgressBar { SegmentCount = 20 };
        bar.Value = 0.0;        Assert.Equal(0, bar.FilledSegments);
        bar.Value = 1.0;        Assert.Equal(20, bar.FilledSegments);
        bar.Value = 0.5;        Assert.Equal(10, bar.FilledSegments);
        bar.Value = -3;         Assert.Equal(0, bar.FilledSegments);
        bar.Value = 2;          Assert.Equal(20, bar.FilledSegments);
        bar.Value = double.NaN; Assert.Equal(0, bar.FilledSegments);
    }
}
