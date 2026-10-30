// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Omu.Speech;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Omu.Shared.Speech;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EmotePackComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public ProtoId<EmotePackPrototype> Pack;

    [DataField(required: true), AutoNetworkedField]
    public ProtoId<SpeciesPrototype> Species;

    public readonly Dictionary<Sex, List<ProtoId<EmoteSoundsPrototype>>> Sounds = new();
}
