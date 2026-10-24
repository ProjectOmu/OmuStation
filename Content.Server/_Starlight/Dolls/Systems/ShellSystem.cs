// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._StarLight.Dolls.Components;
using Content.Shared.Actions;

namespace Content.Server._StarLight.Dolls.Systems;

public sealed class ShellSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShellComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ShellComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnMapInit(EntityUid uid, ShellComponent component, MapInitEvent args)
    {
        _actions.AddAction(uid, ref component.GenerateShellPieceActionEntity, component.GenerateShellPieceAction);
        Dirty(uid, component);
    }

    private void OnShutdown(EntityUid uid, ShellComponent component, ComponentShutdown args)
    {
        _actions.RemoveAction(uid, component.GenerateShellPieceActionEntity);
    }
}