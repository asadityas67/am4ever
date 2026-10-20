// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Board.Cpm;
using Am4ever.Core.Rom;

namespace Am4ever.Tests.Board;

/// <summary>
/// Builds tiny in-memory CPM table images for unit tests. A CPM table is a 4-byte tag, a 16-bit
/// size, two revision bytes, a 32-bit platform mask and a 32-bit attributes word, then the body.
/// Helpers for GPIO and DXIO table bodies are added by the tasks that need them.
/// </summary>
public static class SyntheticCpm
{
    /// <summary>A buffer of CPM tables laid out back to back, each (tag, platform mask, total size).</summary>
    public static byte[] WithTables(params (string Tag, uint Pmask, int Size)[] tables)
    {
        var buf = new byte[tables.Sum(t => t.Size)];
        int o = 0;
        foreach (var (tag, pmask, size) in tables)
        {
            WriteHeader(buf, o, tag, pmask, size);
            o += size;
        }
        return buf;
    }

    public static void WriteHeader(byte[] buf, int o, string tag, uint pmask, int size)
    {
        buf[o] = (byte)tag[0];
        buf[o + 1] = (byte)tag[1];
        buf[o + 2] = (byte)tag[2];
        buf[o + 3] = (byte)tag[3];
        Bin.W16(buf, o + 4, (ushort)size);
        buf[o + 6] = 1;                 // feature revision
        buf[o + 7] = 2;                 // content revision
        Bin.W32(buf, o + 8, pmask);
        Bin.W32(buf, o + 12, 0);        // attributes
    }

    public static void SetSizeField(byte[] img, string tag, ushort size) =>
        Bin.W16(img, FindTag(img, tag) + 4, size);

    public static int FindTag(byte[] img, string tag)
    {
        for (int o = 0; o + 4 <= img.Length; o++)
            if (img[o] == (byte)tag[0] && img[o + 1] == (byte)tag[1]
                && img[o + 2] == (byte)tag[2] && img[o + 3] == (byte)tag[3])
                return o;
        throw new InvalidOperationException($"tag {tag} not found in synthetic image");
    }

    /// <summary>A single $A10 GPIO table of the given total size, with an empty body.</summary>
    public static (byte[] Img, CpmTable Table) OneGpioTable(int size)
    {
        var img = WithTables(("$A10", 0u, size));
        return (img, CpmImage.FindTable(img, "$A10"));
    }

    /// <summary>
    /// A single $A26 DXIO table: 0x14 header+SocketId, then <paramref name="descriptors"/> descriptors
    /// of <paramref name="descSize"/> bytes, then a DDI display block of <paramref name="ddiBytes"/> bytes.
    /// </summary>
    public static (byte[] Img, CpmTable Table) OneDxioTable(int descriptors, int descSize, int ddiBytes = 0)
    {
        int size = 0x14 + descriptors * descSize + ddiBytes;
        var img = WithTables(("$A26", 0u, size));
        return (img, CpmImage.FindTable(img, "$A26"));
    }
}
