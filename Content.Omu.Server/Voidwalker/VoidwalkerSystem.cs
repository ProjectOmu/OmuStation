using Content.Server.Atmos.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Gravity;
using Robust.Shared.Timing;

namespace Content.Omu.Shared.Voidwalker;
public sealed partial class VoidwalkerSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;

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



}

