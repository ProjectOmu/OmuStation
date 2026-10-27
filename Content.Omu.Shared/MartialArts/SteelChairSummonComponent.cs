// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Omu.Shared.MartialArts;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SteelChairSummonComponent : Component
{
    [DataField]
    public EntProtoId Action = "ActionSummonSteelChair";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;

    [DataField]
    public EntProtoId ChairPrototype = "ChairFoldingSteel";

    [DataField, AutoNetworkedField]
    public EntityUid? Chair;

    [DataField]
    public LocId SummonShout = "steel-chair-summon-shout";
}

public sealed partial class SummonSteelChairActionEvent : InstantActionEvent;
