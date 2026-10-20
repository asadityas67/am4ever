// SPDX-License-Identifier: GPL-2.0-or-later
using System.Text;
using Am4ever.Core.Board.Smbios;
using Am4ever.Core.Rom;

namespace Am4ever.Tests.Board;

public class SmbiosTableTests
{
    [Fact] public void ApplyIdentity_RewritesManufacturerString()
    {
        var (img, start, end) = SyntheticSmbios.OneType1("ASRock", "AB350M");
        SmbiosTable.ApplyIdentity(img, start, end,
            new DmiIdentity { SystemManufacturer = "HP", SystemProduct = "HP 285 G3 MT Business PC" }, out _);
        Assert.Equal(("HP", "HP 285 G3 MT Business PC"), SyntheticSmbios.ReadType1(img, start));
    }

    [Fact] public void ApplyIdentity_SetsType1Uuid()
    {
        var (img, start, end) = SyntheticSmbios.OneType1("x", "y");
        var g = Guid.NewGuid();
        SmbiosTable.ApplyIdentity(img, start, end, new DmiIdentity { SystemUuid = g.ToString() }, out _);
        Assert.Equal(g.ToByteArray(), SyntheticSmbios.ReadType1Uuid(img, start));   // SMBIOS 2.6+ byte order
    }

    [Fact] public void Smbios_TooBig_LeavesImageUntouched()
    {
        var (img, start, end) = SyntheticSmbios.OneType1("x", "y");
        var before = (byte[])img.Clone();
        var huge = new string('Z', (end - start) + 100);
        Assert.Throws<RomEditException>(() =>
            SmbiosTable.ApplyIdentity(img, start, end, new DmiIdentity { SystemSerial = huge }, out _));
        Assert.Equal(before, img);
    }

    [Fact] public void FromJson_ReadsSnakeCaseFields()
    {
        var dmi = DmiIdentity.FromJson(Encoding.UTF8.GetBytes(
            "{\"system_manufacturer\":\"HP\",\"board_product\":\"8434\"}"));
        Assert.Equal("HP", dmi.SystemManufacturer);
        Assert.Equal("8434", dmi.BoardProduct);
    }
}
