// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Solstice <solsticeofthewinter@gmail.com>
// SPDX-FileCopyrightText: 2025 SolsticeOfTheWinter <solsticeofthewinter@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text;
using Content.Server.Antag;
using Content.Server.GameTicking.Rules;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server.Roles;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Roles;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._Omu.Chimera.GameTicking.Rules;

public sealed class ChimeraRuleSystem : GameRuleSystem<ChimeraRuleComponent>
{
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly ObjectivesSystem _objective = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChimeraRuleComponent, AfterAntagEntitySelectedEvent>(OnSelectAntag);
        SubscribeLocalEvent<ChimeraRuleComponent, GetBriefingEvent>(OnGetBrief);
    }

    private void OnSelectAntag(EntityUid uid, ChimeraRuleComponent comp, ref AfterAntagEntitySelectedEvent args)
    {
        MakeLeto(args.EntityUid, comp);
    }

    private bool MakeLeto(EntityUid target, ChimeraRuleComponent rule)
    {

        var briefing = Loc.GetString("leto-role-greeting", ("playerName", Name(target)));
        _antag.SendBriefing(target, briefing, Color.DarkRed, rule.BriefingSound);

        _npcFaction.RemoveFaction(target, rule.NanotrasenFaction);
        _npcFaction.AddFaction(target, rule.ChimeraFaction);

        return true;
    }

    private void OnGetBrief(Entity<ChimeraRuleComponent> role, ref GetBriefingEvent args)
    {
        var ent = args.Mind.Comp.OwnedEntity;

        if (ent is null)
            return;

        args.Append(Loc.GetString("leto-role-greeting"));
    }
}
