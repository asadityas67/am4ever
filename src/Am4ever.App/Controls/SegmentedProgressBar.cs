// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Am4ever.App.Controls;

/// <summary>
/// A classic Windows-98 segmented progress bar: a sunken trough of fixed blocks, the first
/// <see cref="FilledSegments"/> of them lit. <see cref="Value"/> is an honest fraction in [0,1]
/// (NaN/∞/out-of-range clamp to the ends), set from a build's real phase progress. The template
/// (in WinFormsTheme.axaml) provides a <c>PART_Segments</c> panel this control fills in code.
/// </summary>
public sealed class SegmentedProgressBar : TemplatedControl
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<SegmentedProgressBar, double>(nameof(Value));

    public static readonly StyledProperty<int> SegmentCountProperty =
        AvaloniaProperty.Register<SegmentedProgressBar, int>(nameof(SegmentCount), 20);

    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public int SegmentCount { get => GetValue(SegmentCountProperty); set => SetValue(SegmentCountProperty, value); }

    static double Clamp01(double v) => double.IsNaN(v) ? 0 : Math.Clamp(v, 0, 1);

    /// <summary>The number of lit blocks, always in [0, SegmentCount].</summary>
    public int FilledSegments => Math.Clamp((int)Math.Floor(Clamp01(Value) * SegmentCount), 0, Math.Max(0, SegmentCount));

    UniformGrid? _segments;

    static SegmentedProgressBar()
    {
        ValueProperty.Changed.AddClassHandler<SegmentedProgressBar>((x, _) => x.Rebuild());
        SegmentCountProperty.Changed.AddClassHandler<SegmentedProgressBar>((x, _) => x.Rebuild());
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _segments = e.NameScope.Find<UniformGrid>("PART_Segments");
        Rebuild();
    }

    void Rebuild()
    {
        if (_segments is null) return;                 // no template yet (e.g. headless property tests)
        int count = Math.Max(1, SegmentCount);
        _segments.Columns = count;
        _segments.Children.Clear();
        IBrush fill = Brushes.Navy;
        if (this.TryGetResource("am4.ProgressFill", ActualThemeVariant, out var r) && r is IBrush rb)
            fill = rb;
        int lit = FilledSegments;
        for (int i = 0; i < count; i++)
            _segments.Children.Add(new Border
            {
                Background = i < lit ? fill : Brushes.Transparent,
                Margin = new Thickness(0, 0, i < count - 1 ? 2 : 0, 0),   // a gap between blocks, Win98-style
            });
    }
}
