// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Rom;

namespace Am4ever.Core.Board.Cpm;

/// <summary>
/// The FCH GPIO table ($A10 / $A19): a list of {UInt16 pin, UInt16 setting} entries in the table
/// body, terminated by a pin of 0xFF. <see cref="Merge"/> applies a donor's per-CPU-family deltas on
/// top of the OEM table while keeping the OEM value on any pin the OEM wires itself (ports the
/// <c>merge_gpio</c> rule from the Python reference).
/// </summary>
public static class GpioTable
{
    const ushort Terminator = 0xFF;

    public static (Dictionary<ushort, ushort> Entries, List<ushort> Order) Read(ReadOnlySpan<byte> img, CpmTable t)
    {
        var entries = new Dictionary<ushort, ushort>();
        var order = new List<ushort>();
        for (int p = t.BodyOffset; p + 4 <= t.End; p += 4)
        {
            ushort pin = (ushort)(img[p] | (img[p + 1] << 8));
            if (pin == Terminator) break;
            ushort state = (ushort)(img[p + 2] | (img[p + 3] << 8));
            entries[pin] = state;
            order.Add(pin);
        }
        return (entries, order);
    }

    public static void Write(byte[] img, CpmTable t, IReadOnlyDictionary<ushort, ushort> entries, IReadOnlyList<ushort> order)
    {
        int bodyLength = t.Size - CpmTable.HeaderLength;
        var body = new byte[bodyLength];
        int p = 0;
        foreach (var pin in order)
        {
            if (p + 4 > bodyLength)
                throw new RomEditException(
                    $"GPIO table at 0x{t.Offset:X} overflows: {order.Count} entries plus terminator do not fit in {bodyLength} body bytes");
            body[p] = (byte)pin;
            body[p + 1] = (byte)(pin >> 8);
            body[p + 2] = (byte)entries[pin];
            body[p + 3] = (byte)(entries[pin] >> 8);
            p += 4;
        }
        if (p + 4 > bodyLength)
            throw new RomEditException(
                $"GPIO table at 0x{t.Offset:X} overflows: no room for the terminator after {order.Count} entries in {bodyLength} body bytes");
        body[p] = (byte)Terminator;      // pin 0xFF, state 0
        Array.Copy(body, 0, img, t.BodyOffset, bodyLength);
    }

    /// <summary>
    /// The OEM table (<paramref name="hp"/>) with the donor's per-family deltas merged in. A pin the
    /// OEM wires itself keeps its OEM value even when the donor family sets it differently.
    /// </summary>
    public static (Dictionary<ushort, ushort> Entries, List<ushort> Order, IReadOnlyList<string> Notes) Merge(
        IReadOnlyDictionary<ushort, ushort> hp, IReadOnlyList<ushort> hpOrder,
        IReadOnlyDictionary<ushort, ushort> baseGpio, IReadOnlyDictionary<ushort, ushort> family,
        string familyName = "")
    {
        var tag = string.IsNullOrEmpty(familyName) ? "" : $" ({familyName})";
        var outGpio = new Dictionary<ushort, ushort>(hp);
        var order = new List<ushort>(hpOrder);
        var notes = new List<string>();

        foreach (var (pin, val) in family)
        {
            bool inHp = hp.TryGetValue(pin, out var hpVal);
            bool inBase = baseGpio.TryGetValue(pin, out var baseVal);

            if (inHp && !inBase && hpVal != val)
            {
                notes.Add($"GPIO{pin}: keeping OEM value 0x{hpVal:X4} (donor{tag} sets 0x{val:X4}, but OEM wires this pin itself)");
                continue;
            }
            if (!inBase || baseVal != val)
            {
                if (inHp && inBase && hpVal != baseVal)
                {
                    notes.Add($"GPIO{pin}: OEM 0x{hpVal:X4} differs from donor base 0x{baseVal:X4}; keeping OEM value (donor{tag} wants 0x{val:X4})");
                    continue;
                }
                if (inHp && hpVal != val)
                    notes.Add($"GPIO{pin}: 0x{hpVal:X4} -> 0x{val:X4} (donor{tag} adjustment)");
                else if (!inHp)
                {
                    notes.Add($"GPIO{pin}: add 0x{val:X4} (donor{tag} specific)");
                    order.Add(pin);
                }
                outGpio[pin] = val;
            }
        }
        foreach (var pin in baseGpio.Keys)
            if (!family.ContainsKey(pin) && hp.ContainsKey(pin))
                notes.Add($"GPIO{pin}: donor drops this pin for{tag}; keeping OEM value 0x{hp[pin]:X4}");

        return (outGpio, order, notes);
    }
}
