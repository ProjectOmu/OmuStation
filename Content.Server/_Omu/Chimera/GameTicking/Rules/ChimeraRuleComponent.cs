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

    [ValidatePrototypeId<NpcFactionPrototype>, DataField]
    public string ChimeraFaction = "Chimera";

    [ValidatePrototypeId<NpcFactionPrototype>, DataField]
    public string NanotrasenFaction = "NanoTrasen";

    [DataField]
    public EntProtoId MindRoleChimera = "MindRoleChimera";
}
