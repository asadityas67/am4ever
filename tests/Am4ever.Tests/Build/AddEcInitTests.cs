// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Board.Ec;
using Am4ever.Core.Build;
using Am4ever.Core.Profile;
using Am4ever.Core.Rom;
using Am4ever.Core.Rom.Fv;

namespace Am4ever.Tests.Build;

public class AddEcInitTests
{
    [Fact] public void AddEcInit_InsertsDriver()
    {
        var (dxe, innerFv) = SyntheticDxe.WithInnerVolume(freeSpace: 0x1000);
        var ctx = new BuildContext(new(), new()) { DxeVolume = dxe, DxeInnerFvOffset = innerFv };
        var op = OperationFactory.Create(new OperationSpec("add_ec_init"));
        op.Check(ctx);
        op.Apply(ctx);
        Assert.Equal(EcFanDriver.FileGuid,
            new FirmwareVolume(ctx.DxeVolume!, ctx.DxeInnerFvOffset).FindByGuid(EcFanDriver.FileGuid).Guid);
    }

    [Fact] public void AddEcInit_AlreadyPresent_Refuses()
    {
        var (dxe, innerFv) = SyntheticDxe.WithInnerVolume(freeSpace: 0x1000);
        var ctx = new BuildContext(new(), new()) { DxeVolume = dxe, DxeInnerFvOffset = innerFv };
        OperationFactory.Create(new OperationSpec("add_ec_init")).Apply(ctx);     // first insert
        Assert.Throws<RomEditException>(() => OperationFactory.Create(new OperationSpec("add_ec_init")).Check(ctx));
    }
}
