// SPDX-License-Identifier: GPL-2.0-or-later
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Am4ever.App.Theme;

/// <summary>
/// Remembers the theme choice in <c>&lt;config dir&gt;/am4ever/settings.json</c>.
/// Only the theme is remembered; the risk acknowledgement never is.
/// </summary>
public sealed class ThemeSettingsStore
{
    readonly string? _path;
    ThemeMode _memory = ThemeMode.FollowSystem;

    public ThemeSettingsStore(string path) => _path = path;

    ThemeSettingsStore() => _path = null;

    /// <summary>A store that keeps the mode in memory only (tests, previews).</summary>
    public static ThemeSettingsStore InMemory() => new();

    /// <summary>The store at the platform's per-user config location.</summary>
    public static ThemeSettingsStore Default() => new(Path.Combine(ConfigDir(), "am4ever", "settings.json"));

    public ThemeMode Load()
    {
        if (_path is null)
            return _memory;
        try
        {
            var node = JsonNode.Parse(File.ReadAllText(_path));
            var value = node?["theme"]?.GetValue<string>();
            return Enum.TryParse<ThemeMode>(value, ignoreCase: false, out var mode) ? mode : ThemeMode.FollowSystem;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException
                                    or InvalidOperationException or FormatException)
        {
            return ThemeMode.FollowSystem;
        }
    }

    public void Save(ThemeMode mode)
    {
        _memory = mode;
        if (_path is null)
            return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, new JsonObject { ["theme"] = mode.ToString() }.ToJsonString());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Not being able to remember the theme is harmless.
        }
    }

    static string ConfigDir()
    {
        if (OperatingSystem.IsWindows())
            return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        return !string.IsNullOrEmpty(xdg)
            ? xdg
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
    }
}
