// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Solstice <solsticeofthewinter@gmail.com>
// SPDX-FileCopyrightText: 2025 SolsticeOfTheWinter <solsticeofthewinter@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.NPC.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._Omu.Chimera.GameTicking.Rules;

[RegisterComponent, Access(typeof(ChimeraRuleSystem))]
public sealed partial class ChimeraRuleComponent : Component
{
    [DataField]
    public SoundPathSpecifier BriefingSound = new("/Audio/Ambience/Antag/traitor_start.ogg");

    [DataField]
    public readonly ProtoId<NpcFactionPrototype> ChimeraFaction = "Chimera";

    [DataField]
    public readonly ProtoId<NpcFactionPrototype>  NanotrasenFaction = "NanoTrasen";

    [DataField]
    public EntProtoId MindRoleChimera = "MindRoleChimera";

    public bool StartAnnounced = false;

    [DataField]
    public float DeltaCallPercentage = 0.7f;
}
