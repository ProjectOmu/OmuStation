// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Alert;

namespace Content.Shared._StarLight.Dolls.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ShellComponent : Component
{
    [DataField]
    public EntProtoId GenerateShellPieceAction = "ActionGenerateShellPiece";

    [DataField, AutoNetworkedField]
    public EntityUid? GenerateShellPieceActionEntity;

    [DataField]
    public ProtoId<DamageGroupPrototype> DestroyedBy = "Brute";

    [DataField]
    public float Stability = 1f;

    [DataField]
    public float Hardness = 15f;

    [DataField]
    public SoundSpecifier? ShellBreakSound =
        new SoundPathSpecifier("/Audio/Effects/metal_glass_break1.ogg");

    [DataField]
    public ProtoId<AlertPrototype> ShellAlert = "DollShellIntegrity";
}