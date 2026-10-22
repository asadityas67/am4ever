// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.App.Controls;
using Am4ever.App.Feedback;
using Am4ever.Core.Build;
using Am4ever.Core.Profile;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace Am4ever.App.Wizard;

/// <summary>
/// The beginner wizard view: one panel per <see cref="WizardStep"/>, Back/Next driven by
/// <see cref="WizardFlow"/>. Built in code (no XAML) to stay small and headless-testable. File
/// pickers use the hosting window's storage provider and are inert until the view is in a window.
/// Interaction feedback (a Win98 segmented bar on the build, toasts per step) is layered on top.
/// </summary>
public sealed class WizardView : UserControl
{
    readonly WizardFlow _flow;
    readonly IToastService _toasts;
    public TextBlock StepLabel { get; } = new() { FontWeight = Avalonia.Media.FontWeight.Bold };
    public TextBlock Reason { get; } = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap, Opacity = 0.8 };
    public TextBlock Body { get; } = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    public Button BackButton { get; } = new() { Content = "< Back" };
    public Button NextButton { get; } = new() { Content = "Next >" };
    public SegmentedProgressBar BuildProgressBar { get; } = new();
    readonly TextBlock _buildResult = new() { TextWrapping = TextWrapping.Wrap, FontWeight = FontWeight.Bold };
    readonly Button _action = new();                 // the per-step "Browse…"/"Build…" action
    readonly ListBox _profiles = new() { MaxHeight = 160 };

    /// <summary>Convenience for hosts/tests that don't supply a toast service: uses a self-contained one.</summary>
    public WizardView(WizardFlow flow) : this(flow, new ToastService()) { }

    public WizardView(WizardFlow flow, IToastService toasts)
    {
        _flow = flow;
        _toasts = toasts;
        _action.Click += async (_, _) => await OnAction();
        _profiles.ItemTemplate = new FuncDataTemplate<BoardProfile>((p, _) =>
            new TextBlock { Text = WizardFlow.ProfileSummary(p), TextWrapping = TextWrapping.Wrap });
        _profiles.SelectionChanged += (_, _) =>
        {
            if (_profiles.SelectedItem is not BoardProfile p) return;
            try
            {
                _flow.SelectProfile(p);
                var (msg, kind) = WizardToasts.ForProfile(p);
                _toasts.Show(msg, kind);
            }
            catch (Exception ex) { Reason.Text = ex.Message; }
            Refresh();
        };
        BackButton.Click += (_, _) => { _flow.Back(); Refresh(); };
        NextButton.Click += (_, _) => { _flow.Next(); Refresh(); };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { BackButton, NextButton } };
        Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(12), Spacing = 8,
            Children = { StepLabel, Body, _profiles, _action, BuildProgressBar, _buildResult, Reason, buttons },
        };
        Refresh();
    }

    void Refresh()
    {
        var s = _flow.Step;
        StepLabel.Text = $"Step {(int)s + 1} of 6 — {Title(s)}";
        Body.Text = Describe(s);
        _profiles.IsVisible = s == WizardStep.Profile;
        if (s == WizardStep.Profile) _profiles.ItemsSource = _flow.MatchingProfiles();
        _action.IsVisible = s is WizardStep.Dump or WizardStep.Donor or WizardStep.Identity or WizardStep.Build;
        BuildProgressBar.IsVisible = s == WizardStep.Build;
        _buildResult.IsVisible = s == WizardStep.Build && !string.IsNullOrEmpty(_buildResult.Text);
        _action.Content = s switch
        {
            WizardStep.Dump => "Choose your BIOS dump…",
            WizardStep.Donor => "Choose the donor BIOS…",
            WizardStep.Identity => "Use a machine-data file (optional)…",
            WizardStep.Build => "Build the image…",
            _ => "",
        };
        BackButton.IsEnabled = s != WizardStep.Dump;
        NextButton.IsEnabled = _flow.CanAdvance(out var reason) && s != WizardStep.Flash;
        Reason.Text = reason ?? "";
    }

    static string Title(WizardStep s) => s switch
    {
        WizardStep.Dump => "Your BIOS", WizardStep.Profile => "Board profile", WizardStep.Donor => "Donor BIOS",
        WizardStep.Identity => "Identity", WizardStep.Build => "Build & checks", _ => "Flash checklist",
    };

    string Describe(WizardStep s) => s switch
    {
        WizardStep.Dump => "Pick your board's BIOS dump (a second read of the same chip is recommended — it must match byte for byte).",
        WizardStep.Profile => "Pick the profile for your board. Review its tested CPUs and any inferred/untested items before continuing.",
        WizardStep.Donor => "Download the exact donor BIOS named by the profile and pick it here; its SHA-256 is verified.",
        WizardStep.Identity => _flow.Identity == IdentityChoice.FromDmiFile ? "Using your machine-data file." : "Using the generic board identity. You can load a machine-data file instead.",
        WizardStep.Build => "Build the modded image and run the structure, checksum and round-trip checks. If a check fails the image is not written.",
        _ => FlashChecklist.Text,
    };

    /// <summary>Show the outcome of a build: full bar + done panel + success toast, or an error toast
    /// with the bar left where it stopped. Public so a headless test can drive it without a real build.</summary>
    public void ReportBuildOutcome(BuildOutcome outcome)
    {
        if (outcome.Ok)
        {
            BuildProgressBar.Value = 1.0;
            _buildResult.Text = $"✓ Done. Report saved: {outcome.ReportPath}";
        }
        else
        {
            _buildResult.Text = $"✕ Build failed (no image written): {outcome.Error}";
        }
        _buildResult.IsVisible = true;
        var (msg, kind) = WizardToasts.ForBuild(outcome);
        _toasts.Show(msg, kind);
    }

    async Task OnAction()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;                      // not in a window (e.g. headless): nothing to do
        try
        {
            if (_flow.Step == WizardStep.Build)
            {
                var save = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { SuggestedFileName = "modded.bin" });
                if (save is null) return;
                Reason.Text = "Building and checking… this takes a few seconds.";
                _action.IsEnabled = false;
                BuildProgressBar.Value = 0;
                _buildResult.Text = "";
                // created on the UI thread, so the reporter posts bar updates back to the UI thread
                var progress = new Progress<BuildProgress>(p => BuildProgressBar.Value = p.Total == 0 ? 0 : (double)p.Done / p.Total);
                var outcome = await Task.Run(() => _flow.Build(save.Path.LocalPath, progress));   // off the UI thread
                _action.IsEnabled = true;
                ReportBuildOutcome(outcome);
                Refresh();
                return;
            }
            var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = false });
            if (files.Count == 0) return;
            var bytes = await ReadAll(files[0]);
            switch (_flow.Step)
            {
                case WizardStep.Dump:
                    _flow.SetDump(bytes);
                    if (_flow.CanAdvance(out _)) { var (m, k) = WizardToasts.ForDump(bytes.Length); _toasts.Show(m, k); }
                    break;
                case WizardStep.Donor:
                    _flow.SetDonor(bytes);
                    if (_flow.CanAdvance(out _)) { var (m, k) = WizardToasts.ForDonor(); _toasts.Show(m, k); }
                    break;
                case WizardStep.Identity:
                    _flow.SetIdentity(IdentityChoice.FromDmiFile, bytes);
                    { var (m, k) = WizardToasts.ForIdentity(IdentityChoice.FromDmiFile); _toasts.Show(m, k); }
                    break;
            }
            Refresh();
        }
        catch (Exception ex)   // a bad file / DMI / adapter must show a message, never crash the wizard
        {
            _action.IsEnabled = true;
            Reason.Text = ex.Message;
        }
    }

    static async Task<byte[]> ReadAll(IStorageFile file)
    {
        await using var s = await file.OpenReadAsync();
        using var ms = new MemoryStream();
        await s.CopyToAsync(ms);
        return ms.ToArray();
    }
}
