// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Rom;
using Am4ever.Core.Rom.Amd;

namespace Am4ever.Tests.Rom;

public class AmdDirectoryTests
{
    const uint PspMagic = 0x50535024;   // $ P S P

    static void WriteDir(RomImage rom, int off, uint magic, int count, bool goodChecksum)
    {
        Bin.W32(rom.Data, off, magic);
        Bin.W32(rom.Data, off + 8, (uint)count);          // count
        Bin.W32(rom.Data, off + 12, 0);                   // info
        for (int i = 0; i < count; i++)                   // 16-byte PSP entries, left zero
            Bin.W32(rom.Data, off + 16 + i * 16, 0x30);
        var dir = new AmdDirectory(rom, off);
        dir.FixChecksum(rom);
        if (!goodChecksum)
            Bin.W32(rom.Data, off + 4, Bin.U32(rom.Data, off + 4) ^ 0xDEAD);
    }

    [Fact] public void FixChecksum_RoundTripsThroughValidation()
    {
        var rom = RomImage.Load(new byte[16 * 1024 * 1024]);
        WriteDir(rom, 0x1000, PspMagic, count: 3, goodChecksum: false);
        Assert.False(new AmdDirectory(rom, 0x1000).ChecksumValid);
        new AmdDirectory(rom, 0x1000).FixChecksum(rom);
        Assert.True(new AmdDirectory(rom, 0x1000).ChecksumValid);
    }

    [Fact] public void FindAll_SkipsDirectoryWithBadChecksum()
    {
        var rom = RomImage.Load(new byte[16 * 1024 * 1024]);
        WriteDir(rom, 0x1000, PspMagic, count: 2, goodChecksum: true);
        WriteDir(rom, 0x2000, PspMagic, count: 2, goodChecksum: false);
        var dirs = AmdDirectory.FindAll(rom);
        Assert.Single(dirs);
        Assert.Equal(0x1000, dirs[0].DirOffset);
    }

    [RomFact] public void Asrock_HasComboDirectories()
    {
        var rom = RomImage.Load(File.ReadAllBytes(TestRoms.AsrockP1043!));
        var dirs = AmdDirectory.FindAll(rom);
        var kinds = dirs.Select(d => d.Kind).ToList();
        Assert.Contains(AmdDirKind.Psp, kinds);
        Assert.Contains(AmdDirKind.Bios, kinds);
        Assert.True(dirs.Count >= 10, $"expected a combo image's many directories, got {dirs.Count}");
    }

    [RomFact] public void Hp_HasExpectedDirectoryCount()
    {
        var rom = RomImage.Load(File.ReadAllBytes(TestRoms.HpF65!));
        Assert.Equal(9, AmdDirectory.FindAll(rom).Count);
    }
}
