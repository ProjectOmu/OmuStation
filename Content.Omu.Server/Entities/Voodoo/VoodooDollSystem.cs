// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Omu.Shared.Entities.Voodoo;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Electrocution;
using Content.Server.Pinpointer;
using Content.Server.Temperature.Systems;
using Content.Shared._EinsteinEngines.Silicon.Components;
using Content.Shared._Shitmed.Targeting;
using Content.Shared._White.Grab;
using Content.Shared.Administration.Logs;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Chat;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.Dice;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Flash;
using Content.Shared.Flash.Components;
using Content.Shared.Forensics.Components;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Item;
using Content.Shared.Kitchen.Components;
using Content.Shared.Localizations;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Speech.Muting;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Content.Shared.Temperature;
using Content.Shared.Throwing;
using Content.Shared.Timing;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Omu.Server.Entities.Voodoo;

public sealed class VoodooDollSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly ElectrocutionSystem _electrocution = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly GrabThrownSystem _grabThrown = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MovementModStatusSystem _movementMod = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedChargesSystem _charges = default!;
    [Dependency] private readonly SharedCombatModeSystem _combat = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly SharedFlashSystem _flash = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
#pragma warning disable CS0618
    [Dependency] private readonly Content.Shared.StatusEffect.StatusEffectsSystem _oldStatusEffects = default!;
#pragma warning restore CS0618
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly TemperatureSystem _temperature = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly ViewSubscriberSystem _viewSubscriber = default!;

    private readonly HashSet<Entity<MobStateComponent>> _mobs = [];
    private readonly List<EntityUid> _victims = [];
    private Action<Entity<VoodooDollComponent>, EntityUid, EntityUid>[] _diceEffects = [];

    public override void Initialize()
    {
        base.Initialize();

        _diceEffects =
        [
            Hug, ForceAttack, Fling, Trip, Shock, Flash, Blindfold, Soap, Heat, Cut, Mute, Weh, Honk, Yawn,
            (doll, target, user) => Hurl(doll, target, user, _random.NextAngle().ToVec()),
        ];

        SubscribeLocalEvent<VoodooDollComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<VoodooDollComponent, VoodooBindDoAfterEvent>(OnBindDoAfter);
        SubscribeLocalEvent<VoodooDollComponent, VoodooUnbindDoAfterEvent>(OnUnbindDoAfter);
        SubscribeLocalEvent<VoodooDollComponent, VoodooDollChooseVictimMessage>(OnChooseVictim);
        SubscribeLocalEvent<VoodooDollComponent, BoundUIClosedEvent>(OnUiClosed);
        SubscribeLocalEvent<VoodooDollComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<VoodooDollComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<VoodooDollComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<VoodooDollComponent, ThrowEvent>(OnThrow);
        SubscribeLocalEvent<VoodooDollComponent, GotUnequippedHandEvent>(OnUnequippedHand);
        SubscribeLocalEvent<VoodooDollComponent, EntityTerminatingEvent>(OnTerminating);
        SubscribeLocalEvent<VoodooDollComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<VoodooDollComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Target is not { } target)
                continue;

            if (TerminatingOrDeleted(target))
            {
                ClearTarget((uid, comp));
                continue;
            }

            if (_mobState.IsDead(target) || Paused(target))
            {
                Unbind((uid, comp), null);
                continue;
            }

            if (comp.Viewer != null && curTime >= comp.ViewEnd)
                EndView((uid, comp));

            if (comp.ForcedAttackStart is { } start
                && (curTime >= start + comp.ForcedAttackTime || TryForcedAttack((uid, comp), target)))
                comp.ForcedAttackStart = null;

            if (comp.SoapEnd != null && curTime >= comp.SoapEnd)
            {
                comp.SoapEnd = null;
                _popup.PopupEntity(Loc.GetString("voodoo-victim-soap-end"), target, target);
            }
        }
    }

    private void OnAfterInteract(Entity<VoodooDollComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (HasComp<ItemComponent>(target))
        {
            if (ent.Comp.Target is { } current)
                _popup.PopupEntity(Loc.GetString("voodoo-user-doll-bound", ("target", current)), ent, args.User);
            else
                ReadTraces(ent, target, args.User);

            args.Handled = true;
            return;
        }

        if (!HasComp<HumanoidAppearanceComponent>(target))
            return;

        if (!IsBindable(ent, target, args.User))
        {
            args.Handled = true;
            return;
        }

        args.Handled = TryStartDoAfter(ent, args.User, ent.Comp.BindDelay,
            new VoodooBindDoAfterEvent(GetNetEntity(target)), target);
    }

    private void OnBindDoAfter(Entity<VoodooDollComponent> ent, ref VoodooBindDoAfterEvent args)
    {
        var victim = GetEntity(args.Victim);
        if (args.Handled || args.Cancelled || !IsBindable(ent, victim, args.User))
            return;

        Bind(ent, victim, args.User);
        args.Handled = true;
    }

    private void OnUnbindDoAfter(Entity<VoodooDollComponent> ent, ref VoodooUnbindDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        Unbind(ent, args.User);
        args.Handled = true;
    }

    private void OnChooseVictim(Entity<VoodooDollComponent> ent, ref VoodooDollChooseVictimMessage args)
    {
        var victim = GetEntity(args.Victim);
        if (!ent.Comp.Candidates.Contains(victim) || !_hands.IsHolding(args.Actor, ent.Owner)
            || !IsBindable(ent, victim, args.Actor))
            return;

        TryStartDoAfter(ent, args.Actor, ent.Comp.RemoteBindDelay, new VoodooBindDoAfterEvent(args.Victim));
    }

    private void OnUiClosed(Entity<VoodooDollComponent> ent, ref BoundUIClosedEvent args)
    {
        if (args.UiKey.Equals(VoodooDollUiKey.Key))
            ent.Comp.Candidates.Clear();
    }

    private void OnGetVerbs(Entity<VoodooDollComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.Target == null
            || !_hands.IsHolding(args.User, ent.Owner))
            return;

        var user = args.User;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("voodoo-user-unbind"),
            Act = () => TryStartDoAfter(ent, user, ent.Comp.UnbindDelay, new VoodooUnbindDoAfterEvent()),
        });
    }

    private void OnInteractUsing(Entity<VoodooDollComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.Target is not { } target
            || GetItemEffect(ent, args.Used) is not { } effect)
            return;

        args.Handled = true;
        if (IsReady(ent, target, args.User))
            effect(ent, target, args.User);
    }

    private void OnUseInHand(Entity<VoodooDollComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.Target is not { } target)
        {
            _popup.PopupEntity(Loc.GetString("voodoo-user-not-bound"), ent, args.User);
            args.Handled = true;
            return;
        }

        if (!TryComp<TargetingComponent>(args.User, out var targeting))
            return;

        Action<Entity<VoodooDollComponent>, EntityUid, EntityUid>? effect = targeting.Target switch
        {
            TargetBodyPart.Head => Watch,
            TargetBodyPart.Chest => Hug,
            var part when (part & TargetBodyPart.Arms) != 0 => ForceAttack,
            var part when (part & TargetBodyPart.Hands) != 0 => Fling,
            var part when (part & TargetBodyPart.FullLegs) != 0 => Trip,
            _ => null,
        };

        if (effect == null)
            return;

        args.Handled = true;
        if (IsReady(ent, target, args.User))
            effect(ent, target, args.User);
    }

    private void Watch(Entity<VoodooDollComponent> ent, EntityUid target, EntityUid user)
    {
        if (!TryComp<ActorComponent>(user, out var actor))
            return;

        EndView(ent);
        _eye.SetTarget(user, target);
        _viewSubscriber.AddViewSubscriber(target, actor.PlayerSession);
        ent.Comp.Viewer = user;
        ent.Comp.ViewerSession = actor.PlayerSession;
        ent.Comp.ViewEnd = _timing.CurTime + ent.Comp.ViewTime;
        _useDelay.TryResetDelay(ent.Owner, id: ent.Comp.DelayId);

        _popup.PopupEntity(Loc.GetString("voodoo-user-eyes-start", ("target", target)), ent, user);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(user):user} watched {ToPrettyString(target):target} via {ToPrettyString(ent):doll}");
    }

    private void Hug(Entity<VoodooDollComponent> ent, EntityUid target, EntityUid user)
    {
        _useDelay.TryResetDelay(ent.Owner, id: ent.Comp.DelayId);
        _popup.PopupEntity(Loc.GetString("voodoo-user-hug"), ent, user);
        _popup.PopupEntity(Loc.GetString("voodoo-victim-hug"), target, target);
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(user):user} hugged {ToPrettyString(target):target} via {ToPrettyString(ent):doll}");
    }

    // We have rage at home
    private void ForceAttack(Entity<VoodooDollComponent> ent, EntityUid target, EntityUid user)
    {
        ent.Comp.ForcedAttackStart = _timing.CurTime;
        Afflict(ent, target, user, "voodoo-user-attack", null, "attack");
    }

    private bool TryForcedAttack(Entity<VoodooDollComponent> ent, EntityUid target)
    {
        if (!TryComp<CombatModeComponent>(target, out var combat))
            return false;

        if (_gun.TryGetGun(target, out var gun))
        {
            if (gun.Comp.NextFire > _timing.CurTime
                || PickForcedAttackVictim(target, ent.Comp.ForcedShotRange) is not { } shot)
                return false;

            return _gun.AttemptShoot(target, gun, Transform(shot).Coordinates, shot);
        }

        if (!_melee.TryGetWeapon(target, out var weapon, out var melee) || melee.NextAttack > _timing.CurTime
            || PickForcedAttackVictim(target, melee.Range) is not { } hit)
            return false;

        var wasInCombatMode = combat.IsInCombatMode;
        _combat.SetInCombatMode(target, true, combat);
        var attacked = _melee.AttemptLightAttack(target, weapon, melee, hit);
        _combat.SetInCombatMode(target, wasInCombatMode, combat);
        return attacked;
    }

    private EntityUid? PickForcedAttackVictim(EntityUid attacker, float range)
    {
        _mobs.Clear();
        _victims.Clear();
        _lookup.GetEntitiesInRange(Transform(attacker).Coordinates, range, _mobs, LookupFlags.Dynamic);
        foreach (var mob in _mobs)
        {
            if (mob.Owner != attacker && _examine.InRangeUnOccluded(attacker, mob, range + 1f))
                _victims.Add(mob.Owner);
        }

        return _victims.Count == 0 ? null : _random.Pick(_victims);
    }

    private void Fling(Entity<VoodooDollComponent> ent, EntityUid target, EntityUid user)
    {
        var held = _hands.EnumerateHeld(target).Where(item => !HasComp<VirtualItemComponent>(item)).ToList();
        if (held.Count > 0)
        {
            var item = _random.Pick(held);
            var direction = _random.NextAngle().ToVec() * ent.Comp.FlingDistance;
            if (_hands.TryDrop(target, item, checkActionBlocker: false))
                _throwing.TryThrow(item, direction, ent.Comp.FlingSpeed, target,
                    predicted: false); // Trauma
        }

        Afflict(ent, target, user, "voodoo-user-fling", "voodoo-victim-fling", "fling");
    }

    private void Trip(Entity<VoodooDollComponent> ent, EntityUid target, EntityUid user)
    {
        _stun.TryKnockdown(target, ent.Comp.TripTime, force: true);
        Afflict(ent, target, user, "voodoo-user-trip", "voodoo-victim-trip", "trip");
    }

    private void OnThrow(Entity<VoodooDollComponent> ent, ref ThrowEvent args)
    {
        if (ent.Comp.Target is not { } target || !TryComp<PhysicsComponent>(ent, out var physics)
            || physics.LinearVelocity == Vector2.Zero || !IsReady(ent, target, args.User))
            return;

        Hurl(ent, target, args.User, physics.LinearVelocity.Normalized());
    }

    private void Hurl(Entity<VoodooDollComponent> ent, EntityUid target, EntityUid? user, Vector2 direction)
    {
        _grabThrown.Throw(target, user ?? ent.Owner, direction * ent.Comp.ThrowDistance, ent.Comp.ThrowSpeed,
            ent.Comp.ThrowDamage);
        Afflict(ent, target, user, null, "voodoo-victim-thrown", "throw");
    }

    private void OnUnequippedHand(Entity<VoodooDollComponent> ent, ref GotUnequippedHandEvent args)
    {
        if (ent.Comp.Viewer == args.User)
            EndView(ent);
    }

    private void OnTerminating(Entity<VoodooDollComponent> ent, ref EntityTerminatingEvent args)
    {
        if (ent.Comp.Target is not { } target || TerminatingOrDeleted(target)
            || TerminatingOrDeleted(Transform(ent).MapUid))
            return;

        _damageable.TryChangeDamage(target, ent.Comp.DestroyedDamage, true, canMiss: false);
        Afflict(ent, target, null, null, "voodoo-victim-destroyed", "destruction");
        ClearTarget(ent);
        StartRebindCooldown(ent, target);
    }

    private void OnShutdown(Entity<VoodooDollComponent> ent, ref ComponentShutdown args)
    {
        EndView(ent);
    }

    private Action<Entity<VoodooDollComponent>, EntityUid, EntityUid>? GetItemEffect(Entity<VoodooDollComponent> ent,
        EntityUid used)
    {
        if (HasComp<DiceComponent>(used))
            return (doll, target, user) => _random.Pick(_diceEffects)(doll, target, user);

        if (HasComp<StunbatonComponent>(used))
            return Shock;

        if (HasComp<FlashComponent>(used))
        {
            TryComp<LimitedChargesComponent>(used, out var charges);
            if (charges != null && _charges.IsEmpty((used, charges)))
                return null;

            return (doll, target, user) =>
            {
                if (charges != null)
                    _charges.TryUseCharge((used, charges));

                Flash(doll, target, user);
            };
        }

        if (HasComp<BlindfoldComponent>(used))
            return Blindfold;

        if (_tag.HasTag(used, ent.Comp.SoapTag))
            return Soap;

        var hot = new IsHotEvent();
        RaiseLocalEvent(used, hot);
        if (hot.IsHot)
            return Heat;

        if (HasComp<SharpComponent>(used))
            return Cut;

        if (_tag.HasTag(used, ent.Comp.PenTag))
            return Mute;

        if (_tag.HasTag(used, ent.Comp.LizardPlushieTag))
            return Weh;

        if (_tag.HasTag(used, ent.Comp.BikeHornTag))
            return Honk;

        if (_tag.HasTag(used, ent.Comp.BedsheetTag))
            return Yawn;

        return null;
    }

    private void Shock(Entity<VoodooDollComponent> ent, EntityUid target, EntityUid user)
    {
        _electrocution.TryDoElectrocution(target, null, ent.Comp.ShockDamage, ent.Comp.ShockTime, true,
            ignoreInsulation: true);
        Afflict(ent, target, user, "voodoo-user-shock", null, "shock");
    }

    private void Flash(Entity<VoodooDollComponent> ent, EntityUid target, EntityUid user)
    {
        _flash.Flash(target, null, null, ent.Comp.FlashTime, ent.Comp.FlashSlowTo, displayPopup: false);
        _audio.PlayPvs(ent.Comp.FlashSound, target);
        Afflict(ent, target, user, "voodoo-user-flash", "voodoo-victim-flash", "flash");
    }

    private void Blindfold(Entity<VoodooDollComponent> ent, EntityUid target, EntityUid user)
    {
        _statusEffects.TryUpdateStatusEffectDuration(target, ent.Comp.BlindStatusEffect, ent.Comp.BlindTime);
        Afflict(ent, target, user, "voodoo-user-blindfold", "voodoo-victim-blindfold", "blindfold");
    }

    private void Soap(Entity<VoodooDollComponent> ent, EntityUid target, EntityUid user)
    {
        ent.Comp.SoapEnd = _timing.CurTime + ent.Comp.SoapTime;
        _movementMod.TryUpdateFrictionModDuration(target, ent.Comp.SoapTime, ent.Comp.SoapFriction);
        Afflict(ent, target, user, "voodoo-user-soap", "voodoo-victim-soap", "soap");
    }

    private void Heat(Entity<VoodooDollComponent> ent, EntityUid target, EntityUid user)
    {
        _temperature.ChangeHeat(target, ent.Comp.HeatChange * _temperature.GetHeatCapacity(target), true);
        _audio.PlayPvs(ent.Comp.HeatSound, target,
            ent.Comp.HeatSound.Params.WithVariation(MeleeSoundSystem.DamagePitchVariation));

        var ignite = _random.Prob(ent.Comp.IgniteChance);
        if (ignite)
            _flammable.AdjustFireStacks(target, 1, ignite: true);

        Afflict(ent, target, user, "voodoo-user-heat",
            ignite ? "voodoo-victim-heat-ignite" : "voodoo-victim-heat", "heat");
    }

    private void Cut(Entity<VoodooDollComponent> ent, EntityUid target, EntityUid user)
    {
        _damageable.TryChangeDamage(target, ent.Comp.CutDamage, true, canMiss: false);
        _bloodstream.TryModifyBleedAmount(target, ent.Comp.BleedAmount);
        _audio.PlayPvs(ent.Comp.CutSound, target,
            ent.Comp.CutSound.Params.WithVariation(MeleeSoundSystem.DamagePitchVariation));
        Afflict(ent, target, user, "voodoo-user-cut", "voodoo-victim-cut", "cut");
    }

    private void Mute(Entity<VoodooDollComponent> ent, EntityUid target, EntityUid user)
    {
#pragma warning disable CS0618
        _oldStatusEffects.TryAddStatusEffect<MutedComponent>(target, ent.Comp.MuteStatusEffect,
            ent.Comp.MuteTime, true);
#pragma warning restore CS0618
        Afflict(ent, target, user, "voodoo-user-poke", "voodoo-victim-mute", "mute");
    }

    private void Weh(Entity<VoodooDollComponent> ent, EntityUid target, EntityUid user)
    {
        _chat.TryEmoteWithChat(target, ent.Comp.WehEmote, forceEmote: true);
        Afflict(ent, target, user, "voodoo-user-weh", null, "weh", hint: false);
    }

    private void Honk(Entity<VoodooDollComponent> ent, EntityUid target, EntityUid user)
    {
        _chat.TryEmoteWithChat(target, ent.Comp.HonkEmote, forceEmote: true);
        Afflict(ent, target, user, "voodoo-user-honk", null, "honk", hint: false);
    }

    private void Yawn(Entity<VoodooDollComponent> ent, EntityUid target, EntityUid user)
    {
        _chat.TryEmoteWithChat(target, ent.Comp.YawnEmote, forceEmote: true);
        Afflict(ent, target, user, "voodoo-user-yawn", null, "yawn", hint: false);
    }

    private bool TryStartDoAfter(Entity<VoodooDollComponent> ent, EntityUid user, TimeSpan delay, DoAfterEvent ev,
        EntityUid? target = null)
    {
        var doAfter = new DoAfterArgs(EntityManager, user, delay, ev, ent, target, ent)
        {
            Hidden = true,
            NeedHand = true,
            BreakOnMove = true,
            BreakOnDamage = true,
        };

        return _doAfter.TryStartDoAfter(doAfter);
    }

    private bool IsReady(Entity<VoodooDollComponent> ent, EntityUid target, EntityUid? user)
    {
        string message;
        if (user != null && !_hands.IsHolding(user.Value, ent.Owner) && _container.IsEntityInContainer(ent))
            message = Loc.GetString("voodoo-user-stowed");
        else if (_useDelay.IsDelayed(ent.Owner, ent.Comp.DelayId))
            message = Loc.GetString("voodoo-user-cooldown");
        else if (ent.Comp.Range is { } range && !_transform.InRange(ent.Owner, target, range))
            message = Loc.GetString("voodoo-user-out-of-range", ("target", target));
        else if (IsShielded(ent, target))
            message = Loc.GetString("voodoo-user-shielded", ("target", target));
        else
            return true;

        if (user != null)
            _popup.PopupEntity(message, ent, user.Value);

        return false;
    }

    private bool IsShielded(Entity<VoodooDollComponent> ent, EntityUid target)
    {
        return TryComp<BloodstreamComponent>(target, out var bloodstream)
            && _solutionContainer.ResolveSolution(target, bloodstream.BloodSolutionName,
                ref bloodstream.BloodSolution, out var blood)
            && blood.ContainsPrototype(ent.Comp.HolyReagent);
    }

    private bool IsBindable(Entity<VoodooDollComponent> ent, EntityUid target, EntityUid user)
    {
        if (TerminatingOrDeleted(target) || _mobState.IsDead(target) || Paused(target))
            return false;

        if (ent.Comp.Target is { } current)
        {
            _popup.PopupEntity(Loc.GetString("voodoo-user-doll-bound", ("target", current)), ent, user);
            return false;
        }

        if (ent.Comp.RebindCooldownStatusEffect is { } cooldown && _statusEffects.HasStatusEffect(target, cooldown))
        {
            _popup.PopupEntity(Loc.GetString("voodoo-user-rebind-cooldown", ("target", target)), ent, user);
            return false;
        }

        var query = AllEntityQuery<VoodooDollComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (uid == ent.Owner || comp.Target != target)
                continue;

            _popup.PopupEntity(Loc.GetString("voodoo-user-already-bound", ("target", target)), ent, user);
            return false;
        }

        return true;
    }

    private void StartRebindCooldown(Entity<VoodooDollComponent> ent, EntityUid target)
    {
        if (ent.Comp.RebindCooldownStatusEffect is { } cooldown)
            _statusEffects.TryUpdateStatusEffectDuration(target, cooldown, ent.Comp.RebindCooldown);
    }

    private void Afflict(Entity<VoodooDollComponent> ent, EntityUid target, EntityUid? user, string? userMessage,
        string? victimMessage, string cause, bool hint = true)
    {
        _useDelay.TryResetDelay(ent.Owner, id: ent.Comp.DelayId);

        if (user != null && userMessage != null)
            _popup.PopupEntity(Loc.GetString(userMessage), ent, user.Value);

        if (victimMessage != null)
            _popup.PopupEntity(Loc.GetString(victimMessage), target, target, PopupType.LargeCaution);

        if (hint)
            GiveHint(ent, target);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(user):user} hit {ToPrettyString(target):target} via {ToPrettyString(ent):doll}: {cause}");
    }

    private void ReadTraces(Entity<VoodooDollComponent> ent, EntityUid item, EntityUid user)
    {
        ent.Comp.Candidates = GetCandidates(item);
        if (ent.Comp.Candidates.Count == 0)
        {
            _ui.CloseUi(ent.Owner, VoodooDollUiKey.Key, user);
            _popup.PopupEntity(Loc.GetString("voodoo-user-read-empty", ("item", item)), ent, user);
            return;
        }

        var candidates = ent.Comp.Candidates
            .Select(uid => new VoodooDollCandidate(GetNetEntity(uid), Name(uid), MetaData(uid).EntityPrototype?.ID))
            .ToList();

        _popup.PopupEntity(Loc.GetString("voodoo-user-read", ("item", item)), ent, user);
        _ui.OpenUi(ent.Owner, VoodooDollUiKey.Key, user);
        _ui.SetUiState(ent.Owner, VoodooDollUiKey.Key, new VoodooDollBoundUserInterfaceState(candidates));
    }

    private void Bind(Entity<VoodooDollComponent> ent, EntityUid target, EntityUid user)
    {
        ent.Comp.Target = target;

        if (!HasComp<SiliconComponent>(target))
        {
            SendVoodooMessage(ent, target, Loc.GetString("voodoo-victim-bound"));
            _audio.PlayGlobal(ent.Comp.BindSound, target);
        }

        _audio.PlayPvs(ent.Comp.BindUserSound, user);

        _popup.PopupEntity(Loc.GetString("voodoo-user-bound", ("target", target)), ent, user);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(user):user} bound {ToPrettyString(ent):doll} to {ToPrettyString(target):target}");
    }

    private void Unbind(Entity<VoodooDollComponent> ent, EntityUid? user)
    {
        if (ent.Comp.Target is not { } target)
            return;

        ClearTarget(ent);
        StartRebindCooldown(ent, target);

        if (!HasComp<SiliconComponent>(target))
            _popup.PopupEntity(Loc.GetString("voodoo-victim-unbound"), target, target);

        if (user != null)
            _popup.PopupEntity(Loc.GetString("voodoo-user-unbound"), ent, user.Value);
        else
            _popup.PopupEntity(Loc.GetString("voodoo-user-unbound"), ent);

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(user):user} unbound {ToPrettyString(ent):doll} from {ToPrettyString(target):target}");
    }

    private void ClearTarget(Entity<VoodooDollComponent> ent)
    {
        EndView(ent);
        ent.Comp.Target = null;
        ent.Comp.SoapEnd = null;
        ent.Comp.ForcedAttackStart = null;
    }

    private void EndView(Entity<VoodooDollComponent> ent)
    {
        if (ent.Comp.Viewer is not { } viewer)
            return;

        ent.Comp.Viewer = null;
        if (ent.Comp.Target is { } target && ent.Comp.ViewerSession is { } session)
            _viewSubscriber.RemoveViewSubscriber(target, session);

        ent.Comp.ViewerSession = null;
        if (TerminatingOrDeleted(viewer))
            return;

        _eye.SetTarget(viewer, null);
        _popup.PopupEntity(Loc.GetString("voodoo-user-eyes-end"), viewer, viewer);
    }

    private List<EntityUid> GetCandidates(EntityUid item)
    {
        var candidates = new List<EntityUid>();
        if (!TryComp<ForensicsComponent>(item, out var forensics))
            return candidates;

        var query = EntityQueryEnumerator<HumanoidAppearanceComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (_mobState.IsDead(uid))
                continue;

            var printMatch = TryComp<FingerprintComponent>(uid, out var print) && print.Fingerprint != null
                && forensics.Fingerprints.Contains(print.Fingerprint);
            var dnaMatch = TryComp<DnaComponent>(uid, out var dna) && dna.DNA != null
                && forensics.DNAs.Any(trace => trace.Item1 == dna.DNA);

            if (printMatch || dnaMatch)
                candidates.Add(uid);
        }

        return candidates;
    }

    private void GiveHint(Entity<VoodooDollComponent> ent, EntityUid target)
    {
        if (!ent.Comp.LocationHints)
            return;

        var dollCoords = _transform.GetMapCoordinates(ent);
        var targetCoords = _transform.GetMapCoordinates(target);

        string message;
        if (_random.Prob(ent.Comp.HintBeaconChance)
            && _navMap.TryGetNearestBeacon(dollCoords, out var beacon, out _) && beacon.Value.Comp.Text != null)
        {
            message = Loc.GetString("voodoo-victim-hint-beacon", ("beacon", beacon.Value.Comp.Text));
        }
        else if (dollCoords.MapId == targetCoords.MapId && dollCoords.Position != targetCoords.Position)
        {
            var gridRotation = Transform(target).GridUid is { } grid ? _transform.GetWorldRotation(grid) : Angle.Zero;
            var direction = ((dollCoords.Position - targetCoords.Position).ToWorldAngle() - gridRotation).GetDir();
            message = Loc.GetString("voodoo-victim-hint-direction",
                ("direction", ContentLocalizationManager.FormatDirection(direction)));
        }
        else
        {
            return;
        }

        SendVoodooMessage(ent, target, message);
    }

    private void SendVoodooMessage(Entity<VoodooDollComponent> ent, EntityUid target, string message)
    {
        if (!TryComp<ActorComponent>(target, out var actor))
            return;

        var size = ent.Comp.FontSize;
        var wrapped = Loc.GetString("voodoo-base-message", ("size", size), ("text", message.Replace('"', '\'')));
        SharedChatSystem.UpdateFontSize(size, ref message, ref wrapped);
        _chatManager.ChatMessageToOne(ChatChannel.Server, message, wrapped, default, false,
            actor.PlayerSession.Channel, canCoalesce: false);
    }
}
