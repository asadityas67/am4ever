// SPDX-License-Identifier: GPL-2.0-or-later
using System.Buffers.Binary;
using Am4ever.Core.Rom;

namespace Am4ever.Core.Board.Cpm;

/// <summary>
/// The DXIO port topology inside a $A26 table: fixed-size descriptors after a 0x14-byte header
/// (0x10 CPM header + a 4-byte SocketId). ComboAM4v2 descriptors are 0x1A4 bytes, PinnaclePI 0x94.
/// Ports <c>Dxio</c> from the Python reference, including the template-copy behaviour.
/// </summary>
public sealed class DxioTable
{
    /// <summary>Offset of the first descriptor within the table (0x10 header + 4-byte SocketId).</summary>
    public const int DescriptorBase = 0x14;

    readonly byte[] _img;
    readonly int _tableOffset;
    readonly int _descSize;

    public DxioTable(byte[] img, CpmTable t, int descriptorSize)
    {
        _img = img;
        _tableOffset = t.Offset;
        _descSize = descriptorSize;
        Count = (t.Size - DescriptorBase) / descriptorSize;
    }

    public int Count { get; }

    int Off(int i)
    {
        if (i < 0 || i >= Count)
            throw new RomEditException($"DXIO descriptor {i} is out of range (table has {Count} descriptors)");
        return _tableOffset + DescriptorBase + i * _descSize;
    }

    public string Describe(int i)
    {
        int o = Off(i);
        uint port = BinaryPrimitives.ReadUInt32LittleEndian(_img.AsSpan(o + 0xC));
        return $"eng={_img[o + 4]} lanes {_img[o + 6]}-{_img[o + 7]} present={port & 1} " +
               $"dev={(port >> 3) & 0x1f} func={(port >> 8) & 7} clkreq={(port >> 24) & 0xf} type=0x{_img[o + 0x14]:X}";
    }

    public void Set(int i, int? template = null, byte? engine = null, (byte start, byte end)? lanes = null,
                    byte? device = null, byte? function = null, byte? clkReq = null)
    {
        int o = Off(i);
        if (template is not null)
        {
            var flags = _img.AsSpan(o, 4).ToArray();                 // keep this slot's terminator flags
            _img.AsSpan(Off(template.Value), _descSize).CopyTo(_img.AsSpan(o, _descSize));
            flags.CopyTo(_img, o);
        }
        if (engine is not null) _img[o + 4] = engine.Value;
        if (lanes is not null) { _img[o + 6] = lanes.Value.start; _img[o + 7] = lanes.Value.end; }

        uint port = BinaryPrimitives.ReadUInt32LittleEndian(_img.AsSpan(o + 0xC));
        if (device is not null) port = (uint)((port & ~(0x1fu << 3)) | ((uint)device.Value << 3) | 1);
        if (function is not null) port = (uint)((port & ~(7u << 8)) | ((uint)function.Value << 8));
        if (clkReq is not null) port = (uint)((port & ~(0xfu << 24)) | ((uint)clkReq.Value << 24));
        BinaryPrimitives.WriteUInt32LittleEndian(_img.AsSpan(o + 0xC), port);
    }
}
