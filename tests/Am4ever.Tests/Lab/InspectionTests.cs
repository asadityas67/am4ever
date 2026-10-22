// SPDX-License-Identifier: GPL-2.0-or-later
using System.Linq;
using Am4ever.App.Lab;
using Am4ever.Tests.Board;

namespace Am4ever.Tests.Lab;

public class InspectionTests
{
    [Fact] public void Hex_FormatsOffsetBytesAscii()
    {
        var line = HexDump.Format(new byte[] { 0x41, 0x42, 0x43 })[0];
        Assert.StartsWith("00000000", line);
        Assert.Contains("41 42 43", line);
        Assert.Contains("ABC", line);
    }

    [Fact] public void Disasm_DecodesTheNeutralisedStub()
    {
        var text = string.Join(" ; ", Disassembler.Decode(new byte[] { 0x31, 0xC0, 0xC3 }).Select(i => i.Text));
        Assert.Contains("xor", text);
        Assert.Contains("ret", text);
    }

    [Fact] public void Disasm_DoesNotThrowOnGarbage()
    {
        var ex = Record.Exception(() => Disassembler.Decode(new byte[] { 0xFF, 0xFF, 0x00, 0x12, 0x9A }));
        Assert.Null(ex);
    }

    [Fact] public void TableRows_ListsTheCpmTables()
    {
        var img = SyntheticCpm.WithTables(("$A11", 0u, 0x20));
        Assert.Contains(TableRows.For(img), r => r.Contains("$A11"));
    }
}
