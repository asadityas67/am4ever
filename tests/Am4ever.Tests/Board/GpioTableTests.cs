// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Board.Cpm;
using Am4ever.Core.Rom;

namespace Am4ever.Tests.Board;

public class GpioTableTests
{
    [Fact] public void WriteThenRead_RoundTrips()
    {
        var (img, t) = SyntheticCpm.OneGpioTable(size: 0x40);
        var entries = new Dictionary<ushort, ushort> { [0] = 0x1234, [5] = 0x00AB };
        var order = new List<ushort> { 0, 5 };
        GpioTable.Write(img, t, entries, order);
        var (back, backOrder) = GpioTable.Read(img, t);
        Assert.Equal(entries, back);
        Assert.Equal(order, backOrder);
    }

    [Fact] public void Merge_KeepsHpValueWhereHpWiresPin()
    {
        var hp = new Dictionary<ushort, ushort> { [10] = 0x00AA };
        var baseG = new Dictionary<ushort, ushort> { };                  // donor base: pin 10 unset
        var family = new Dictionary<ushort, ushort> { [10] = 0x00BB };   // donor family wants 0xBB
        var (merged, _, _) = GpioTable.Merge(hp, new List<ushort> { 10 }, baseG, family);
        Assert.Equal(0x00AA, merged[10]);                                // HP wins: HP wires it, base doesn't
    }

    [Fact] public void Merge_AppliesFamilyDeltaWhereHpDoesNot()
    {
        var hp = new Dictionary<ushort, ushort> { };
        var baseG = new Dictionary<ushort, ushort> { };
        var family = new Dictionary<ushort, ushort> { [12] = 0x00CC };
        var (merged, order, _) = GpioTable.Merge(hp, new List<ushort>(), baseG, family);
        Assert.Equal(0x00CC, merged[12]);
        Assert.Contains((ushort)12, order);
    }

    [Fact] public void Write_Overflow_Throws()
    {
        var (img, t) = SyntheticCpm.OneGpioTable(size: 0x18);            // room for ~1 entry + terminator
        var big = Enumerable.Range(0, 20).ToDictionary(i => (ushort)i, i => (ushort)i);
        Assert.Throws<RomEditException>(() => GpioTable.Write(img, t, big, big.Keys.ToList()));
    }
}
