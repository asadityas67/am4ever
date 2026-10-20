// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Rom;
using Am4ever.Core.Rom.Fv;

namespace Am4ever.Tests.Rom;

public class FvEditorTests
{
    [Fact] public void AppendFile_ThenReParseFindsIt()
    {
        var buf = SyntheticFv.WithFreeSpace(0x4000, out _);
        var fv = new FirmwareVolume(buf, 0);
        var g = Guid.NewGuid();
        FvEditor.AppendRawFile(buf, fv, g, new byte[] { 1, 2, 3, 4 });
        Assert.Equal(g, new FirmwareVolume(buf, 0).FindByGuid(g).Guid);
    }

    [Fact] public void AppendFile_NoRoom_Throws()
    {
        var buf = SyntheticFv.WithFreeSpace(0x20, out _);
        var fv = new FirmwareVolume(buf, 0);
        var before = (byte[])buf.Clone();
        Assert.Throws<RomEditException>(() => FvEditor.AppendRawFile(buf, fv, Guid.NewGuid(), new byte[0x1000]));
        Assert.Equal(before, buf);
    }

    [Fact] public void ReplaceFileBody_WrongLength_Throws()
    {
        var buf = SyntheticFv.WithOneFile(out _);
        var file = new FirmwareVolume(buf, 0).Files().First();
        Assert.Throws<RomEditException>(() => FvEditor.ReplaceFileBody(buf, file, new byte[9999]));
    }

    [Fact] public void ReplaceFileBody_SameLength_UpdatesAndKeepsChecksumValid()
    {
        var buf = SyntheticFv.WithOneFile(out _);
        var file = new FirmwareVolume(buf, 0).Files().First();
        int bodyLen = file.End - file.DataOffset;
        FvEditor.ReplaceFileBody(buf, file, Enumerable.Repeat((byte)0x55, bodyLen).ToArray());
        Assert.Equal(0x55, buf[file.DataOffset]);
        Assert.True(SyntheticFv.HeaderChecksumValid(buf, new FirmwareVolume(buf, 0).Files().First()));
    }
}
