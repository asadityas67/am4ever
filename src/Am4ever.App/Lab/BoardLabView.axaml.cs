// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.App.Controls;
using Am4ever.App.Feedback;
using Am4ever.Core.Build;
using Am4ever.Core.Profile;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace Am4ever.App.Lab;

/// <summary>
/// The expert view: open an OEM ROM (and optionally a donor), browse the PEI modules, see each
/// module's CPM tables and a byte-compare vs the donor, run a module in the emulator — and, in the
/// Profile Composer section, discover/add operations, test-build, and export a shareable profile JSON,
/// with read-only hex/disassembly/table inspection and a warning-gated editable table. Code-only.
/// </summary>
public sealed class BoardLabView : UserControl
{
    const uint Pinnacle = 0x00800F82;
    static readonly string[] BundledAdapters = { "asrock-ab350m-pro4-p10.43" };

    BoardAnalysis? _oem, _donor;
    DonorAdapter? _labAdapter;
    readonly IToastService _toasts;
    readonly ComposerCore _composer = new();
    bool _composerOemSet, _composerAdapterSet;
    System.Collections.Generic.List<OperationSpec> _suggestions = new();

    public Button OpenOemButton { get; } = new() { Content = "Open OEM ROM…" };
    public Button OpenDonorButton { get; } = new() { Content = "Open donor ROM…" };
    readonly TextBlock _summary = new() { TextWrapping = TextWrapping.Wrap };
    readonly ListBox _modules = new() { MaxHeight = 160 };
    readonly TextBlock _detail = new() { TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Consolas, monospace") };
    readonly Button _emulate = new() { Content = "Run selected module in emulator", IsEnabled = false };

    // --- composer ---
    readonly ComboBox _adapterBox = new() { PlaceholderText = "choose donor adapter…" };
    readonly Button _discoverButton = new() { Content = "Discover operations", IsEnabled = false };
    readonly ListBox _discovery = new() { MaxHeight = 140 };
    readonly Button _addButton = new() { Content = "Add →", IsEnabled = false };
    readonly ListBox _ops = new() { MaxHeight = 140 };
    readonly Button _removeButton = new() { Content = "Remove", IsEnabled = false };
    readonly TextBox _json = new() { IsReadOnly = true, AcceptsReturn = true, MaxHeight = 200, FontFamily = new FontFamily("Consolas, monospace") };
    readonly TextBlock _composerStatus = new() { TextWrapping = TextWrapping.Wrap };
    readonly TextBlock _tableEditWarning = new()
    {
        Text = "⚠ Editing tables is for experts — a wrong value can brick a board. Your edits become profile operations.",
        TextWrapping = TextWrapping.Wrap, IsVisible = false, FontWeight = FontWeight.Bold,
    };

    public Button TestButton { get; } = new() { Content = "Test build", IsEnabled = false };
    public Button ExportButton { get; } = new() { Content = "Export JSON…" };
    public Button ImportButton { get; } = new() { Content = "Import JSON…" };
    public ToggleButton TableEditToggle { get; } = new() { Content = "Edit table" };
    public SegmentedProgressBar ComposerProgressBar { get; } = new() { IsVisible = false };
    public bool LastTableEditWarningShown { get; private set; }

    // --- donor adapter authoring ---
    byte[]? _donorBytes;
    AdapterDraft _adapterDraft = new();
    readonly TextBox _ovFamily = new() { Watermark = "family", Width = 80 };
    readonly TextBox _ovOffset = new() { Watermark = "offset", Width = 80 };
    readonly TextBox _ovExpected = new() { Watermark = "expected hex", Width = 150 };
    readonly TextBox _adapterJson = new() { IsReadOnly = true, AcceptsReturn = true, MaxHeight = 160, FontFamily = new FontFamily("Consolas, monospace") };
    readonly Button _importAdapterButton = new() { Content = "Import adapter…" };
    public Button DetectAdapterButton { get; } = new() { Content = "Detect from donor", IsEnabled = false };
    public Button AddOverrideButton { get; } = new() { Content = "Add override" };
    public Button ExportAdapterButton { get; } = new() { Content = "Export adapter…" };

    public BoardLabView() : this(new ToastService()) { }

    public BoardLabView(IToastService toasts)
    {
        _toasts = toasts;
        OpenOemButton.Click += async (_, _) => await Open(oem: true);
        OpenDonorButton.Click += async (_, _) => await Open(oem: false);
        _modules.SelectionChanged += (_, _) => ShowDetail();
        _emulate.Click += (_, _) => RunEmulator();

        _adapterBox.ItemsSource = BundledAdapters;
        _adapterBox.SelectionChanged += (_, _) =>
        {
            if (_adapterBox.SelectedItem is not string id) return;
            try { _composer.SetAdapterId(id); _labAdapter = BundledData.LoadAdapter(id); _composerAdapterSet = true; }
            catch (Exception ex) { _composerStatus.Text = ex.Message; }
            UpdateComposerState();
        };
        _discoverButton.Click += (_, _) => RunDiscover();
        _discovery.SelectionChanged += (_, _) => _addButton.IsEnabled = _discovery.SelectedIndex >= 0;
        _addButton.Click += (_, _) => AddSelectedSuggestion();
        _ops.SelectionChanged += (_, _) => _removeButton.IsEnabled = _ops.SelectedIndex >= 0;
        _removeButton.Click += (_, _) => { if (_ops.SelectedIndex >= 0) { _composer.RemoveOperation(_ops.SelectedIndex); RefreshOps(); } };
        TestButton.Click += async (_, _) => await RunTest();
        ExportButton.Click += async (_, _) => await Export();
        ImportButton.Click += async (_, _) => await Import();
        TableEditToggle.IsCheckedChanged += (_, _) => OnTableEditToggled();
        DetectAdapterButton.Click += (_, _) => DetectAdapter();
        AddOverrideButton.Click += (_, _) => AddOverride();
        ExportAdapterButton.Click += async (_, _) => await ExportAdapter();
        _importAdapterButton.Click += async (_, _) => await ImportAdapter();

        var adapter = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = "— Donor Adapter Authoring —", FontWeight = FontWeight.Bold },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { DetectAdapterButton, ExportAdapterButton, _importAdapterButton } },
                new TextBlock { Text = "add override site:" },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { _ovFamily, _ovOffset, _ovExpected, AddOverrideButton } },
                new TextBlock { Text = "adapter JSON:" },
                _adapterJson,
            },
        };

        var composer = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = "— Profile Composer —", FontWeight = FontWeight.Bold },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _adapterBox, _discoverButton } },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _discovery, _addButton } },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { _ops, _removeButton } },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { TestButton, ExportButton, ImportButton, TableEditToggle } },
                _tableEditWarning,
                ComposerProgressBar,
                _composerStatus,
                new TextBlock { Text = "profile JSON:" },
                _json,
            },
        };

        Content = new ScrollViewer
        {
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(12), Spacing = 8,
                Children =
                {
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { OpenOemButton, OpenDonorButton } },
                    _summary, _modules, _detail, _emulate,
                    composer,
                    adapter,
                },
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
            var bytes = ms.ToArray();
            if (oem)
            {
                _oem = BoardAnalysis.LoadOem(bytes);           // auto-detect the PEI volume
                _modules.ItemsSource = _oem.Modules().Select(m => m.Name).ToList();
                _composer.SetOemDump(bytes);
                _composerOemSet = true;
            }
            else
            {
                if (_labAdapter is null) { _summary.Text = "Choose a donor adapter first (Profile Composer)."; return; }
                _donor = BoardAnalysis.LoadDonor(bytes, _labAdapter);   // offsets from the adapter
                _composer.SetDonor(bytes);
                _donorBytes = bytes;
                DetectAdapterButton.IsEnabled = true;
            }
            RefreshSummary();
            ShowDetail();
            UpdateComposerState();
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

    // --- composer behaviour ---
    void UpdateComposerState()
    {
        TestButton.IsEnabled = _composerOemSet && _composerAdapterSet;
        _discoverButton.IsEnabled = _composerOemSet && _composerAdapterSet && _donor is not null;
        RefreshJson();
    }

    void RunDiscover()
    {
        try
        {
            _suggestions = _composer.Discover().ToList();
            _discovery.ItemsSource = _suggestions.Select(Summary).ToList();
        }
        catch (Exception ex) { _composerStatus.Text = ex.Message; }
    }

    void AddSelectedSuggestion()
    {
        int i = _discovery.SelectedIndex;
        if (i < 0 || i >= _suggestions.Count) return;
        _composer.AddOperation(_suggestions[i]);
        RefreshOps();
    }

    void RefreshOps()
    {
        _ops.ItemsSource = _composer.Draft.Operations.Select(Summary).ToList();
        _removeButton.IsEnabled = false;
        RefreshJson();
    }

    void RefreshJson()
    {
        try { _json.Text = System.Text.Encoding.UTF8.GetString(_composer.ToJson()); }
        catch (Exception ex) { _json.Text = "(cannot render: " + ex.Message + ")"; }
    }

    static string Summary(OperationSpec op) => op.Family is { } f ? $"{op.Op} [{f}]" : op.Op;

    async Task RunTest()
    {
        ComposerProgressBar.IsVisible = true;
        ComposerProgressBar.Value = 0;
        TestButton.IsEnabled = false;
        var progress = new Progress<BuildProgress>(p => ComposerProgressBar.Value = p.Total == 0 ? 0 : (double)p.Done / p.Total);
        var r = await Task.Run(() => _composer.TestBuild(null, progress));
        TestButton.IsEnabled = true;
        _composerStatus.Text = r.Ok ? "✓ Profile builds — report ready." : "✕ " + r.Error;
        _toasts.Show(r.Ok ? "✓ Profile builds" : "✕ Build failed: " + r.Error, r.Ok ? ToastKind.Success : ToastKind.Error);
    }

    async Task Export()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;
        var save = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { SuggestedFileName = "profile.json" });
        if (save is null) return;
        try { File.WriteAllBytes(save.Path.LocalPath, _composer.ToJson()); _toasts.Show("✓ Profile exported", ToastKind.Success); }
        catch (Exception ex) { _composerStatus.Text = ex.Message; }
    }

    async Task Import()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = false });
        if (files.Count == 0) return;
        try
        {
            await using var s = await files[0].OpenReadAsync();
            using var ms = new MemoryStream();
            await s.CopyToAsync(ms);
            _composer.Import(ms.ToArray());
            RefreshOps();
            _toasts.Show("✓ Profile imported", ToastKind.Success);
        }
        catch (Exception ex) { _composerStatus.Text = "Import failed: " + ex.Message; _toasts.Show("✕ Import failed", ToastKind.Error); }
    }

    // --- donor adapter authoring ---
    void DetectAdapter()
    {
        if (_donorBytes is null) return;
        _adapterDraft = AdapterDetector.Detect(_donorBytes);
        RefreshAdapterJson();
        _toasts.Show("✓ Adapter detected — fill the override sites", ToastKind.Info);
    }

    void AddOverride()
    {
        if (!int.TryParse(_ovOffset.Text, out var off)) { _toasts.Show("✕ override offset must be a number", ToastKind.Error); return; }
        _adapterDraft.Overrides.Add(new OverrideDraft(_ovFamily.Text ?? "", off, (_ovExpected.Text ?? "").Replace(" ", "")));
        RefreshAdapterJson();
    }

    void RefreshAdapterJson()
    {
        try { _adapterJson.Text = System.Text.Encoding.UTF8.GetString(AdapterWriter.Write(_adapterDraft.ToAdapter())); }
        catch (Exception ex) { _adapterJson.Text = "(cannot render: " + ex.Message + ")"; }
    }

    async Task ExportAdapter()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;
        var save = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { SuggestedFileName = "adapter.json" });
        if (save is null) return;
        try { File.WriteAllBytes(save.Path.LocalPath, AdapterWriter.Write(_adapterDraft.ToAdapter())); _toasts.Show("✓ Adapter exported", ToastKind.Success); }
        catch (Exception ex) { _toasts.Show("✕ " + ex.Message, ToastKind.Error); }
    }

    async Task ImportAdapter()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null) return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = false });
        if (files.Count == 0) return;
        try
        {
            await using var s = await files[0].OpenReadAsync();
            using var ms = new MemoryStream();
            await s.CopyToAsync(ms);
            _adapterDraft = AdapterDraft.FromAdapter(ProfileLoader.LoadAdapter(ms.ToArray()));
            RefreshAdapterJson();
            _toasts.Show("✓ Adapter imported", ToastKind.Success);
        }
        catch (Exception ex) { _toasts.Show("✕ Import failed: " + ex.Message, ToastKind.Error); }
    }

    void OnTableEditToggled()
    {
        if (TableEditToggle.IsChecked == true)
        {
            LastTableEditWarningShown = true;               // a warning is shown on every enable
            _tableEditWarning.IsVisible = true;
            _composer.ConfirmTableEdit = () => true;        // the inline banner is the acknowledgement
            _composer.EnableTableEdit();
        }
        else
        {
            _tableEditWarning.IsVisible = false;
            _composer.DisableTableEdit();
        }
    }
}
