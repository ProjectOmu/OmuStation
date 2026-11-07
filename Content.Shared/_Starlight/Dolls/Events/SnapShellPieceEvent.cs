// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;
using Content.Shared.Damage;

namespace Content.Shared._StarLight.Dolls.Events;

public sealed partial class SnapShellPieceEvent : InstantActionEvent
{
    [DataField]
    public DamageSpecifier? SelfDamage;

    [DataField]
    public bool RequiresFreeHand = true;
}