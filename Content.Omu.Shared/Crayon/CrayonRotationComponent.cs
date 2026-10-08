// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Omu.Shared.Decals;
using Robust.Shared.GameStates;

namespace Content.Omu.Shared.Crayon;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(DecalRotationSystem))]
public sealed partial class CrayonRotationComponent : Component
{
    [DataField, AutoNetworkedField]
    public Angle Rotation;
}
