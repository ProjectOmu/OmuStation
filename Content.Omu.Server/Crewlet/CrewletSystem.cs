// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Omu.Shared.Crewlet;
using Content.Server._EinsteinEngines.Language;
using Content.Server.Chat;
using Content.Server.Chat.Systems;
using Content.Server.Cloning;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared._Shitmed.Damage;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Chat;
using Content.Shared.Cloning;
using Content.Shared.Cloning.Events;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Jittering;
using Content.Shared.Maps;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.NameModifier.EntitySystems;
using Content.Shared.NPC;
using Content.Shared.Physics;
using Content.Shared.SSDIndicator;
using Content.Shared.Speech.Components;
using Content.Shared.Storage;
using Content.Shared.Tag;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Omu.Server.Crewlet;

public sealed class CrewletSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefinitions = default!;
    [Dependency] private readonly CloningSystem _cloning = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly LanguageSystem _language = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly NameModifierSystem _nameModifier = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly TileSystem _tile = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    private static readonly ProtoId<CloningSettingsPrototype> Settings = "Crewlet";
    private static readonly EntProtoId Kit = "CrewletKit";
    private static readonly EntProtoId FlareEffect = "CrewletFlare";
    private static readonly TimeSpan NpcCheckInterval = TimeSpan.FromMinutes(1);

    private TimeSpan _nextNpcCheck;
    private const string FleshFloor = "FloorFlesh";
    private const int FleshTiles = 4;
    private const float FleshRange = 1.6f;

    private readonly List<TileRef> _fleshTiles = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CrewletComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<CrewletComponent, ComponentShutdown>(OnCrewletShutdown);
        SubscribeLocalEvent<CrewletOwnerComponent, ComponentShutdown>(OnOwnerShutdown);
        SubscribeLocalEvent<CrewletRegenComponent, MapInitEvent>(OnRegenMapInit);
        SubscribeLocalEvent<CrewletHarmScreamComponent, DamageChangedEvent>(OnHarmed,
            after: [typeof(EmoteOnDamageSystem)]);
        SubscribeLocalEvent<CrewletHarmScreamComponent, BeforeEmoteEvent>(OnBeforeEmote);
    }

    public bool CanCreate(EntityUid template)
    {
        if (!HasComp<HumanoidAppearanceComponent>(template)
            || HasComp<CrewletComponent>(template)
            || !_mind.TryGetMind(template, out _, out var mind)
            || mind.UserId == null)
            return false;

        var attempt = new CloningAttemptEvent(_proto.Index(Settings));
        RaiseLocalEvent(template, ref attempt);
        return !attempt.Cancelled;
    }

    public bool TryCreate(EntityUid template,
        EntityUid owner,
        MapCoordinates coords,
        [NotNullWhen(true)] out EntityUid? crewlet)
    {
        crewlet = null;
        if (!CanCreate(template) || !_cloning.TryCloning(template, coords, Settings, out crewlet))
            return false;

        var uid = crewlet.Value;
        EntityManager.AddComponents(uid, _proto.Index(Kit));
        RemComp<MindExaminableComponent>(uid);
        RemComp<PassiveDamageComponent>(uid);
        RemComp<SSDIndicatorComponent>(uid);
        var comp = EnsureComp<CrewletComponent>(uid);
        _tag.AddTags(uid, comp.Tags);

        if (_proto.Resolve(comp.DamageModifierSet, out var modifierSet))
            EnsureComp<DamageProtectionBuffComponent>(uid).Modifiers.TryAdd(comp.DamageModifierSet, modifierSet);

        _physics.ScaleFixtures(uid, comp.FixtureScale);
        _language.AddLanguage(uid, comp.Language);
        _language.SetLanguage(uid, comp.Language);

        var fullName = _nameModifier.GetBaseName(template);
        var shortName = GetShortName(fullName);
        comp.TemplateName = fullName;
        comp.CallName = Loc.GetString("crewlet-name", ("name", shortName));
        EnsureComp<CrewletAccentComponent>(uid).Name = shortName;
        _metaData.SetEntityName(uid, comp.CallName);
        _metaData.SetEntityDescription(uid, Loc.GetString("crewlet-description", ("name", fullName)));

        SetOwner((uid, comp), owner);
        return true;
    }

    public void SetOwner(Entity<CrewletComponent> crewlet, EntityUid? owner)
    {
        if (crewlet.Comp.CrewletOwner is { } previous)
            Disown(previous, crewlet);

        crewlet.Comp.CrewletOwner = owner;

        if (owner is { } target)
            EnsureComp<CrewletOwnerComponent>(target).Crewlets.Add(crewlet);
    }

    public void Flare(EntityUid uid)
    {
        var xform = Transform(uid);
        SpawnAtPosition(FlareEffect, xform.Coordinates);

        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var center = _transform.GetWorldPosition(xform);
        _fleshTiles.Clear();
        foreach (var tile in _map.GetTilesIntersecting(gridUid, grid, new Circle(center, FleshRange)))
        {
            if (Vector2.Distance(_map.GridTileToWorldPos(gridUid, grid, tile.GridIndices), center) <= FleshRange
                && !_turf.IsSpace(tile)
                && !_turf.IsTileBlocked(tile, CollisionGroup.Impassable))
                _fleshTiles.Add(tile);
        }

        _random.Shuffle(_fleshTiles);
        var flesh = (ContentTileDefinition) _tileDefinitions[FleshFloor];
        for (var i = 0; i < Math.Min(FleshTiles, _fleshTiles.Count); i++)
        {
            _tile.ReplaceTile(_fleshTiles[i], flesh);
        }

        _fleshTiles.Clear();
    }

    public void Wail(Entity<CrewletComponent> crewlet, EntityCoordinates coords)
    {
        if (!TryComp<VocalComponent>(crewlet, out var vocal)
            || vocal.EmoteSounds is not { } soundsId
            || !_proto.Resolve(soundsId, out var sounds))
            return;

        if (!sounds.Sounds.TryGetValue(vocal.ScreamId, out var scream))
            scream = sounds.FallbackSound;

        if (scream == null)
            return;

        var audioParams = (sounds.GeneralParams ?? scream.Params)
            .WithPitchScale(crewlet.Comp.WailPitch)
            .AddVolume(crewlet.Comp.WailVolume);
        if (_audio.PlayPvs(scream, coords, audioParams) is not { } wail)
            return;

        if (TryComp<TimedDespawnComponent>(wail.Entity, out var despawn))
            despawn.Lifetime /= wail.Component.Params.Pitch;

        if (_audio.Auxiliaries.TryGetValue(crewlet.Comp.WailReverb, out var reverb))
            _audio.SetAuxiliary(wail.Entity, wail.Component, reverb);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        using var regens = EntityQueryEnumerator<CrewletRegenComponent, DamageableComponent, MobStateComponent>();
        while (regens.MoveNext(out var uid, out var regen, out var damageable, out var mobState))
        {
            if (now < regen.NextRegen)
                continue;

            regen.NextRegen = now + regen.Interval;
            if (damageable.TotalDamage <= 0 || !regen.AllowedStates.Contains(mobState.CurrentState))
                continue;

            _damageable.TryChangeDamage(uid,
                regen.Damage,
                true,
                false,
                damageable,
                targetPart: TargetBodyPart.All,
                splitDamage: SplitDamageBehavior.SplitEnsureAll);
        }

        using var query = EntityQueryEnumerator<CrewletMeltingComponent, CrewletComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var melting, out var crewlet, out var xform))
        {
            if (now < melting.MeltAt)
                continue;

            Wail((uid, crewlet), xform.Coordinates);
            DropEverything(uid);
            SpawnAtPosition(crewlet.MeltEffect, xform.Coordinates);
            QueueDel(uid);
        }

        if (now < _nextNpcCheck)
            return;

        _nextNpcCheck = now + NpcCheckInterval;
        RecoverNpcs();
    }

    private void RecoverNpcs()
    {
        using var query = EntityQueryEnumerator<CrewletComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out _, out var mobState))
        {
            if (mobState.CurrentState != MobState.Alive
                || HasComp<CrewletHuskComponent>(uid)
                || HasComp<CrewletMeltingComponent>(uid)
                || HasComp<ActorComponent>(uid)
                || (TryComp<MindContainerComponent>(uid, out var minds) && minds.HasMind)
                || (HasComp<HTNComponent>(uid)
                    && HasComp<CrewletBrainComponent>(uid)
                    && HasComp<ActiveNPCComponent>(uid)))
                continue;

            Log.Warning($"Recovering the NPC of {ToPrettyString(uid)}");
            EntityManager.AddComponents(uid, _proto.Index(Kit), removeExisting: false);
            _npc.WakeNPC(uid);
        }
    }

    private void OnMobStateChanged(Entity<CrewletComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
        {
            RemComp<CrewletMeltingComponent>(ent);
            return;
        }

        if (HasComp<CrewletMeltingComponent>(ent))
            return;

        EnsureComp<CrewletMeltingComponent>(ent).MeltAt = _timing.CurTime + ent.Comp.MeltDelay;
        _jitter.DoJitter(ent, ent.Comp.MeltDelay, true, ent.Comp.JitterAmplitude, ent.Comp.JitterFrequency, true);
        SpawnAtPosition(ent.Comp.ThroesEffect, Transform(ent).Coordinates);
    }

    private void OnRegenMapInit(Entity<CrewletRegenComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextRegen = _timing.CurTime + ent.Comp.Interval;
    }

    private void OnHarmed(Entity<CrewletHarmScreamComponent> ent, ref DamageChangedEvent args)
    {
        var now = _timing.CurTime;
        var screams = ent.Comp.Screams;
        if (now < ent.Comp.HushedUntil
            || _mind.TryGetMind(ent, out _, out _)
            || !TryComp<EmoteOnDamageComponent>(ent, out var emote)
            || emote.LastEmoteTime != now
            || (screams.Count > 0 && screams[^1] == now))
            return;

        screams.RemoveAll(time => now - time > ent.Comp.Window);
        screams.Add(now);
        if (screams.Count < ent.Comp.Limit)
            return;

        screams.Clear();
        ent.Comp.HushedUntil = now + ent.Comp.Cooldown;
    }

    private void OnBeforeEmote(Entity<CrewletHarmScreamComponent> ent, ref BeforeEmoteEvent args)
    {
        if (_timing.CurTime < ent.Comp.HushedUntil
            && args.Emote.ID == ent.Comp.Emote
            && !_mind.TryGetMind(ent, out _, out _))
            args.Cancel();
    }

    private void OnCrewletShutdown(Entity<CrewletComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.CrewletOwner is { } owner)
            Disown(owner, ent);
    }

    private void OnOwnerShutdown(Entity<CrewletOwnerComponent> ent, ref ComponentShutdown args)
    {
        var crewlets = ent.Comp.Crewlets.ToArray();
        ent.Comp.Crewlets.Clear();

        foreach (var uid in crewlets)
        {
            if (!TerminatingOrDeleted(uid)
                && TryComp<CrewletComponent>(uid, out var crewlet)
                && crewlet.CrewletOwner == ent.Owner)
                SetOwner((uid, crewlet), null);
        }
    }

    private void Disown(EntityUid owner, EntityUid crewlet)
    {
        if (TryComp<CrewletOwnerComponent>(owner, out var marker)
            && marker.Crewlets.Remove(crewlet)
            && marker.Crewlets.Count == 0
            && !TerminatingOrDeleted(owner))
            RemComp(owner, marker);
    }

    public void DropEverything(EntityUid uid)
    {
        if (_inventory.TryGetContainerSlotEnumerator(uid, out var slots))
        {
            while (slots.MoveNext(out var slot))
            {
                _inventory.TryUnequip(uid, slot.ID, true, true);
            }
        }

        foreach (var held in _hands.EnumerateHeld(uid))
        {
            _hands.TryDrop(uid, held, checkActionBlocker: false);
        }

        if (TryComp<StorageComponent>(uid, out var storage))
            _container.EmptyContainer(storage.Container);
    }

    public static string GetShortName(string fullName)
    {
        var words = fullName.Replace('-', ' ').Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return fullName;

        return words.Length > 1 && words[0].Equals("The", StringComparison.OrdinalIgnoreCase)
            ? words[1]
            : words[0];
    }
}
