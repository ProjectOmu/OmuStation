using Content.Omu.Shared.Voidwalker;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Server.Emoting.Systems;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Emoting;
using Content.Shared.Examine;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.Stunnable;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Timing;

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

    /// <summary>
    /// If the voidwalker is within this much of a passed object, don't count it as being in space.
    /// This is to prevent being able to stand inside a passed object, since they have no atmosphere inside.
    /// If you can think of a better way to handle this, do tell me - delph
    /// </summary>
    private const float PassedObjectGraceRange = 1; //

    /// <inheritdoc />
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VoidwalkerComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<VoidwalkerComponent, GridUidChangedEvent>(OnGridUidChanged);

        SubscribeLocalEvent<VoidwalkerComponent, ExaminedEvent>(OnExamined);

        SubscribeAbilities();
    }

    private void OnInit(Entity<VoidwalkerComponent> entity, ref MapInitEvent args)
    {
        UpdateSpacedStatus(entity);
        _meta.AddFlag(entity, MetaDataFlags.ExtraTransformEvents); // So we can check when they leave a grid.
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

    private bool CheckInSpace(Entity<VoidwalkerComponent> voidwalker)
    {
        var voidwalkerXform = Transform(voidwalker);

        // Check if the voidwalker is standing inside a passed object.
        // is this hacky? Yes. Very.
        foreach (var (entityPassed, _) in voidwalker.Comp.EntitiesPassed)
            if (_transform.InRange(voidwalker.Owner, entityPassed, PassedObjectGraceRange))
                return false;

        // If the voidwalker is not on a grid, it is in space.
        if (voidwalkerXform.GridUid is not { } gridUid)
            return true;

        // If the voidwalker *is* on a grid, but the grid has no atmosphere; it is in space.
        var position = _transform.GetGridOrMapTilePosition(voidwalker);
        var tileMixture = _atmos.GetTileMixture(gridUid, voidwalkerXform.MapUid, position);

        if (tileMixture is null)
            return true;

        return tileMixture.Pressure <= 0;
    }

    private void UpdateSpacedStatus(Entity<VoidwalkerComponent> entity)
    {
        var isInSpace = CheckInSpace(entity);
        entity.Comp.IsInSpace = isInSpace;

        var ev = new VoidwalkerSpacedStatusChangedEvent(isInSpace);
        RaiseLocalEvent(entity, ref ev);
    }

    #endregion

    private bool CanSeeVoidwalker(EntityUid target)
    {
        if (HasComp<PermanentBlindnessComponent>(target)
            || HasComp<TemporaryBlindnessComponent>(target))
            return false;

        return !TryComp<BlindableComponent>(target, out var blindable)
               || blindable.EyeDamage < blindable.MaxDamage;
    }

    private void OnExamined(Entity<VoidwalkerComponent> entity, ref ExaminedEvent args)
    {
        if (entity.Comp.UnsettleDoAfterId is not { } doAfterId)
            return;

        _doAfter.Cancel(entity, doAfterId);
        entity.Comp.UnsettleDoAfterId = null;

        var popup = Loc.GetString("voidwalker-unsettle-fail-looked-at");
        _popup.PopupEntity(popup, entity, entity, PopupType.MediumCaution);
    }

}

