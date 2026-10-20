// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Build;
using Am4ever.Core.Profile;
using Am4ever.Core.Rom;

namespace Am4ever.Tests.Build;

public class OperationTests
{
    [Fact] public void Factory_UnknownOp_Throws() =>
        Assert.Throws<RomFormatException>(() => OperationFactory.Create(new OperationSpec("no_such_op")));

    [Fact] public void Build_MissingTable_StopsWithMessage()
    {
        var ctx = SyntheticBuild.ContextWithoutGpioTable();
        var op = OperationFactory.Create(new OperationSpec("port_gpio") { Family = "MTS_VMR" });
        var ex = Assert.Throws<RomFormatException>(() => op.Check(ctx));
        Assert.Contains("$A10", ex.Message);
    }

    [Fact] public void NeutraliseOp_AppliesAndPostChecksPass()
    {
        var ctx = SyntheticBuild.ContextWithOverrideSite(0x10, new byte[] { 0x81, 0xEC, 0xC4, 0, 0, 0 });
        var op = OperationFactory.Create(new OperationSpec("neutralise_override")
        {
            Offset = 0x10,
            Expected = new byte[] { 0x81, 0xEC, 0xC4, 0, 0, 0 },
        });
        op.Check(ctx);
        var lines = op.Apply(ctx);
        Assert.NotEmpty(lines);
        Assert.True(op.Verify(ctx, out _));
    }
}
