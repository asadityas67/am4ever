// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Am4ever.App.Feedback;

/// <summary>
/// Overlay that shows a <see cref="ToastService"/>'s toasts stacked at the bottom-right, each a raised
/// Win98 bevel box (glyph + message) that dismisses on click. Built in code (no template) so it needs
/// no theme entry; its empty area is transparent and hit-test-invisible, so it never blocks the wizard
/// beneath — only the toast boxes themselves take clicks.
/// </summary>
public sealed class ToastHost : Decorator
{
    public static readonly StyledProperty<ToastService?> ServiceProperty =
        AvaloniaProperty.Register<ToastHost, ToastService?>(nameof(Service));

    public ToastService? Service { get => GetValue(ServiceProperty); set => SetValue(ServiceProperty, value); }

    readonly ItemsControl _items = new();

    public ToastHost()
    {
        HorizontalAlignment = HorizontalAlignment.Right;
        VerticalAlignment = VerticalAlignment.Bottom;
        Margin = new Thickness(0, 0, 12, 12);
        IsHitTestVisible = true;                       // the boxes are hit-testable; the gaps are transparent
        _items.ItemTemplate = new FuncDataTemplate<Toast>((t, _) => BuildToast(t), supportsRecycling: false);
        Child = _items;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ServiceProperty)
            _items.ItemsSource = Service?.Toasts;
    }

    Control BuildToast(Toast toast)
    {
        var glyph = new TextBlock { Text = toast.Glyph, FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 0, 8, 0) };
        var text = new TextBlock { Text = toast.Message, TextWrapping = TextWrapping.Wrap, MaxWidth = 260 };
        var content = new StackPanel { Orientation = Orientation.Horizontal, Children = { glyph, text } };

        // raised bevel: light top-left, dark bottom-right, over the control face
        var inner = new Border { BorderThickness = new Thickness(0, 0, 1, 1), BorderBrush = Res("am4.ControlDarkDark", Brushes.Gray), Background = Res("am4.Control", Brushes.Silver), Padding = new Thickness(8, 6), Child = content };
        var box = new Border { BorderThickness = new Thickness(1, 1, 0, 0), BorderBrush = Res("am4.ControlLightLight", Brushes.White), Child = inner, Margin = new Thickness(0, 6, 0, 0), MinWidth = 140 };
        box.PointerPressed += (_, _) => Service?.Dismiss(toast);
        return box;
    }

    IBrush Res(string key, IBrush fallback) =>
        this.TryGetResource(key, ActualThemeVariant, out var r) && r is IBrush b ? b : fallback;
}
