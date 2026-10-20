// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Board.Cpm;

namespace Am4ever.Tests.Board;

public class DxioTableTests
{
    [Fact] public void Set_LanesAndClkReq_UpdatesDescriptor()
    {
        var (img, t) = SyntheticCpm.OneDxioTable(descriptors: 6, descSize: 0x1A4);
        var dx = new DxioTable(img, t, 0x1A4);
        dx.Set(2, lanes: (0, 0), clkReq: 1);
        Assert.Contains("lanes 0-0", dx.Describe(2));
        Assert.Contains("clkreq=1", dx.Describe(2));
    }

    [Fact] public void Set_Template_CopiesThenOverrides()
    {
        var (img, t) = SyntheticCpm.OneDxioTable(descriptors: 6, descSize: 0x1A4);
        var dx = new DxioTable(img, t, 0x1A4);
        dx.Set(2, lanes: (0, 0), clkReq: 1);
        dx.Set(4, template: 2, engine: 1, lanes: (1, 1), device: 1, function: 2, clkReq: 2);
        Assert.Contains("present=1", dx.Describe(4));         // device set turns present on
        Assert.Contains("lanes 1-1", dx.Describe(4));
    }

    [Fact] public void DdiCopy_PadsToDestinationLength()
    {
        var (dstImg, dst) = SyntheticCpm.OneDxioTable(descriptors: 16, descSize: 0x1A4, ddiBytes: 0x60);
        var (srcImg, src) = SyntheticCpm.OneDxioTable(descriptors: 16, descSize: 0x94, ddiBytes: 0x40);
        DdiBlock.Copy(dstImg, dst, srcImg, src, 0x1A4, 0x94);  // must not throw; pads 0x40 -> 0x60
    }
}
