// SPDX-License-Identifier: GPL-2.0-or-later
using System.Security.Cryptography;
using Am4ever.Core.Profile;
using Am4ever.Core.Rom;
using Am4ever.Core.Rom.Amd;
using Am4ever.Core.Rom.Compression;
using Am4ever.Core.Rom.Fv;

namespace Am4ever.App.Lab;

/// <summary>
/// Auto-detects the fields of a donor adapter from a donor ROM, filling what it can and leaving the rest
/// (the override sites, the identity strings) blank for the expert. Best-effort and honest: a field it
/// cannot determine stays null. Reuses the Core parsers; never guesses.
/// </summary>
public static class AdapterDetector
{
    const uint FvSignature = 0x4856465F;

    public static AdapterDraft Detect(byte[] donorRom)
    {
        var draft = new AdapterDraft
        {
            Sha256 = Convert.ToHexString(SHA256.HashData(donorRom)).ToLowerInvariant(),
        };

        RomImage rom;
        try { rom = RomImage.Load(donorRom); }
        catch (RomFormatException) { return draft; }   // not an AM4 image: SHA only

        DetectDxe(donorRom, draft);
        DetectPei(rom, donorRom, draft);
        return draft;
    }

    static void DetectDxe(byte[] rom, AdapterDraft draft)
    {
        foreach (int fv in FindFvOffsets(rom))
        {
            try
            {
                foreach (var f in new FirmwareVolume(rom, fv).Files())
                {
                    var guided = f.Sections().FirstOrDefault(s => s.Type == SectionType.GuidDefined);
                    if (guided is null) continue;
                    try { GuidedSection.DecompressLzmaGuided(rom, guided); }   // the DXE container is LZMA-wrapped
                    catch { continue; }
                    draft.DxeFvOffset = fv;
                    draft.DxeFileGuid = f.Guid.ToString();
                    return;
                }
            }
            catch (RomFormatException) { /* not a usable FV; keep scanning */ }
        }
    }

    static void DetectPei(RomImage rom, byte[] raw, AdapterDraft draft)
    {
        // uncompressed PEI (HP-style): an FV that directly holds a CPM OEM PEIM
        foreach (int fv in FindFvOffsets(raw))
        {
            if (TryModules(raw, fv, draft))
            {
                draft.Compression = null;
                draft.BootBlobOffset = fv;
                draft.BootFvOffset = fv;
                return;
            }
        }

        // compressed PEI (amd-zlib): a BIOS-directory region that decompresses to a CPM-bearing FV
        foreach (var e in AmdDirectory.FindAll(rom).SelectMany(d => d.Entries))
        {
            int off = (int)(e.Location & (ulong)(raw.Length - 1));
            if (off <= 0 || off + 0x18 > raw.Length) continue;
            long len = Bin.U32(raw, off + 0x14);
            if (len <= 0 || off + 0x100 + len > raw.Length) continue;
            byte[] pei;
            try { (_, pei) = PspZlib.Decompress(raw.AsSpan(off, 0x100 + (int)len)); }
            catch { continue; }
            int innerFv = FindFvOffsets(pei).FirstOrDefault(fv => TryModules(pei, fv, draft), -1);
            if (innerFv < 0) continue;
            draft.Compression = "amd-zlib";
            draft.BootBlobOffset = off;
            draft.BootBlobLength = 0x100 + (int)len;
            draft.BootFvOffset = innerFv;
            return;
        }
    }

    /// <summary>If the FV at <paramref name="fvOffset"/> holds CPM OEM PEIMs, record them on the draft and return true.</summary>
    static bool TryModules(byte[] buf, int fvOffset, AdapterDraft draft)
    {
        List<ModuleEntry> mods;
        try
        {
            mods = new List<ModuleEntry>();
            var counts = new Dictionary<string, int>();
            int plainSeen = 0;
            foreach (var f in new FirmwareVolume(buf, fvOffset).Files())
            {
                var name = f.Name();
                if (!name.StartsWith("AmdCpm", StringComparison.Ordinal) || !name.Contains("InitPeim")) continue;
                int idx = counts.GetValueOrDefault(name);
                counts[name] = idx + 1;
                string fam = name.Contains("Rv") ? "RV" : name.Contains("Rn") ? "RN" : name.Contains("Cz") ? "CZN"
                    : (plainSeen++ == 0 ? "ZP" : "MTS");
                mods.Add(new ModuleEntry(name, fam, idx));
            }
        }
        catch (RomFormatException) { return false; }
        if (!mods.Any(m => m.Name.StartsWith("AmdCpmOem", StringComparison.Ordinal))) return false;
        draft.Modules = mods;
        return true;
    }

    static IEnumerable<int> FindFvOffsets(byte[] buf)
    {
        for (int i = 0x28; i + 4 <= buf.Length; i++)
            if (Bin.U32(buf, i) == FvSignature) yield return i - 0x28;
    }
}
