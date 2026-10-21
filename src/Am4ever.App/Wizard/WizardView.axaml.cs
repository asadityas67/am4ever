// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Profile;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;

namespace Am4ever.App.Wizard;

/// <summary>
/// The beginner wizard view: one panel per <see cref="WizardStep"/>, Back/Next driven by
/// <see cref="WizardFlow"/>. Built in code (no XAML) to stay small and headless-testable. File
/// pickers use the hosting window's storage provider and are inert until the view is in a window.
/// </summary>
public sealed class WizardView : UserControl
{
    readonly WizardFlow _flow;
    public TextBlock StepLabel { get; } = new() { FontWeight = Avalonia.Media.FontWeight.Bold };
    public TextBlock Reason { get; } = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap, Opacity = 0.8 };
    public TextBlock Body { get; } = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    public Button BackButton { get; } = new() { Content = "< Back" };
    public Button NextButton { get; } = new() { Content = "Next >" };
    readonly Button _action = new();                 // the per-step "Browse…"/"Build…" action
    readonly ListBox _profiles = new() { MaxHeight = 160 };

    public WizardView(WizardFlow flow)
    {
        _flow = flow;
        _action.Click += async (_, _) => await OnAction();
        _profiles.SelectionChanged += (_, _) => { if (_profiles.SelectedItem is BoardProfile p) { _flow.SelectProfile(p); Refresh(); } };
        BackButton.Click += (_, _) => { _flow.Back(); Refresh(); };
        NextButton.Click += (_, _) => { _flow.Next(); Refresh(); };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { BackButton, NextButton } };
        Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(12), Spacing = 8,
            Children = { StepLabel, Body, _profiles, _action, Reason, buttons },
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
        WizardStep.Build => "Build the modded image and run every check. If a check fails the image is not written.",
        _ => FlashChecklist.Text,
    };

    async Task OnAction()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;                      // not in a window (e.g. headless): nothing to do
        if (_flow.Step == WizardStep.Build)
        {
            var save = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { SuggestedFileName = "modded.bin" });
            if (save is null) return;
            var outcome = _flow.Build(save.Path.LocalPath);
            Reason.Text = outcome.Ok ? $"Built. Report: {outcome.ReportPath}" : $"Build failed: {outcome.Error}";
            Refresh();
            return;
        }
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = false });
        if (files.Count == 0) return;
        var bytes = await ReadAll(files[0]);
        switch (_flow.Step)
        {
            case WizardStep.Dump: _flow.SetDump(bytes); break;
            case WizardStep.Donor: _flow.SetDonor(bytes); break;
            case WizardStep.Identity: _flow.SetIdentity(IdentityChoice.FromDmiFile, bytes); break;
        }
        Refresh();
    }

    static async Task<byte[]> ReadAll(IStorageFile file)
    {
        await using var s = await file.OpenReadAsync();
        using var ms = new MemoryStream();
        await s.CopyToAsync(ms);
        return ms.ToArray();
    }
}
