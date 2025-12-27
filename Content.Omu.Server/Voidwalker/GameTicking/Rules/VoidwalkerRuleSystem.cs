using Content.Omu.Server.Voidwalker.Roles;
using Content.Server.Antag;
using Content.Server.GameTicking.Rules;
using Content.Server.Mind;
using Content.Server.Roles;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Localizations;
using Robust.Server.GameObjects;

namespace Content.Omu.Server.Voidwalker.GameTicking.Rules;

public sealed class VoidwalkerRuleSystem : GameRuleSystem<VoidwalkerRuleComponent>
{
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly RoleSystem _roleSystem = default!;
    [Dependency] private readonly MindSystem _mind = default!;

    /// <inheritdoc />
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VoidwalkerRuleComponent, AfterAntagEntitySelectedEvent>(OnSelectAntag);
        SubscribeLocalEvent<VoidwalkerRoleComponent, GetBriefingEvent>(UpdateBriefing);
    }

    private void UpdateBriefing(Entity<VoidwalkerRoleComponent> _, ref GetBriefingEvent args)
    {
        var ent = args.Mind.Comp.OwnedEntity;

        if (ent is not { } entity)
            return;

        args.Append(MakeBriefing(entity));
    }

    private void OnSelectAntag(Entity<VoidwalkerRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        if (!_mind.TryGetMind(args.EntityUid, out var mindId, out _)
            || !_roleSystem.MindHasRole<VoidwalkerRoleComponent>(mindId))
            return;

        _antag.SendBriefing(args.EntityUid, MakeBriefing(args.EntityUid), null, null);
    }

    private string MakeBriefing(EntityUid voidwalker)
    {
        var direction = string.Empty;

        var voidwalkerXform = Transform(voidwalker);

        var station = _station.GetStationInMap(voidwalkerXform.MapID);
        EntityUid? stationGrid = null;
        if (TryComp<StationDataComponent>(station, out var stationData))
            stationGrid = _station.GetLargestGrid(stationData);

        if (stationGrid is not null)
        {
            var stationPosition = _transform.GetWorldPosition((EntityUid)stationGrid);
            var voidwalkerPosition = _transform.GetWorldPosition(voidwalker);

            var vectorToStation = stationPosition - voidwalkerPosition;
            direction = ContentLocalizationManager.FormatDirection(vectorToStation.GetDir());
        }

        var briefing = Loc.GetString("voidwalker-role-briefing", ("direction", direction));

        return briefing;
    }
}
