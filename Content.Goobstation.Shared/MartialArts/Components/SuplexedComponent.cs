// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;

namespace Content.Goobstation.Shared.MartialArts.Components;

[RegisterComponent]
public sealed partial class SuplexedComponent : Component
{
    [DataField]
    public EntityUid Thrower;

    [DataField]
    public Vector2 Landing;

    [DataField]
    public float ThrownSpeed;
}
