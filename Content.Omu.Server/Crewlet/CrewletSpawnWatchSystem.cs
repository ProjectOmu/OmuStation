// SPDX-License-Identifier: MIT

using Content.Omu.Shared.Crewlet;
using Content.Server.Anomaly.Effects;
using Content.Shared.Anomaly.Components;
using Robust.Shared.Timing;

namespace Content.Omu.Server.Crewlet;

public sealed class CrewletSpawnWatchSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly List<EntityUid> _spawned = new();
    private int _watching;
    private GameTick _tick;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CrewletDisguiseComponent, AnomalyPulseEvent>(Watch,
            before: [typeof(EntityAnomalySystem)]);
        SubscribeLocalEvent<CrewletDisguiseComponent, AnomalySupercriticalEvent>(Watch,
            before: [typeof(EntityAnomalySystem)]);
        SubscribeLocalEvent<CrewletDisguiseComponent, AnomalyShutdownEvent>(Watch,
            before: [typeof(EntityAnomalySystem)]);
        SubscribeLocalEvent<CrewletDisguiseComponent, AnomalyStabilityChangedEvent>(Watch,
            before: [typeof(EntityAnomalySystem)]);
        SubscribeLocalEvent<CrewletDisguiseComponent, AnomalySeverityChangedEvent>(Watch,
            before: [typeof(EntityAnomalySystem)]);

        EntityManager.EntityInitialized += OnEntityInitialized;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        EntityManager.EntityInitialized -= OnEntityInitialized;
    }

    public void TakeSpawned(List<EntityUid> spawned)
    {
        spawned.AddRange(_spawned);
        _spawned.Clear();
        if (_watching > 0)
            _watching--;
    }

    private void Watch<T>(Entity<CrewletDisguiseComponent> ent, ref T args)
    {
        var tick = _timing.CurTick;
        if (tick != _tick)
        {
            _tick = tick;
            _watching = 0;
            _spawned.Clear();
        }

        _watching++;
    }

    private void OnEntityInitialized(Entity<MetaDataComponent> ent)
    {
        if (_watching > 0 && _timing.CurTick == _tick)
            _spawned.Add(ent.Owner);
    }
}
