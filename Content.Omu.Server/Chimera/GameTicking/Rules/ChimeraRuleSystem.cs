// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Solstice <solsticeofthewinter@gmail.com>
// SPDX-FileCopyrightText: 2025 SolsticeOfTheWinter <solsticeofthewinter@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later


using Content.Server.Antag;
using Content.Server.GameTicking.Rules;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server.Roles;
using Content.Shared.NPC.Systems;
using Robust.Shared.Audio;
using Content.Server.Chat.Systems;
using Content.Server.RoundEnd;
using Content.Server.Station.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Player;
using Content.Server.GameTicking;
using Robust.Shared.Audio.Systems;
using Content.Server.Nuke;
using Content.Server.AlertLevel;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Timing;

namespace Content.Server._Omu.Chimera.GameTicking.Rules;

public sealed class ChimeraRuleSystem : GameRuleSystem<ChimeraRuleComponent>
{
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly RoundEndSystem _roundEnd = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly NukeCodePaperSystem _nukeCode = default!;
    [Dependency] private readonly AlertLevelSystem _alertLevelSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChimeraRuleComponent, AfterAntagEntitySelectedEvent>(OnSelectAntag);
        SubscribeLocalEvent<ChimeraRuleComponent, GetBriefingEvent>(OnGetBrief);
    }
    protected override void ActiveTick(EntityUid uid, ChimeraRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);
        if (!component.NextRoundEndCheck.HasValue || component.NextRoundEndCheck > _timing.CurTime)
            return;
        CheckRoundEnd(component);
        component.NextRoundEndCheck = _timing.CurTime + component.EndCheckDelay;
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

    /// <summary>
    /// Get the fraction of players that are infected, between 0 and 1, exact same as in zombie system.
    /// </summary>
    /// <param name="includeOffStation">Include healthy players that are not on the station grid</param>
    /// <param name="includeDead">Should dead zombies be included in the count</param>
    /// <returns></returns>
    private float GetInfectedFraction(bool includeOffStation = false, bool includeDead = true)
    {
        var players = GetHealthyHumans(includeOffStation);
        var ChimeraCount = 0;
        var query = EntityQueryEnumerator<HumanoidAppearanceComponent, ChimeraComponent, MobStateComponent>();
        while (query.MoveNext(out _, out _, out _, out var mob))
        {
            if (!includeDead && mob.CurrentState == MobState.Dead)
                continue;
            ChimeraCount++;
        }

        return ChimeraCount / (float) (players.Count + ChimeraCount);
    }

    private List<EntityUid> GetHealthyHumans(bool includeOffStation = false)
    {
        var healthy = new List<EntityUid>();

        var stationGrids = new HashSet<EntityUid>();
        if (!includeOffStation)
        {
            foreach (var station in _gameTicker.GetSpawnableStations())
            {
                if (_station.GetLargestGrid(station) is { } grid)
                    stationGrids.Add(grid);
            }
        }

        var players = AllEntityQuery<HumanoidAppearanceComponent, ActorComponent, MobStateComponent, TransformComponent>();
        var Chimera = GetEntityQuery<ChimeraComponent>();
        while (players.MoveNext(out var uid, out _, out _, out var mob, out var xform))
        {

            if (!_mobState.IsAlive(uid, mob)
                || Chimera.HasComponent(uid)
                || !includeOffStation && !stationGrids.Contains(xform.GridUid ?? EntityUid.Invalid))
                continue;

            healthy.Add(uid);
        }
        return healthy;
    }

    private void CheckRoundEnd(ChimeraRuleComponent ChimeraRuleComponent)
    {
        var healthy = GetHealthyHumans();
        if (GetInfectedFraction(false) > ChimeraRuleComponent.DeltaCallPercentage / 5f && !ChimeraRuleComponent.StartAnnounced)
        {
            ChimeraRuleComponent.StartAnnounced = true;

            foreach (var station in _station.GetStations())
            {
                _chat.DispatchStationAnnouncement(station,
                    Loc.GetString("zombie-start-announcement"),
                    colorOverride: Color.Pink);
            }

            var audio = new SoundPathSpecifier("/Audio/Announcements/outbreak7.ogg");

            _audio.PlayGlobal(audio, Filter.Broadcast(), true, AudioParams.Default.WithVolume(-2f));
        }

        if (GetInfectedFraction(false) > ChimeraRuleComponent.DeltaCallPercentage && !_roundEnd.IsRoundEndRequested())
        {
            foreach (var station in _station.GetStations())
            {
                _nukeCode.SendNukeCodes(station);       // Send nuke codes
                _alertLevelSystem.SetLevel(station, "delta", true, true, true, true);   // neenaw delta!
                _chat.DispatchStationAnnouncement(station, Loc.GetString("chimera-critical-announcement"), colorOverride: Color.DarkRed);       // run for your lives!!
            }
            _roundEnd.RequestRoundEnd(null, false);
        }

        // we include dead for this count because we don't want to end the round
        // when everyone gets on the shuttle.
        if (GetInfectedFraction() >= 1) // Oops, all chimera
            _roundEnd.EndRound();
    }
}
