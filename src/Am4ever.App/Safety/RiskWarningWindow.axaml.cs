// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Safety;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace Am4ever.App.Safety;

/// <summary>
/// The red "you are responsible" warning shown before anything else on every launch
/// (design §6.0). Blinks at 1 Hz, or stays solid with a thick border under reduce-motion.
/// </summary>
public partial class RiskWarningWindow : Window
{
    static readonly IBrush Bright = new SolidColorBrush(Color.Parse("#E00000"));
    static readonly IBrush Dark = new SolidColorBrush(Color.Parse("#6E0000"));
    static readonly TimeSpan BlinkPeriod = TimeSpan.FromMilliseconds(500);

    readonly RiskAcknowledgement _ack;
    readonly bool _reduceMotion;
    readonly TimeProvider _time;
    readonly TaskCompletionSource<bool> _closed = new();
    ITimer? _blink;

    /// <summary>Required by the XAML loader; the app always uses the other constructor.</summary>
    public RiskWarningWindow() : this(RiskAcknowledgement.NewForThisLaunch(), new EnvReduceMotionProvider(new NoReduceMotion())) { }

    public RiskWarningWindow(RiskAcknowledgement ack, IReduceMotionProvider motion, TimeProvider? time = null)
    {
        _ack = ack;
        _reduceMotion = motion.ReduceMotion;
        _time = time ?? TimeProvider.System;
        InitializeComponent();

        BannerText.Text = RiskWarning.Banner;
        DetailsList.ItemsSource = RiskWarning.Details;
        AcceptCheck.Content = RiskWarning.AcceptLabel;
        ContinueButton.Content = RiskWarning.ContinueLabel;
        ExitButton.Content = RiskWarning.ExitLabel;

        AcceptCheck.IsCheckedChanged += (_, _) => ContinueButton.IsEnabled = AcceptCheck.IsChecked == true;
        ContinueButton.Click += (_, _) => { if (AcceptCheck.IsChecked == true) { _ack.Accept(); Close(); } };
        ExitButton.Click += (_, _) => Close();

        SetBright(true);
        if (_reduceMotion)
            Banner.BorderThickness = new Thickness(6);

        Opened += (_, _) => StartBlinking();
        Closed += (_, _) =>
        {
            _blink?.Dispose();
            _closed.TrySetResult(Accepted);
        };
    }

    /// <summary>True only if the user ticked the box and pressed Continue.</summary>
    public bool Accepted => _ack.Accepted;

    public bool BannerBright { get; private set; }

    public Thickness BannerBorderThickness => Banner.BorderThickness;

    /// <summary>Shows the window and completes with <see cref="Accepted"/> when it closes.</summary>
    public Task<bool> ShowAndWaitAsync()
    {
        Show();
        return _closed.Task;
    }

    void StartBlinking()
    {
        if (_reduceMotion || _blink is not null)
            return;
        _blink = _time.CreateTimer(_ => Dispatcher.UIThread.Post(() => SetBright(!BannerBright)),
            null, BlinkPeriod, BlinkPeriod);
    }

    void SetBright(bool bright)
    {
        BannerBright = bright;
        Banner.Background = bright ? Bright : Dark;
    }

    sealed class NoReduceMotion : IReduceMotionProvider
    {
        public bool ReduceMotion => false;
    }
}
