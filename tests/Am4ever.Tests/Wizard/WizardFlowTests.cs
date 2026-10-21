// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.App.Wizard;

namespace Am4ever.Tests.Wizard;

public class WizardFlowTests
{
    static byte[] Rom16() => new byte[16 * 1024 * 1024];

    [Fact] public void Steps_InOrder() =>
        Assert.Equal(new[] { WizardStep.Dump, WizardStep.Profile, WizardStep.Donor, WizardStep.Identity, WizardStep.Build, WizardStep.Flash },
            Enum.GetValues<WizardStep>());

    [Fact] public void Next_GatedByValidation()
    {
        var w = new WizardFlow();
        Assert.False(w.Next());                 // no dump set yet
        Assert.Equal(WizardStep.Dump, w.Step);
    }

    [Fact] public void Back_PreservesState()
    {
        var w = new WizardFlow();
        w.SetDump(Rom16());                     // not a valid AM4 image, but the path/bytes are retained
        w.Back();                               // already at Dump: no-op, below-floor guarded
        Assert.Equal(WizardStep.Dump, w.Step);
        Assert.NotNull(w.PrimaryDump);
    }
}
