// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Omu.Common.Paper;
using Content.Omu.Shared.Crewlet;
using Content.Server._DV.Paper;
using Content.Server.Chat.Systems;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Server.VendingMachines;
using Content.Server.Verbs;
using Content.Shared.Access.Systems;
using Content.Shared.ActionBlocker;
using Content.Shared.Anomaly.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Climbing.Components;
using Content.Shared.CombatMode;
using Content.Shared.Cuffs;
using Content.Shared.DoAfter;
using Content.Shared.Doors.Components;
using Content.Shared.Examine;
using Content.Shared.Fluids;
using Content.Shared.Fluids.Components;
using Content.Shared.Ghost;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Item;
using Content.Shared.Maps;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Pointing;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Sound.Components;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Stunnable;
using Content.Shared.VendingMachines;
using Content.Shared.Wall;
using Content.Shared.Weapons.Melee;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Omu.Server.Crewlet;

public sealed partial class CrewletBrainSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly CommonSignatureFontSystem _signatureFont = default!;
    [Dependency] private readonly CrewletMergeSystem _merge = default!;
    [Dependency] private readonly CrewletReputationSystem _reputation = default!;
    [Dependency] private readonly CrewletSystem _crewlet = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly IngestionSystem _ingestion = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly NPCSteeringSystem _steering = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly RotateToFaceSystem _rotate = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly SharedCombatModeSystem _combat = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedCuffableSystem _cuffable = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedEntityStorageSystem _entityStorage = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private readonly SharedMoverController _mover = default!;
    [Dependency] private readonly SharedPointingSystem _pointing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly SharedPuddleSystem _puddle = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SignatureSystem _signature = default!;
    [Dependency] private readonly SleepingSystem _sleeping = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly VendingMachineSystem _vending = default!;
    [Dependency] private readonly VerbSystem _verbs = default!;

    private EntityQuery<AnomalyComponent> _anomalyQuery;
    private EntityQuery<ClimbableComponent> _climbableQuery;
    private EntityQuery<CrewletComponent> _crewletQuery;
    private EntityQuery<DoorComponent> _doorQuery;
    private EntityQuery<EdibleComponent> _edibleQuery;
    private EntityQuery<EmitSoundOnUseComponent> _hornQuery;
    private EntityQuery<EntityStorageComponent> _entityStorageQuery;
    private EntityQuery<GhostComponent> _ghostQuery;
    private EntityQuery<HumanoidAppearanceComponent> _humanoidQuery;
    private EntityQuery<InteractionPopupComponent> _popupQuery;
    private EntityQuery<ItemComponent> _itemQuery;
    private EntityQuery<MobStateComponent> _mobStateQuery;
    private EntityQuery<CrewletOwnerComponent> _ownerQuery;
    private EntityQuery<NPCSteeringComponent> _steeringQuery;
    private EntityQuery<PuddleComponent> _puddleQuery;
    private EntityQuery<SleepingComponent> _sleepingQuery;
    private EntityQuery<StrapComponent> _strapQuery;
    private EntityQuery<VendingMachineComponent> _vendingQuery;
    private EntityQuery<VirtualItemComponent> _virtualItemQuery;
    private EntityQuery<WallMountComponent> _wallMountQuery;

    public override void Initialize()
    {
        base.Initialize();

        UpdatesAfter.Add(typeof(NPCSteeringSystem));
        UpdatesBefore.Add(typeof(SharedPhysicsSystem));

        InitializeReactions();
        InitializeMerge();
        SubscribeLocalEvent<CrewletBrainComponent, MindAddedMessage>(OnMindAdded);

        _anomalyQuery = GetEntityQuery<AnomalyComponent>();
        _climbableQuery = GetEntityQuery<ClimbableComponent>();
        _crewletQuery = GetEntityQuery<CrewletComponent>();
        _doorQuery = GetEntityQuery<DoorComponent>();
        _edibleQuery = GetEntityQuery<EdibleComponent>();
        _hornQuery = GetEntityQuery<EmitSoundOnUseComponent>();
        _entityStorageQuery = GetEntityQuery<EntityStorageComponent>();
        _ghostQuery = GetEntityQuery<GhostComponent>();
        _humanoidQuery = GetEntityQuery<HumanoidAppearanceComponent>();
        _popupQuery = GetEntityQuery<InteractionPopupComponent>();
        _itemQuery = GetEntityQuery<ItemComponent>();
        _mobStateQuery = GetEntityQuery<MobStateComponent>();
        _ownerQuery = GetEntityQuery<CrewletOwnerComponent>();
        _steeringQuery = GetEntityQuery<NPCSteeringComponent>();
        _puddleQuery = GetEntityQuery<PuddleComponent>();
        _sleepingQuery = GetEntityQuery<SleepingComponent>();
        _strapQuery = GetEntityQuery<StrapComponent>();
        _vendingQuery = GetEntityQuery<VendingMachineComponent>();
        _virtualItemQuery = GetEntityQuery<VirtualItemComponent>();
        _wallMountQuery = GetEntityQuery<WallMountComponent>();
    }

    public override void Shutdown()
    {
        base.Shutdown();

        CommandBinds.Unregister<CrewletBrainSystem>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        using var query = EntityQueryEnumerator<NPCSteeringComponent, CrewletBrainComponent, InputMoverComponent>();
        while (query.MoveNext(out _, out _, out _, out var mover))
        {
            if (mover.Sprinting)
                continue;

            mover.CurTickWalkMovement = mover.CurTickSprintMovement;
            mover.CurTickSprintMovement = Vector2.Zero;
        }
    }

    public void StartThinking(EntityUid uid)
    {
        if (!TryComp<CrewletBrainComponent>(uid, out var brain))
            return;

        var now = _timing.CurTime;
        brain.Active = true;
        brain.Activity = CrewletActivity.None;
        brain.Step = CrewletStep.Start;
        brain.Target = null;
        brain.NextScan = now + _random.Next(brain.ScanInterval);
        brain.NextVocalize = now + _random.Next(brain.MinVocalizeInterval, brain.MaxVocalizeInterval);
        brain.NextTypingRoll = now + _random.Next(brain.TypingRollInterval);
        brain.NextWakeRoll = now + _random.Next(brain.WakeRollInterval);
    }

    public void Think(EntityUid uid)
    {
        if (!TryComp<CrewletBrainComponent>(uid, out var brain) || !brain.Active)
            return;

        var ent = new Entity<CrewletBrainComponent>(uid, brain);
        var now = _timing.CurTime;

        TrackOwner(ent, now);
        Perceive(ent, now);
        UpdateWatches(ent, now);
        UpdateSpeech(ent, now);
        UpdateParrot(ent, now);
        RollWake(ent, now);
        Choose(ent, now);
        Run(ent, now);
    }

    private void OnMindAdded(Entity<CrewletBrainComponent> ent, ref MindAddedMessage args)
    {
        StopThinking(ent);
    }

    public void StopThinking(EntityUid uid)
    {
        if (!TryComp<CrewletBrainComponent>(uid, out var brain) || !brain.Active)
            return;

        var ent = new Entity<CrewletBrainComponent>(uid, brain);
        End(ent);
        StopTyping(ent);
        brain.PendingLine = null;
        brain.Active = false;
    }

    private void Choose(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var next = Triggered(ent, now);
        if (next != ent.Comp.Activity)
            Switch(ent, next, now);
        else if (next == CrewletActivity.None)
            Switch(ent, CrewletActivity.Wander, now);
    }

    private CrewletActivity Triggered(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var current = ent.Comp.Activity;

        if (current > CrewletActivity.Escape && WantsEscape(ent, now))
            return CrewletActivity.Escape;

        if (current > CrewletActivity.Unbuckle && WantsUnbuckle(ent, now))
            return CrewletActivity.Unbuckle;

        if (current > CrewletActivity.BreakPull && WantsBreakPull(ent, now))
            return CrewletActivity.BreakPull;

        if (current > CrewletActivity.Retaliate && WantsRetaliate(ent, now))
            return CrewletActivity.Retaliate;

        if (current > CrewletActivity.Rest && now < ent.Comp.RestUntil)
            return CrewletActivity.Rest;

        if (current > CrewletActivity.Stay && now < ent.Comp.StayUntil)
            return CrewletActivity.Stay;

        if (current > CrewletActivity.Merge && WantsMerge(ent, now))
            return CrewletActivity.Merge;

        if (current > CrewletActivity.Recall && WantsRecall(ent, now))
            return CrewletActivity.Recall;

        if (current > CrewletActivity.CatchUp && WantsCatchUp(ent, now))
            return CrewletActivity.CatchUp;

        if (current > CrewletActivity.Mourn && WantsMourn(ent, now))
            return CrewletActivity.Mourn;

        if (current > CrewletActivity.Worry && WantsWorry(ent, now))
            return CrewletActivity.Worry;

        if (current > CrewletActivity.Deliver && WantsDeliver(ent, now))
            return CrewletActivity.Deliver;

        if (current > CrewletActivity.Fetch && WantsFetch(ent, now))
            return CrewletActivity.Fetch;

        if (current > CrewletActivity.HugReaction && WantsHugReaction(ent, now))
            return CrewletActivity.HugReaction;

        if (current > CrewletActivity.Stare && WantsStare(ent, now))
            return CrewletActivity.Stare;

        if (current > CrewletActivity.Dance && WantsDance(ent, now))
            return CrewletActivity.Dance;

        if (current > CrewletActivity.Greet && WantsGreet(ent, now))
            return CrewletActivity.Greet;

        if (current > CrewletActivity.Hello && WantsHello(ent, now))
            return CrewletActivity.Hello;

        if (current > CrewletActivity.WakeSleeper && WantsWakeSleeper(ent, now))
            return CrewletActivity.WakeSleeper;

        if (IdleTier(ent.Comp) && WantsTagged(ent))
            return CrewletActivity.Tag;

        if (current > CrewletActivity.Follow && WantsFollow(ent, now))
            return CrewletActivity.Follow;

        if (current > CrewletActivity.Eat && WantsEat(ent, now))
            return CrewletActivity.Eat;

        if (current > CrewletActivity.KeepAway && WantsKeepAway(ent, now))
            return CrewletActivity.KeepAway;

        return current;
    }

    private void Run(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        switch (ent.Comp.Activity)
        {
            case CrewletActivity.Escape:
                RunEscape(ent, now);
                break;
            case CrewletActivity.Unbuckle:
                RunUnbuckle(ent, now);
                break;
            case CrewletActivity.BreakPull:
                RunBreakPull(ent, now);
                break;
            case CrewletActivity.Retaliate:
                RunRetaliate(ent, now);
                break;
            case CrewletActivity.Rest:
                RunRest(ent, now);
                break;
            case CrewletActivity.Stay:
                RunStay(ent, now);
                break;
            case CrewletActivity.Merge:
                RunMerge(ent, now);
                break;
            case CrewletActivity.Recall:
                RunRecall(ent, now);
                break;
            case CrewletActivity.CatchUp:
            case CrewletActivity.Follow:
                RunFollow(ent, now);
                break;
            case CrewletActivity.Mourn:
                RunMourn(ent, now);
                break;
            case CrewletActivity.Worry:
                RunWorry(ent, now);
                break;
            case CrewletActivity.Hello:
                RunHello(ent, now);
                break;
            case CrewletActivity.Deliver:
                RunDeliver(ent, now);
                break;
            case CrewletActivity.Fetch:
                RunFetch(ent, now);
                break;
            case CrewletActivity.HugReaction:
                RunHugReaction(ent, now);
                break;
            case CrewletActivity.Stare:
                RunStare(ent, now);
                break;
            case CrewletActivity.Dance:
                RunDance(ent, now);
                break;
            case CrewletActivity.Greet:
                RunGreet(ent, now);
                break;
            case CrewletActivity.WakeSleeper:
                RunWakeSleeper(ent, now);
                break;
            case CrewletActivity.KeepAway:
                RunKeepAway(ent, now);
                break;
            case CrewletActivity.Hug:
                RunHug(ent, now);
                break;
            case CrewletActivity.Loot:
                RunLoot(ent, now);
                break;
            case CrewletActivity.Hat:
                RunHat(ent, now);
                break;
            case CrewletActivity.Climb:
                RunClimb(ent, now);
                break;
            case CrewletActivity.Knock:
                RunKnock(ent, now);
                break;
            case CrewletActivity.Punch:
                RunPunch(ent, now);
                break;
            case CrewletActivity.Pickpocket:
                RunPickpocket(ent, now);
                break;
            case CrewletActivity.Pet:
                RunPet(ent, now);
                break;
            case CrewletActivity.Glance:
                RunGlance(ent, now);
                break;
            case CrewletActivity.Sit:
                RunSit(ent, now);
                break;
            case CrewletActivity.Honk:
                RunHonk(ent, now);
                break;
            case CrewletActivity.Mop:
                RunMop(ent, now);
                break;
            case CrewletActivity.Mimic:
                RunMimic(ent);
                break;
            case CrewletActivity.Tag:
                RunTag(ent, now);
                break;
            case CrewletActivity.Tinker:
                RunTinker(ent, now);
                break;
            case CrewletActivity.Eat:
                RunEat(ent, now);
                break;
            case CrewletActivity.Wander:
                RunWander(ent, now);
                break;
            case CrewletActivity.Idle:
                RunIdle(ent, now);
                break;
        }
    }

    private void Switch(Entity<CrewletBrainComponent> ent, CrewletActivity next, TimeSpan now)
    {
        var target = ent.Comp.Candidate;
        End(ent);

        ent.Comp.Activity = next;
        ent.Comp.Target = target;

        if (ent.Comp.ActivityCooldowns.TryGetValue(next, out var cooldown))
            ent.Comp.ActivityReadyAt[next] = now + cooldown;
    }

    private void End(Entity<CrewletBrainComponent> ent)
    {
        StopMoving(ent);

        if (ent.Comp.Activity == CrewletActivity.KeepAway)
            SetWalking(ent, false);

        if (ent.Comp.ActivityDoAfter is { } doAfter)
        {
            ent.Comp.ActivityDoAfter = null;
            if (_doAfter.GetStatus(doAfter) == DoAfterStatus.Running)
                _doAfter.Cancel(doAfter);
        }

        if (ent.Comp.FormedTool is { } formed)
        {
            ent.Comp.FormedTool = null;
            QueueDel(formed);
        }
        else if (ent.Comp.Activity == CrewletActivity.Tinker
            && ent.Comp.RetaliationTarget == null
            && ent.Comp.Target is { } tool
            && _hands.IsHolding(ent.Owner, tool))
            _hands.TryDrop(ent.Owner, tool);

        if (ent.Comp.InCombat)
        {
            ent.Comp.InCombat = false;
            _combat.SetInCombatMode(ent, false);
        }

        if (HeldItem(ent) == null)
            ent.Comp.GiftRecipient = null;

        ent.Comp.Activity = CrewletActivity.None;
        ent.Comp.Step = CrewletStep.Start;
        ent.Comp.Target = null;
        ent.Comp.Candidate = null;
        ent.Comp.Count = 0;
    }

    private void Fail(Entity<CrewletBrainComponent> ent, TimeSpan now)
    {
        var activity = ent.Comp.Activity;
        var retry = now + ent.Comp.FailureCooldown;
        if (!ent.Comp.ActivityReadyAt.TryGetValue(activity, out var ready) || ready < retry)
            ent.Comp.ActivityReadyAt[activity] = retry;

        End(ent);
    }

    private bool IsReady(CrewletBrainComponent brain, CrewletActivity activity, TimeSpan now)
    {
        return !brain.ActivityReadyAt.TryGetValue(activity, out var ready) || now >= ready;
    }

    private void WaitFor(Entity<CrewletBrainComponent> ent, TimeSpan duration, TimeSpan now)
    {
        StopMoving(ent);
        ent.Comp.Step = CrewletStep.Wait;
        ent.Comp.StepEnds = now + duration;
    }

    private void Interact(Entity<CrewletBrainComponent> ent, EntityUid target)
    {
        if (_combat.IsInCombatMode(ent))
            _combat.SetInCombatMode(ent, false);

        ent.Comp.InCombat = false;
        _interaction.UserInteraction(ent, Transform(target).Coordinates, target);
    }

    private bool Present(EntityUid? uid)
    {
        return uid is { } target && !TerminatingOrDeleted(target);
    }
}
