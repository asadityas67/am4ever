// SPDX-License-Identifier: GPL-2.0-or-later
using System.Text;

namespace Am4ever.Tests.Board;

/// <summary>
/// Builds a minimal SMBIOS static-data region: one type-1 (System Information) structure with a
/// manufacturer and product string, then a type-127 end structure. Strings are NUL-separated and the
/// set ends with a double NUL.
/// </summary>
public static class SyntheticSmbios
{
    public static (byte[] Img, int Start, int End) OneType1(string manufacturer, string product)
    {
        var bytes = new List<byte>();

        var fmt1 = new byte[0x1B];     // SMBIOS 2.x System Information length
        fmt1[0] = 1;                   // type
        fmt1[1] = 0x1B;                // length
        fmt1[4] = 1;                   // manufacturer -> string #1
        fmt1[5] = 2;                   // product name -> string #2
        fmt1[24] = 6;                  // wake-up type (arbitrary non-string field)
        bytes.AddRange(fmt1);
        bytes.AddRange(Encoding.Latin1.GetBytes(manufacturer)); bytes.Add(0);
        bytes.AddRange(Encoding.Latin1.GetBytes(product)); bytes.Add(0);
        bytes.Add(0);                  // second NUL of the double terminator

        var fmt127 = new byte[4];
        fmt127[0] = 127; fmt127[1] = 4;
        bytes.AddRange(fmt127);
        bytes.Add(0); bytes.Add(0);    // no strings

        // AMI ships the static-data section with plenty of 0xFF slack after the table; give the
        // fixture the same so an identity rewrite that lengthens a string still fits.
        bytes.AddRange(Enumerable.Repeat((byte)0xFF, 0x40));

        var img = bytes.ToArray();
        return (img, 0, img.Length);
    }

    public static (string Manufacturer, string Product) ReadType1(byte[] img, int start)
    {
        var strs = ReadStrings(img, start);
        return (strs[img[start + 4] - 1], strs[img[start + 5] - 1]);
    }

    public static byte[] ReadType1Uuid(byte[] img, int start) => img[(start + 8)..(start + 24)];

    static List<string> ReadStrings(byte[] img, int start)
    {
        int len = img[start + 1];
        int p = start + len;
        var strs = new List<string>();
        while (!(img[p] == 0 && img[p + 1] == 0))
        {
            int s = p;
            while (img[p] != 0) p++;
            strs.Add(Encoding.Latin1.GetString(img, s, p - s));
            p++;                       // skip the NUL
        }
        return strs;
    }
}
