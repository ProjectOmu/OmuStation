using Content.Omu.Shared.Voidwalker;
using Content.Server.Administration.Systems;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Server.Emoting.Systems;
using Content.Server.Mind;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Emoting;
using Content.Shared.Examine;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.Stunnable;
using Content.Shared.Traits.Assorted;
using Content.Shared.Verbs;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Omu.Server.Voidwalker;
public sealed partial class VoidwalkerSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly SharedSlurredSystem _slurred = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly RejuvenateSystem _rejuvenate = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    /// <summary>
    /// If the voidwalker is within this much of a passed object, don't count it as being in space.
    /// This is to prevent being able to stand inside a passed object, since they have no atmosphere inside.
    /// If you can think of a better way to handle this, do tell me - delph
    /// </summary>
    private const float PassedObjectGraceRange = 1; //

    private static Entity<MapComponent>? theVoid;

    /// <inheritdoc />
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VoidwalkerComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnCleanup);

        SubscribeLocalEvent<VoidwalkerComponent, GridUidChangedEvent>(OnGridUidChanged);

        SubscribeLocalEvent<VoidwalkerComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<VoidwalkerComponent, GetVerbsEvent<InnateVerb>>(OnGetVerbs);


        SubscribeAbilities();
    }

    private void OnInit(Entity<VoidwalkerComponent> entity, ref MapInitEvent args)
    {
        if (theVoid == null
            && _mapLoader.TryLoadMap(entity.Comp.MapPath, out theVoid, out _, new DeserializationOptions { InitializeMaps = true }))
            _map.SetPaused(theVoid.Value.Comp.MapId, false); // load T??HE V??OID

        UpdateSpacedStatus(entity);
        _meta.AddFlag(entity, MetaDataFlags.ExtraTransformEvents); // So we can check when they leave a grid.
    }

    private void OnCleanup(RoundRestartCleanupEvent args)
    {
        if (theVoid is not null)
            QueueDel(theVoid);

        theVoid = null;
    }

    #region Updating Spaced Status

    private void OnGridUidChanged(Entity<VoidwalkerComponent> entity, ref GridUidChangedEvent args) =>
        UpdateSpacedStatus(entity);

    /// <inheritdoc />
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Check every X amount of seconds. Just in case.
        var query = EntityQueryEnumerator<VoidwalkerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime > comp.NextSpacedCheck)
            {
                UpdateSpacedStatus((uid, comp));
                comp.NextSpacedCheck = _timing.CurTime + comp.SpacedCheckInterval;
            }

            if (_timing.CurTime > comp.NextHealingTick)
            {
                if (comp.HealingWhenSpaced is { } healing)
                    _damage.TryChangeDamage(uid, healing);

                comp.NextHealingTick = _timing.CurTime + comp.HealingTickInterval;
            }

            foreach (var (ent, expiry) in comp.EntitiesPassed)
            {
                if (_timing.CurTime > expiry)
                {
                    comp.EntitiesPassed.Remove(ent);
                    EntityManager.RemoveComponents(ent, comp.ComponentsAddedOnPass);
                }
            }
        }
    }

    private void UpdateSpacedStatus(Entity<VoidwalkerComponent> entity)
    {
        var isInSpace = CheckInSpace(entity.Owner, entity.Comp);
        entity.Comp.IsInSpace = isInSpace;

        var ev = new VoidwalkerSpacedStatusChangedEvent(isInSpace);
        RaiseLocalEvent(entity, ref ev);
    }

    #endregion


    #region Helpers

    public bool CheckInSpace(EntityUid uid, VoidwalkerComponent? voidwalker = null)
    {
        var entityXform = Transform(uid);

        // Check if the voidwalker is standing inside a passed object.
        // is this hacky? Yes. Very.
        if (voidwalker is not null)
            foreach (var (entityPassed, _) in voidwalker.EntitiesPassed)
                if (_transform.InRange(uid, entityPassed, PassedObjectGraceRange))
                  return false;

        // If the voidwalker is not on a grid, it is in space.
        if (entityXform.GridUid is not { } gridUid)
            return true;

        // If the voidwalker *is* on a grid, but the grid has no atmosphere; it is in space.
        var position = _transform.GetGridOrMapTilePosition(uid);
        var tileMixture = _atmos.GetTileMixture(gridUid, entityXform.MapUid, position);

        if (tileMixture is null)
            return true;

        return tileMixture.Pressure <= 0;
    }

    private bool CanSeeVoidwalker(EntityUid target)
    {
        if (HasComp<PermanentBlindnessComponent>(target)
            || HasComp<TemporaryBlindnessComponent>(target))
            return false;

        return !TryComp<BlindableComponent>(target, out var blindable)
               || blindable.EyeDamage < blindable.MaxDamage;
    }

    #endregion


    private void OnExamined(Entity<VoidwalkerComponent> entity, ref ExaminedEvent args)
    {
        if (entity.Comp.UnsettleDoAfterId is not { } doAfterId)
            return;

        _doAfter.Cancel(entity, doAfterId);
        entity.Comp.UnsettleDoAfterId = null;

        var popup = Loc.GetString("voidwalker-unsettle-fail-looked-at");
        _popup.PopupEntity(popup, entity, entity, PopupType.MediumCaution);
    }

    private void OnGetVerbs(Entity<VoidwalkerComponent> entity, ref GetVerbsEvent<InnateVerb> args)
    {
        var target = args.Target;

        if (!args.CanInteract
            || !args.CanAccess
            || !entity.Comp.IsInSpace
            || !HasComp<MobStateComponent>(target)
            || !_mobState.IsCritical(target))
            return;

        InnateVerb kidnapVerb = new()
        {
            Act = () => StartKidnap(entity, target),
            Text = Loc.GetString("voidwalker-kidnap-verb"),
            Message = Loc.GetString("voidwalker-kidnap-verb-text"),
            Icon = new SpriteSpecifier.Rsi(new ResPath("_Omu/Actions/voidwalker.rsi"), "kidnap"),
            Priority = 1,
        };

        args.Verbs.Add(kidnapVerb);
    }

}

