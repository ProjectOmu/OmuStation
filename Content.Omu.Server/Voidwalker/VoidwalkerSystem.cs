using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Gravity;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Microsoft.CodeAnalysis.Scripting.Hosting;
using Robust.Shared.Timing;

namespace Content.Omu.Shared.Voidwalker;
public sealed partial class VoidwalkerSystem : EntitySystem
{
    [Dependency] private readonly SharedGravitySystem _gravity = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedStealthSystem _stealth = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;

    /// <inheritdoc />
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VoidwalkerComponent, MapInitEvent>(OnInit);

        SubscribeLocalEvent<VoidwalkerComponent, GridUidChangedEvent>(OnGridUidChanged);
        SubscribeLocalEvent<VoidwalkerComponent, VoidwalkerSpacedStatusChangedEvent>(OnSpacedStatusChanged);
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
        }
    }

    private bool CheckInSpace(EntityUid entity)
    {
        var transform = Transform(entity);

        // If the voidwalker is not on a grid, it is in space.
        if (transform.GridUid is not { } gridUid)
            return true;

        // If the voidwalker *is* on a grid, but the grid has no atmosphere; it is in space.
        var position = _transform.GetGridOrMapTilePosition(entity);
        var tileMixture = _atmos.GetTileMixture(gridUid, transform.MapUid, position);

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

    private void OnSpacedStatusChanged(Entity<VoidwalkerComponent> entity, ref VoidwalkerSpacedStatusChangedEvent args)
    {
        if (args.Spaced)
        {
            EnsureComp<StealthComponent>(entity);
            _stealth.SetThermalsImmune(entity, args.Spaced);
        }
        else
            RemComp<StealthComponent>(entity);
    }

}

