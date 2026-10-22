// SPDX-License-Identifier: GPL-2.0-or-later
using Iced.Intel;

namespace Am4ever.App.Lab;

/// <summary>
/// Read-only 32-bit x86 disassembly of a code slice, via the managed Iced decoder — enough to eyeball
/// an override routine. Never throws on arbitrary bytes: undecodable bytes format as <c>(bad)</c>.
/// </summary>
public static class Disassembler
{
    public static IReadOnlyList<(ulong Address, string Text)> Decode(byte[] code, ulong rip = 0)
    {
        var decoder = Decoder.Create(32, new ByteArrayCodeReader(code), rip);
        var formatter = new NasmFormatter();
        var output = new StringOutput();
        var list = new List<(ulong, string)>();
        ulong end = rip + (ulong)code.Length;
        while (decoder.IP < end)
        {
            ulong ip = decoder.IP;
            var instr = decoder.Decode();
            formatter.Format(instr, output);
            list.Add((ip, output.ToStringAndReset()));
        }
        return list;
    }
}
