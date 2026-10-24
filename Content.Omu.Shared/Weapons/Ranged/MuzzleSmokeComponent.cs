// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Omu.Shared.Weapons.Ranged;

[RegisterComponent]
public sealed partial class MuzzleSmokeComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Effect = string.Empty;
}
