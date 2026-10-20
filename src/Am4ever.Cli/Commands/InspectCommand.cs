// SPDX-License-Identifier: GPL-2.0-or-later
using System.Text;
using Am4ever.Core.Rom;
using Am4ever.Core.Rom.Amd;

namespace Am4ever.Cli.Commands;

/// <summary>Read-only report on an AM4 ROM image: size, AMD directories and the AGESA string.</summary>
public static class InspectCommand
{
    public static int Run(string romPath, IConsoleIo io)
    {
        if (!File.Exists(romPath))
        {
            io.Err.WriteLine($"am4ever inspect: file not found: {romPath}");
            return 1;
        }

        RomImage rom;
        try
        {
            rom = RomImage.Load(File.ReadAllBytes(romPath));
        }
        catch (RomFormatException e)
        {
            io.Err.WriteLine($"am4ever inspect: {romPath} is not a valid AM4 ROM ({e.Message})");
            return 1;
        }

        io.Out.WriteLine($"ROM: {romPath}");
        io.Out.WriteLine($"size: {rom.Size / (1024 * 1024)} MiB");

        var dirs = AmdDirectory.FindAll(rom);
        io.Out.WriteLine($"AMD directories: {dirs.Count}");
        foreach (var d in dirs)
            io.Out.WriteLine($"  {d.Kind} @ {d.DirOffset:X6} ({d.Count} entries)");

        var agesa = FindAgesa(rom.Data);
        if (agesa is not null)
            io.Out.WriteLine($"AGESA: {agesa}");

        return 0;
    }

    static string? FindAgesa(byte[] data)
    {
        var marker = "AGESA!"u8;
        int i = data.AsSpan().IndexOf(marker);
        if (i < 0)
            return null;
        // read printable bytes (NULs separate the version words); stop at a run of non-text.
        var sb = new StringBuilder();
        for (int j = i; j < data.Length && j < i + 64; j++)
        {
            byte b = data[j];
            if (b == 0) sb.Append(' ');
            else if (b >= 0x20 && b < 0x7F) sb.Append((char)b);
            else break;
        }
        return sb.ToString().Trim();
    }
}
