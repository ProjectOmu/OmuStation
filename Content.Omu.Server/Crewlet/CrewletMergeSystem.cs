// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Omu.Shared.Crewlet;
using Content.Server.Anomaly;
using Content.Server.Anomaly.Components;
using Content.Server.Anomaly.Effects;
using Content.Server.Power.EntitySystems;
using Content.Server.Speech.Components;
using Content.Shared.Anomaly;
using Content.Shared.Anomaly.Components;
using Content.Shared.Anomaly.Effects.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NameModifier.EntitySystems;
using Content.Shared.Research.Components;
using Content.Shared.Speech;
using Content.Shared.Speech.Components;
using Robust.Server.GameStates;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Omu.Server.Crewlet;

public sealed class CrewletMergeSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AnomalySystem _anomaly = default!;
    [Dependency] private readonly CrewletSpawnWatchSystem _spawnWatch = default!;
    [Dependency] private readonly CrewletSystem _crewlet = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly NameModifierSystem _nameModifier = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    [Dependency] private readonly PvsOverrideSystem _pvsOverride = default!;
    [Dependency] private readonly SharedPointLightSystem _pointLight = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private const float SpawnRangeMargin = 1f;

    private static readonly LocId DisguiseName = "crewlet-disguise-name";

    private readonly List<EntityUid> _spawned = new();
    private readonly HashSet<EntityUid> _released = new();
    private readonly List<EntityUid> _releasing = new();
    private readonly List<(AnomalySpawnSettings Settings, EntProtoId Mob)> _mobSpawns = new();

    private EntityUid? _huskMap;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CrewletMergedAnomalyComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CrewletMergedAnomalyComponent, AnomalyPulseEvent>(Stamp,
            after: [typeof(EntityAnomalySystem)]);
        SubscribeLocalEvent<CrewletMergedAnomalyComponent, AnomalySupercriticalEvent>(Stamp,
            after: [typeof(EntityAnomalySystem)]);
        SubscribeLocalEvent<CrewletMergedAnomalyComponent, AnomalyShutdownEvent>(OnAnomalyShutdown,
            after: [typeof(EntityAnomalySystem)]);
        SubscribeLocalEvent<CrewletMergedAnomalyComponent, AnomalyStabilityChangedEvent>(Stamp,
            after: [typeof(EntityAnomalySystem)]);
        SubscribeLocalEvent<CrewletMergedAnomalyComponent, AnomalySeverityChangedEvent>(Stamp,
            after: [typeof(EntityAnomalySystem)]);
        SubscribeLocalEvent<CrewletDisguiseComponent, RefreshNameModifiersEvent>(OnRefreshName);
        SubscribeLocalEvent<CrewletDisguiseComponent, MobStateChangedEvent>(OnDisguisedStateChanged);
        SubscribeLocalEvent<CrewletDisguiseComponent, ComponentShutdown>(OnDisguiseShutdown);
        SubscribeLocalEvent<ResearchClientComponent, ResearchServerGetPointsPerSecondEvent>(OnGetPointsPerSecond);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    public bool CanMerge(EntityUid anomaly)
    {
        return !TerminatingOrDeleted(anomaly)
            && !HasComp<CrewletMergedAnomalyComponent>(anomaly)
            && HasComp<AnomalyComponent>(anomaly)
            && !HasComp<MobStateComponent>(anomaly)
            && TryComp<EntitySpawnAnomalyComponent>(anomaly, out var spawner)
            && PickMob(spawner) != null;
    }

    public bool TryMerge(Entity<CrewletComponent> crewlet, EntityUid anomaly)
    {
        if (!CanMerge(anomaly) || !_mobState.IsAlive(crewlet) || HasComp<CrewletHuskComponent>(crewlet))
            return false;

        _crewlet.SetOwner(crewlet, null);
        _crewlet.Wail(crewlet, Transform(anomaly).Coordinates);

        var husk = EnsureComp<CrewletHuskComponent>(crewlet);
        husk.DropAt = _transform.GetMapCoordinates(anomaly);
        husk.Users.Add(anomaly);

        _transform.SetCoordinates(crewlet, new EntityCoordinates(EnsureHuskMap(), Vector2.Zero));
        _pvsOverride.AddGlobalOverride(crewlet);

        var merged = EnsureComp<CrewletMergedAnomalyComponent>(anomaly);
        merged.NextSpawn = _timing.CurTime + merged.SpawnInterval;
        var disguise = EnsureComp<CrewletDisguiseComponent>(anomaly);
        disguise.Husk = crewlet;
        Dirty(anomaly, disguise);

        var light = _pointLight.EnsureLight(anomaly);
        _pointLight.SetColor(anomaly, merged.AuraColor, light);
        _pointLight.SetRadius(anomaly, merged.AuraRadius, light);
        _pointLight.SetEnergy(anomaly, merged.AuraEnergy, light);

        _anomaly.DoAnomalyPulse(anomaly);
        for (var i = 0; i < merged.SpawnCap; i++)
        {
            SpawnMob((anomaly, merged, disguise));
        }

        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        using var query = EntityQueryEnumerator<CrewletMergedAnomalyComponent, CrewletDisguiseComponent>();
        while (query.MoveNext(out var uid, out var merged, out var disguise))
        {
            if (merged.NextSpawn is not { } next || now < next)
                continue;

            merged.NextSpawn = next + merged.SpawnInterval;
            SpawnMob((uid, merged, disguise));
        }

        if (_released.Count == 0)
            return;

        _releasing.AddRange(_released);
        _released.Clear();
        foreach (var uid in _releasing)
        {
            Release(uid);
        }

        _releasing.Clear();
    }

    private void OnExamined(Entity<CrewletMergedAnomalyComponent> ent, ref ExaminedEvent args)
    {
        if (TryComp<CrewletDisguiseComponent>(ent, out var disguise)
            && TryComp<CrewletComponent>(disguise.Husk, out var crewlet))
            args.PushMarkup(Loc.GetString(ent.Comp.ExamineText, ("name", crewlet.TemplateName)));
    }

    private void OnAnomalyShutdown(Entity<CrewletMergedAnomalyComponent> ent, ref AnomalyShutdownEvent args)
    {
        ent.Comp.NextSpawn = null;
        Stamp(ent, ref args);
    }

    private void SpawnMob(Entity<CrewletMergedAnomalyComponent, CrewletDisguiseComponent> anomaly)
    {
        if (anomaly.Comp2.Husk is not { } husk
            || !TryComp<CrewletHuskComponent>(husk, out var huskComp)
            || huskComp.Users.Count > anomaly.Comp1.SpawnCap
            || !TryComp<EntitySpawnAnomalyComponent>(anomaly, out var spawner)
            || !TryComp<MapGridComponent>(Transform(anomaly).GridUid, out var grid)
            || PickMob(spawner) is not { } pick
            || _anomaly.GetSpawningPoints(anomaly, 1f, 1f, pick.Settings) is not { Count: > 0 } tiles)
            return;

        var mob = Spawn(pick.Mob, _map.ToCenterCoordinates(_random.Pick(tiles), grid));
        Disguise(mob, anomaly.Comp1, anomaly.Comp2, husk, huskComp);
    }

    private (AnomalySpawnSettings Settings, EntProtoId Mob)? PickMob(EntitySpawnAnomalyComponent spawner)
    {
        _mobSpawns.Clear();
        foreach (var entry in spawner.Entries)
        {
            foreach (var spawn in entry.Spawns)
            {
                if (_proto.Index(spawn).TryGetComponent<MobStateComponent>(out _, Factory))
                    _mobSpawns.Add((entry.Settings, spawn));
            }
        }

        if (_mobSpawns.Count == 0)
            return null;

        var pick = _random.Pick(_mobSpawns);
        _mobSpawns.Clear();
        return pick;
    }

    private void Disguise(EntityUid uid,
        CrewletMergedAnomalyComponent merged,
        CrewletDisguiseComponent source,
        EntityUid husk,
        CrewletHuskComponent huskComp)
    {
        var disguise = EnsureComp<CrewletDisguiseComponent>(uid);
        disguise.Husk = husk;
        disguise.Color = source.Color;
        Dirty(uid, disguise);
        huskComp.Users.Add(uid);
        _nameModifier.RefreshNameModifiers(uid);

        EntityManager.AddComponents(uid, _proto.Index(merged.MobKit));
        if (_proto.Resolve(merged.MobProtection, out var protection))
            EnsureComp<DamageProtectionBuffComponent>(uid).Modifiers.TryAdd(merged.MobProtection, protection);

        RemComp<ReplacementAccentComponent>(uid);
        if (TryComp<CrewletAccentComponent>(husk, out var accent))
            EnsureComp<CrewletAccentComponent>(uid).Name = accent.Name;

        if (TryComp<VocalComponent>(husk, out var vocal))
        {
            var mobVocal = EnsureComp<VocalComponent>(uid);
            mobVocal.EmoteSounds = vocal.EmoteSounds;
            mobVocal.ScreamId = vocal.ScreamId;
            Dirty(uid, mobVocal);
        }

        if (!TryComp<SpeechComponent>(husk, out var voice))
            return;

        var speech = EnsureComp<SpeechComponent>(uid);
        speech.SpeechSounds = voice.SpeechSounds;
        speech.SpeechVerb = voice.SpeechVerb;
    }

    private void Stamp<T>(Entity<CrewletMergedAnomalyComponent> ent, ref T args)
    {
        _spawned.Clear();
        _spawnWatch.TakeSpawned(_spawned);

        if (_spawned.Count == 0
            || !TryComp<CrewletDisguiseComponent>(ent, out var source)
            || source.Husk is not { } husk
            || !TryComp<CrewletHuskComponent>(husk, out var huskComp)
            || !TryComp<EntitySpawnAnomalyComponent>(ent, out var spawner))
        {
            _spawned.Clear();
            return;
        }

        var origin = _transform.GetMapCoordinates(ent);
        foreach (var uid in _spawned)
        {
            if (TerminatingOrDeleted(uid)
                || !HasComp<MobStateComponent>(uid)
                || HasComp<CrewletDisguiseComponent>(uid)
                || _container.IsEntityInContainer(uid)
                || !SpawnedBy(spawner, uid, origin))
                continue;

            Disguise(uid, ent.Comp, source, husk, huskComp);
        }

        _spawned.Clear();
    }

    private bool SpawnedBy(EntitySpawnAnomalyComponent spawner, EntityUid uid, MapCoordinates origin)
    {
        if (MetaData(uid).EntityPrototype?.ID is not { } id)
            return false;

        var position = _transform.GetMapCoordinates(uid);
        if (position.MapId != origin.MapId)
            return false;

        var distance = (position.Position - origin.Position).Length();
        foreach (var entry in spawner.Entries)
        {
            if (distance > entry.Settings.MaxRange + SpawnRangeMargin)
                continue;

            foreach (var spawn in entry.Spawns)
            {
                if (spawn.Id == id)
                    return true;
            }
        }

        return false;
    }

    private void OnRefreshName(Entity<CrewletDisguiseComponent> ent, ref RefreshNameModifiersEvent args)
    {
        if (ent.Comp.Husk is { } husk && HasComp<MobStateComponent>(ent) && !TerminatingOrDeleted(husk))
            args.AddModifier(DisguiseName, int.MaxValue, ("name", Name(husk)));
    }

    private void OnDisguisedStateChanged(Entity<CrewletDisguiseComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        RemComp(ent, ent.Comp);
        _nameModifier.RefreshNameModifiers(ent.Owner);
    }

    private void OnDisguiseShutdown(Entity<CrewletDisguiseComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Husk is not { } husk || !TryComp<CrewletHuskComponent>(husk, out var huskComp))
            return;

        if (HasComp<CrewletMergedAnomalyComponent>(ent))
        {
            var position = _transform.GetMapCoordinates(ent);
            if (position.MapId != MapId.Nullspace)
                huskComp.DropAt = position;
        }

        if (huskComp.Users.Remove(ent) && huskComp.Users.Count == 0)
            _released.Add(husk);
    }

    private void OnGetPointsPerSecond(Entity<ResearchClientComponent> ent,
        ref ResearchServerGetPointsPerSecondEvent args)
    {
        if (TryComp<AnomalyVesselComponent>(ent, out var vessel)
            && vessel.Anomaly is { } anomaly
            && TryComp<CrewletMergedAnomalyComponent>(anomaly, out var merged)
            && _power.IsPowered(ent))
            args.Points += (int) (_anomaly.GetAnomalyPointValue(anomaly) * vessel.PointMultiplier * merged.PointBonus);
    }

    private void Release(EntityUid husk)
    {
        if (TerminatingOrDeleted(husk)
            || !TryComp<CrewletHuskComponent>(husk, out var huskComp)
            || huskComp.Users.Count > 0)
            return;

        var dropAt = huskComp.DropAt;
        if (_map.TryGetMap(dropAt.MapId, out var map) && !TerminatingOrDeleted(map.Value))
        {
            _pvsOverride.RemoveGlobalOverride(husk);
            _transform.SetMapCoordinates(husk, dropAt);
            _crewlet.DropEverything(husk);
        }

        QueueDel(husk);
    }

    private EntityUid EnsureHuskMap()
    {
        if (_huskMap is { } existing && Exists(existing))
            return existing;

        var map = _map.CreateMap();
        _metaData.SetEntityName(map, Loc.GetString("crewlet-husk-map-name"));
        _map.SetPaused(map, true);
        _huskMap = map;
        return map;
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _released.Clear();
        if (_huskMap is { } map && Exists(map))
            Del(map);

        _huskMap = null;
    }
}
