// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Omu.Server.Arcade;

[RegisterComponent, Access(typeof(PolybiusSystem))]
public sealed partial class PolybiusStatusEffectComponent : Component
{
    [DataField(required: true)]
    public ComponentRegistry Components = new();

    public ComponentRegistry Granted = new();
}
