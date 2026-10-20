// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Tests.Rom;

namespace Am4ever.Tests.Reference;

/// <summary>Locates the HP 285 G3 T5 reference image (never committed) for the acceptance test.</summary>
public static class T5Reference
{
    const string Name = "HP285G3_ComboAM4v2_T5.bin";

    public static string? Path
    {
        get
        {
            var folder = TestRoms.Folder;
            if (string.IsNullOrEmpty(folder)) return null;
            foreach (var p in new[]
            {
                System.IO.Path.Combine(folder, Name),
                System.IO.Path.Combine(folder, "..", "build", Name),
            })
                if (File.Exists(p)) return p;
            return null;
        }
    }
}

/// <summary>A Fact that runs only when the reference ROMs and the T5 image are present.</summary>
public sealed class T5FactAttribute : FactAttribute
{
    public T5FactAttribute() =>
        Skip = TestRoms.SkipReason ?? (T5Reference.Path is null
            ? $"place {nameof(T5Reference)} image next to the reference ROMs (or in ../build) to run the acceptance test"
            : null);
}
