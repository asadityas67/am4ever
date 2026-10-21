// SPDX-License-Identifier: GPL-2.0-or-later
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace Am4ever.App.Lab;

/// <summary>
/// A simple expert view: open an OEM ROM (and optionally a donor), browse the PEI modules, see each
/// module's CPM tables and a byte-compare vs the donor, and run a module in the emulator. Code-only,
/// like the wizard. Intentionally minimal — the hex editor / disassembly / editors / export are future work.
/// </summary>
public sealed class BoardLabView : UserControl
{
    const uint Pinnacle = 0x00800F82;
    BoardAnalysis? _oem, _donor;

    public Button OpenOemButton { get; } = new() { Content = "Open OEM ROM…" };
    public Button OpenDonorButton { get; } = new() { Content = "Open donor ROM…" };
    readonly TextBlock _summary = new() { TextWrapping = TextWrapping.Wrap };
    readonly ListBox _modules = new() { MaxHeight = 220 };
    readonly TextBlock _detail = new() { TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Consolas, monospace") };
    readonly Button _emulate = new() { Content = "Run selected module in emulator", IsEnabled = false };

    public BoardLabView()
    {
        OpenOemButton.Click += async (_, _) => await Open(oem: true);
        OpenDonorButton.Click += async (_, _) => await Open(oem: false);
        _modules.SelectionChanged += (_, _) => ShowDetail();
        _emulate.Click += (_, _) => RunEmulator();

        Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(12), Spacing = 8,
            Children =
            {
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { OpenOemButton, OpenDonorButton } },
                _summary, _modules, _detail, _emulate,
            },
        };
    }

    async Task Open(bool oem)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;
        try
        {
            var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = false });
            if (files.Count == 0) return;
            await using var s = await files[0].OpenReadAsync();
            using var ms = new MemoryStream();
            await s.CopyToAsync(ms);
            var a = BoardAnalysis.Load(ms.ToArray(), 0xC00000, 0xE02000);
            if (oem) { _oem = a; _modules.ItemsSource = a.Modules().Select(m => m.Name).ToList(); }
            else _donor = a;
            RefreshSummary();
            ShowDetail();
        }
        catch (Exception ex) { _summary.Text = "Error: " + ex.Message; }
    }

    void RefreshSummary()
    {
        string S(string label, BoardAnalysis? a) => a is null ? $"{label}: (not loaded)" : $"{label}: {a.Summary().Size / (1024 * 1024)} MiB, {a.Summary().AmdDirectories} AMD dirs, AGESA {a.Summary().Agesa}";
        _summary.Text = S("OEM", _oem) + "\n" + S("Donor", _donor) +
            (BoardAnalysis.EmulatorAvailable ? "" : "\n(emulator host not built — Run is disabled)");
    }

    void ShowDetail()
    {
        _emulate.IsEnabled = false;
        if (_oem is null || _modules.SelectedItem is not string name) { _detail.Text = ""; return; }
        int index = ModuleIndex(name);
        var tags = _oem.CpmTables(name, index);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(name);
        sb.AppendLine(tags.Count > 0 ? "CPM tables: " + string.Join(" ", tags) : "(no CPM tables)");
        if (_donor is not null)
        {
            var d = _oem.CompareModule(_donor, name, index);
            sb.AppendLine(d.FirstDiff < 0 && d.SameLength ? "vs donor: identical"
                : d.DiffCount < 0 ? "vs donor: not present in both"
                : $"vs donor: {(d.SameLength ? "same size" : "different size")}, first diff at 0x{d.FirstDiff:X}, {d.DiffCount} bytes differ");
        }
        _detail.Text = sb.ToString();
        _emulate.IsEnabled = BoardAnalysis.EmulatorAvailable && tags.Count > 0;
    }

    void RunEmulator()
    {
        if (_oem is null || _modules.SelectedItem is not string name) return;
        try
        {
            var r = _oem.Emulate(name, ModuleIndex(name), Pinnacle);
            _detail.Text += r is null ? "\nemulator unavailable"
                : $"\nemulator (Pinnacle): entry=0x{r.Entry:X}, {r.Tables.Count} tables, writes_outside={r.WritesOutside}";
        }
        catch (Exception ex) { _detail.Text += "\nemulator error: " + ex.Message; }
    }

    int ModuleIndex(string name)
    {
        var mods = _oem!.Modules();
        int sel = _modules.SelectedIndex, idx = 0;
        for (int i = 0; i < sel && i < mods.Count; i++) if (mods[i].Name == name) idx++;
        return idx;
    }
}
