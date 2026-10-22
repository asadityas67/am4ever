// SPDX-License-Identifier: GPL-2.0-or-later
using System.Text;

namespace Am4ever.App.Lab;

/// <summary>Read-only hex view: 16 bytes per line as <c>OFFSET  hex…  ascii</c>.</summary>
public static class HexDump
{
    public static IReadOnlyList<string> Format(byte[] data, int baseOffset = 0)
    {
        var lines = new List<string>();
        for (int i = 0; i < data.Length; i += 16)
        {
            int n = Math.Min(16, data.Length - i);
            var hex = new StringBuilder();
            var ascii = new StringBuilder();
            for (int j = 0; j < n; j++)
            {
                byte b = data[i + j];
                hex.Append(b.ToString("X2")).Append(' ');
                ascii.Append(b >= 0x20 && b < 0x7F ? (char)b : '.');
            }
            lines.Add($"{baseOffset + i:X8}  {hex.ToString().TrimEnd(),-47}  {ascii}");
        }
        return lines;
    }
}
