// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Build;
using Am4ever.Emu;
using Am4ever.Core.Rom;
using Am4ever.Core.Rom.Compression;
using Am4ever.Core.Rom.Fv;
using Am4ever.Tests.Rom;

namespace Am4ever.Tests.Emu;

public class PeiVerificationTests
{
    static byte[] Peim(byte[] image, string name, int index)
    {
        uint clen = Bin.U32(image, 0xE02000 + 0x14);
        var (_, pei) = PspZlib.Decompress(image.AsSpan(0xE02000, 0x100 + (int)clen));
        var fv = new FirmwareVolume(pei, 0x20);
        var (s, e) = fv.Files().Where(f => f.Name() == name).ToList()[index].ImageSection();
        return pei[s..e];
    }

    [EmuRomFact] public void BuiltImage_OemPeimsVerifyUnderServedFamilies()
    {
        var image = BuildPipeline.Build(
            File.ReadAllBytes(TestRoms.AsrockP1043!), File.ReadAllBytes(TestRoms.HpF65!),
            BundledData.LoadProfile("hp-285-g3-8434"), BundledData.LoadAdapter("asrock-ab350m-pro4-p10.43"), null).Image;

        // (module, index, a family it serves, its neutralised override offset)
        var cases = new (string Name, int Idx, uint Cpuid, int Override)[]
        {
            ("AmdCpmOemInitPeim", 0, 0x00800F82, 0x2D6),     // Summit/Pinnacle
            ("AmdCpmOemRvInitPeim", 0, 0x00810F10, 0x394),   // Raven/Picasso
        };
        foreach (var c in cases)
        {
            var r = PeiEmulator.Run(Peim(image, c.Name, c.Idx), c.Cpuid, c.Override);
            Assert.Equal(0u, r.Entry);                       // ran to success under this family
            Assert.Equal(0, r.WritesOutside);                // no writes outside image/stack/heap
            Assert.True(r.OverrideOk, $"{c.Name} override @0x{c.Override:X} not neutralised");
            Assert.Contains(r.Tables, t => t.StartsWith("$A10"));
            Assert.Contains(r.Tables, t => t.StartsWith("$A26"));
        }
    }
}
