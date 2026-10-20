// SPDX-License-Identifier: GPL-2.0-or-later
using Am4ever.Core.Build.Operations;
using Am4ever.Core.Profile;
using Am4ever.Core.Rom;

namespace Am4ever.Core.Build;

/// <summary>Maps an <see cref="OperationSpec.Op"/> name to its operation implementation.</summary>
public static class OperationFactory
{
    public static IBoardOperation Create(OperationSpec spec) => spec.Op switch
    {
        "neutralise_override" => new NeutraliseOverrideOp(spec),
        "byte_patch" => new BytePatchOp(spec),
        "port_gpio" => new PortGpioOp(spec),
        "port_gevent" => new PortGeventOp(spec),
        "empty_tables" => new EmptyTablesOp(spec),
        "dxio_layout" => new DxioLayoutOp(spec),
        "ddi_layout" => new DdiLayoutOp(spec),
        "verb_table" => new VerbTableOp(spec),
        "smbios_identity" => new SmbiosIdentityOp(spec),
        "apcb_check" => new ApcbCheckOp(spec),
        _ => throw new RomFormatException($"unknown operation '{spec.Op}'"),
    };
}
