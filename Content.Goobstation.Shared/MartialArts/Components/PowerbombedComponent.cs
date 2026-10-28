// SPDX-License-Identifier: AGPL-3.0-or-later

// whole file is Omu

using Robust.Shared.Prototypes;

namespace Content.Goobstation.Shared.MartialArts.Components;

[RegisterComponent]
public sealed partial class PowerbombedComponent : Component
{
    [DataField]
    public TimeSpan SlamTime = TimeSpan.Zero;

    [DataField]
    public List<EntProtoId> BreakableTables = new();
}
