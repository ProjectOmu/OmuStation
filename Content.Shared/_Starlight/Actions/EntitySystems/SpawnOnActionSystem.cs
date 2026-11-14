using Content.Shared._Starlight.Actions.Components;
using Content.Shared._Starlight.Actions.Events;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Robust.Shared.Network;

namespace Content.Shared._Starlight.Actions.EntitySystems;

public sealed class SpawnOnActionSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpawnOnActionComponent, MapInitEvent>(OnStartup);
        SubscribeLocalEvent<SpawnOnActionComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SpawnOnActionComponent, SpawnOnActionEvent>(OnSpawn);
    }

    private void OnStartup(EntityUid uid, SpawnOnActionComponent component, MapInitEvent args)
    {
        _actions.AddAction(uid, ref component.ActionEntity, component.Action);

        Dirty(uid, component);
    }

    private void OnShutdown(EntityUid uid, SpawnOnActionComponent component, ComponentShutdown args) =>
        _actions.RemoveAction(uid, component.ActionEntity);

    private void OnSpawn(EntityUid uid, SpawnOnActionComponent component, SpawnOnActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (_net.IsServer)
        {
            SpawnAtPosition(component.EntityToSpawn, Transform(uid).Coordinates);

            if (component.Damage != null)
                _damageable.TryChangeDamage(uid, component.Damage, origin: uid);
        }
    }
}