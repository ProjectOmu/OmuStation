// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Shitmed.Medical.Surgery;

/// <summary>
///     Prevents the entity from causing toxin damage to entities it does surgery on.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class SanitizedComponent : Component
{
    [DataField]
    public bool WorksInHands;

    [DataField] public bool RequiresGloves = false; //omu - ipcs don't need a mask to do surgery (but still need gloves)
}
