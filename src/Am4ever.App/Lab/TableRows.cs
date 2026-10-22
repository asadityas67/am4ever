// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Board.Cpm;

namespace Am4ever.App.Lab;

/// <summary>
/// Read-only parsed-table view: lists the CPM tables a module image publishes (tag, platform mask,
/// size) using the Core parser. Per-descriptor DXIO editing (behind the warned toggle) is done in the
/// Board Lab view against the <c>dxio_layout</c> operation, not here.
/// </summary>
public static class TableRows
{
    public static IReadOnlyList<string> For(byte[] moduleImage) =>
        CpmImage.Tables(moduleImage).Select(t => t.PlatformMask != 0
            ? $"{t.Tag}  mask=0x{t.PlatformMask:X}  size=0x{t.Size:X}"
            : $"{t.Tag}  size=0x{t.Size:X}").ToList();
}
