// SPDX-License-Identifier: MIT

using Content.Shared._Omu.Roles; // Omu
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server.Access.Components;

[RegisterComponent]
public sealed partial class PresetIdCardComponent : Component
{
    [DataField("job")]
    public ProtoId<JobPrototype>? JobName;

    [DataField("name")]
    public string? IdName;

    // Omu start
    [DataField("alternateTitle")]
    public ProtoId<JobAlternateTitlePrototype>? AlternateTitle;
    // Omu end
}