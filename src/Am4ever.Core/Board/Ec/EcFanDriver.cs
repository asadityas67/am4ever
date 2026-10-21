// SPDX-License-Identifier: GPL-2.0-or-later
using System.Text;
using Am4ever.Core.Rom;

namespace Am4ever.Core.Board.Ec;

/// <summary>
/// Builds "HpEcFanInit", the small x64 DXE driver that performs HP's EC fan hand-off on the HP 285 G3
/// (LPC 0x62/0x66 decode on, EC[0x7E]=0xE7, EC[0x80]&amp;=~2, every EC access bounded by a timeout), so
/// the fans run their own curve instead of full speed. A C# port of <c>hp_ecfan.py</c>'s PE + FFS
/// construction around a fixed, pre-assembled code blob.
/// </summary>
public static class EcFanDriver
{
    public static Guid FileGuid { get; } = Guid.Parse("6f9a1c2e-5b7d-4e41-9c3a-8534a0e7f285");
    public const string Name = "HpEcFanInit";

    // Position-independent x64, assembled (Keystone) from the sequence in hp_ecfan.py:
    //   push rbx; enable LPC D14F3x44 bit 30; EC[0x7E]=0xE7 (HPThermalDxe);
    //   EC[0x80] &= ~2 (ProjectDxe default); ec_write/ec_read/wait_ibf/wait_obf/flush_obf with timeouts.
    // Regenerating from source needs Keystone (bundled with the emulator milestone). The golden-FFS test
    // is the oracle that these bytes and the PE/FFS wrappers reproduce the reference driver exactly.
    const string CodeHex = "53b844a3008066baf80cef66bafc0ced0d00000040efb17eb3e7e856000000b180e87b000000720b24fd88c3b180e84200000031c05bc341b800000200e466a802740741ffc875f5f9c3f8c341b800000200e466a801750741ffc875f5f9c3f8c341b800010000e466a8017407e46241ffc875f3c3e8e7ffffffe8b8ffffff721fb081e666e8adffffff721488c8e662e8a2ffffff720988d8e662e897ffffffc3e8bbffffffe88cffffff7219b080e666e881ffffff720e88c8e662e88bffffff7203e462f8c3";

    /// <summary>The complete FFS driver file ready to insert into a DXE firmware volume.</summary>
    public static byte[] Build() => BuildFfs(BuildPe(Convert.FromHexString(CodeHex)));

    static byte[] BuildPe(byte[] code)
    {
        const int fileAlign = 0x200, sectionAlign = 0x1000;
        var raw = new byte[code.Length + (fileAlign - code.Length % fileAlign) % fileAlign];
        code.CopyTo(raw, 0);

        var hdr = new byte[0x200];
        hdr[0] = (byte)'M'; hdr[1] = (byte)'Z';
        Bin.W32(hdr, 0x3C, 0x80);                        // e_lfanew
        const int pe = 0x80;
        hdr[pe] = (byte)'P'; hdr[pe + 1] = (byte)'E';    // "PE\0\0"
        Bin.W16(hdr, pe + 4, 0x8664);                    // machine x64
        Bin.W16(hdr, pe + 6, 1);                         // one section
        Bin.W16(hdr, pe + 20, 0xF0);                     // SizeOfOptionalHeader
        Bin.W16(hdr, pe + 22, 0x0022);                   // EXECUTABLE_IMAGE | LARGE_ADDRESS_AWARE

        int o = pe + 24;
        int imageSize = sectionAlign + ((code.Length + sectionAlign - 1) / sectionAlign) * sectionAlign;
        hdr[o] = 0x0B; hdr[o + 1] = 0x02;                // PE32+ magic
        hdr[o + 2] = 14;                                 // MajorLinkerVersion
        Bin.W32(hdr, o + 4, (uint)raw.Length);           // SizeOfCode
        Bin.W32(hdr, o + 16, 0x1000);                    // AddressOfEntryPoint
        Bin.W32(hdr, o + 20, 0x1000);                    // BaseOfCode
        Bin.W64(hdr, o + 24, 0);                         // ImageBase
        Bin.W32(hdr, o + 32, sectionAlign);
        Bin.W32(hdr, o + 36, fileAlign);
        Bin.W32(hdr, o + 56, (uint)imageSize);           // SizeOfImage
        Bin.W32(hdr, o + 60, 0x200);                     // SizeOfHeaders
        Bin.W16(hdr, o + 68, 11);                        // Subsystem: EFI_BOOT_SERVICE_DRIVER
        Bin.W64(hdr, o + 72, 0x10000);                   // SizeOfStackReserve
        Bin.W64(hdr, o + 80, 0x1000);                    // SizeOfStackCommit
        Bin.W64(hdr, o + 88, 0x10000);                   // SizeOfHeapReserve
        Bin.W64(hdr, o + 96, 0x1000);                    // SizeOfHeapCommit
        Bin.W32(hdr, o + 108, 16);                       // NumberOfRvaAndSizes

        int s = o + 0xF0;                                // section table (16 data dirs are zero)
        Encoding.ASCII.GetBytes(".text").CopyTo(hdr, s);
        Bin.W32(hdr, s + 8, (uint)code.Length);          // VirtualSize
        Bin.W32(hdr, s + 12, 0x1000);                    // VirtualAddress
        Bin.W32(hdr, s + 16, (uint)raw.Length);          // SizeOfRawData
        Bin.W32(hdr, s + 20, 0x200);                     // PointerToRawData
        Bin.W32(hdr, s + 36, 0x60000020);                // CODE | EXECUTE | READ

        var image = new byte[hdr.Length + raw.Length];
        hdr.CopyTo(image, 0);
        raw.CopyTo(image, hdr.Length);
        return image;
    }

    static byte[] BuildFfs(byte[] pe)
    {
        var secs = new List<byte>();
        foreach (var (type, body) in new (byte Type, byte[] Body)[]
        {
            ((byte)0x13, new byte[] { 0x06, 0x08 }),                 // DXE depex: TRUE END
            ((byte)0x10, pe),                                        // PE32
            ((byte)0x15, Encoding.Unicode.GetBytes(Name + "\0")),    // UI name (UTF-16LE)
        })
        {
            while (secs.Count % 4 != 0) secs.Add(0);                 // 4-byte align each section
            int secSize = 4 + body.Length;
            secs.Add((byte)secSize); secs.Add((byte)(secSize >> 8)); secs.Add((byte)(secSize >> 16));
            secs.Add(type);
            secs.AddRange(body);
        }

        int size = 24 + secs.Count;
        var hdr = new byte[24];
        FileGuid.ToByteArray().CopyTo(hdr, 0);
        hdr[18] = 0x07;                                  // file type: driver
        Bin.W24(hdr, 20, (uint)size);
        int sum = 0;
        for (int i = 0; i < 24; i++) sum += hdr[i];      // [16],[17],[23] are still zero here
        hdr[16] = (byte)((0x100 - (sum & 0xFF)) & 0xFF); // header checksum
        hdr[17] = 0xAA;                                  // data checksum disabled
        hdr[23] = 0xF8;                                  // state: data valid

        var ffs = new byte[size];
        hdr.CopyTo(ffs, 0);
        secs.CopyTo(ffs, 24);
        return ffs;
    }
}
