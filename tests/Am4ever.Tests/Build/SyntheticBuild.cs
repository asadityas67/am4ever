// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Build;
using Am4ever.Tests.Board;

namespace Am4ever.Tests.Build;

/// <summary>Builds minimal <see cref="BuildContext"/> fixtures for operation unit tests.</summary>
public static class SyntheticBuild
{
    /// <summary>A context whose only donor module has a $A11 but no $A10 GPIO table.</summary>
    public static BuildContext ContextWithoutGpioTable()
    {
        var img = SyntheticCpm.WithTables(("$A11", 0u, 0x20));
        return new BuildContext(new() { ["MTS_VMR"] = img }, new());
    }

    /// <summary>A context whose only donor module holds <paramref name="expected"/> at <paramref name="offset"/>.</summary>
    public static BuildContext ContextWithOverrideSite(int offset, byte[] expected)
    {
        var img = new byte[offset + expected.Length + 4];
        expected.CopyTo(img, offset);
        return new BuildContext(new() { ["ZP"] = img }, new());
    }
}
