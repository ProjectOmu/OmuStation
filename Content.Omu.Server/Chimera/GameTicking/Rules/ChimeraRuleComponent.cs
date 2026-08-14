// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Solstice <solsticeofthewinter@gmail.com>
// SPDX-FileCopyrightText: 2025 SolsticeOfTheWinter <solsticeofthewinter@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.NPC.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Omu.Chimera.GameTicking.Rules;

[RegisterComponent, Access(typeof(ChimeraRuleSystem))]
public sealed partial class ChimeraRuleComponent : Component
{
    [DataField]
    public SoundPathSpecifier BriefingSound = new("/Audio/Ambience/Antag/traitor_start.ogg");

    [DataField]
    public ProtoId<NpcFactionPrototype> ChimeraFaction = "Chimera";

    [DataField]
    public ProtoId<NpcFactionPrototype>  NanotrasenFaction = "NanoTrasen";

    [DataField]
    public EntProtoId MindRoleChimera = "MindRoleChimera";

    public bool StartAnnounced = false;

    [DataField]
    public float DeltaCallPercentage = 0.7f;

    /// <summary>
    /// When the round will next check for round end.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan? NextRoundEndCheck;

    /// <summary>
    /// The amount of time between each check for the end of the round.
    /// </summary>
    [DataField]
    public TimeSpan EndCheckDelay = TimeSpan.FromSeconds(30);
}
