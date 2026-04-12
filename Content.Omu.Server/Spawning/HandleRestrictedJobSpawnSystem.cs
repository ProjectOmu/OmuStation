using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Server.Spawners.EntitySystems;
using Content.Shared.GameTicking;
using Content.Server.Spawners.Components;
using Robust.Server.Containers;
using Robust.Shared.Containers;
using Robust.Shared.Random;

namespace Content.Omu.Server.Spawning;

// todo this is all boilerplate copypaste code and is bad but i really don't want to touch upstream shit atm.
// probably PR some changes to wizden and some helpers to make use of ContainerSpawnPointSystem.
public sealed class HandleRestrictedJobSpawnSystem : EntitySystem
{
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawning = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawningEvent>(
            OnPlayerSpawning,
            before:
            [
                typeof(ContainerSpawnPointSystem),
                typeof(ArrivalsSystem),
            ]);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawnComplete);
    }

    private void OnPlayerSpawning(PlayerSpawningEvent args)
    {
        if (args.SpawnResult != null || args.Job == null)
            return;

        var isLateJoin = _ticker.RunLevel == GameRunLevel.InRound;

        var query = EntityQueryEnumerator<ForcedCryosleepSpawnerComponent, ContainerManagerComponent, TransformComponent>();
        var possibleContainers = new List<Entity<ForcedCryosleepSpawnerComponent, ContainerManagerComponent, TransformComponent>>();

        while (query.MoveNext(out var uid, out var comp, out var manager, out var xform))
        {
            if (!comp.Forced || comp.Job != args.Job)
                continue;

            if (args.Station != null && _station.GetOwningStation(uid, xform) != args.Station)
                continue;

            var valid = comp.SpawnTypes.Any(type =>
                type == SpawnPointType.LateJoin && isLateJoin ||
                type == SpawnPointType.Job && !isLateJoin);

            if (!valid)
                continue;

            possibleContainers.Add((uid, comp, manager, xform));
        }

        if (possibleContainers.Count == 0)
            return;

        TrySpawnIntoContainers(args, possibleContainers);
    }

    private bool TrySpawnIntoContainers(PlayerSpawningEvent args, List<Entity<ForcedCryosleepSpawnerComponent, ContainerManagerComponent, TransformComponent>> containers)
    {
        var coords = containers[0].Comp3.Coordinates;

        args.SpawnResult = _stationSpawning.SpawnPlayerMob(
            coords,
            args.Job,
            args.HumanoidCharacterProfile,
            args.Station);

        if (args.SpawnResult == null)
            return false;

        _random.Shuffle(containers);

        foreach (var (uid, comp, manager, xform) in containers)
        {
            if (!_container.TryGetContainer(uid, comp.ContainerId, out var container, manager))
                continue;

            if (!_container.Insert(args.SpawnResult.Value, container, containerXform: xform))
                continue;

            return true;
        }

        Del(args.SpawnResult);
        args.SpawnResult = null;
        return false;
    }

    private void OnSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (ev.JobId == null)
            return;
        var query = EntityQueryEnumerator<ForcedCryosleepSpawnerComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Job != ev.JobId)
                continue;
            if (!_container.TryGetContainer(uid, comp.ContainerId, out var container))
                continue;
            if (!container.Contains(ev.Mob))
                continue;

            _chat.DispatchServerMessage(ev.Player, Loc.GetString("latejoin-forced-job-spawn"));
            return;
        }
    }
}
