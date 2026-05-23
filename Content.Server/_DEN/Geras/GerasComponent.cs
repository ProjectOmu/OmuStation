// SPDX-FileCopyrightText: 2024 DEATHB4DEFEAT <77995199+DEATHB4DEFEAT@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 sleepyyapril <flyingkarii@gmail.com>
// SPDX-FileCopyrightText: 2025 sleepyyapril <123355664+sleepyyapril@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later AND MIT

using Content.Shared.Actions;
using Content.Shared.Polymorph;
using Robust.Shared.Prototypes;

namespace Content.Server._DEN.Geras;

/// <summary>
/// This component assigns the entity with a polymorph action.
/// </summary>
[RegisterComponent]
public sealed partial class GerasComponent : Component
{
    [DataField] public ProtoId<PolymorphPrototype> GerasPolymorphId = "SlimeMorphGeras";

    [DataField] public EntProtoId GerasAction = "ActionMorphGeras";

    [DataField] public EntityUid? GerasActionEntity;
}
