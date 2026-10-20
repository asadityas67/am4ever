// SPDX-License-Identifier: GPL-2.0-or-later
namespace Am4ever.Tests.Rom;

/// <summary>
/// Locations of the developer's own reference ROM files, taken from the folder named by
/// AM4EVER_TEST_ROMS. These files are never committed; tests that need them use [RomFact].
/// </summary>
public static class TestRoms
{
    const string FolderVar = "AM4EVER_TEST_ROMS";

    public static string? Folder => Environment.GetEnvironmentVariable(FolderVar);

    public static string? AsrockP1043 => Resolve("ASRock_AB350MPro4_P10.43.bin");
    public static string? HpF65 => Resolve("HP_08434_F65_raw.bin");

    static string? Resolve(string name)
    {
        var folder = Folder;
        if (string.IsNullOrEmpty(folder)) return null;
        var path = Path.Combine(folder, name);
        return File.Exists(path) ? path : null;
    }

    /// <summary>A skip reason when the reference ROMs are unavailable, else null.</summary>
    public static string? SkipReason =>
        string.IsNullOrEmpty(Folder)
            ? "set AM4EVER_TEST_ROMS to a folder with your own ROM files to run reference tests"
            : (AsrockP1043 is null || HpF65 is null)
                ? "AM4EVER_TEST_ROMS is set but the expected ROM file(s) are missing"
                : null;
}
