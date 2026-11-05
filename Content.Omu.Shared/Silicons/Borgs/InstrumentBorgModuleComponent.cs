// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Omu.Shared.Silicons.Borgs;

[RegisterComponent]
public sealed partial class InstrumentBorgModuleComponent : Component
{
    [DataField(required: true)]
    public ComponentRegistry Components = new();

    [DataField]
    public EntProtoId Action = "ActionBorgPlayMidi";

    [DataField]
    public EntityUid? ActionEntity;
}
