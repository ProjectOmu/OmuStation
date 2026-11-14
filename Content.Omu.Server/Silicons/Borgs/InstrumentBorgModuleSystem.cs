// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Omu.Shared.Silicons.Borgs;
using Content.Shared.Actions;
using Content.Shared.Instruments;
using Content.Shared.Silicons.Borgs.Components;

namespace Content.Omu.Server.Silicons.Borgs;

public sealed class InstrumentBorgModuleSystem : SharedInstrumentBorgModuleSystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InstrumentBorgModuleComponent, BorgModuleInstalledEvent>(OnInstalled);
        SubscribeLocalEvent<InstrumentBorgModuleComponent, BorgModuleUninstalledEvent>(OnUninstalled);
    }

    private void OnInstalled(Entity<InstrumentBorgModuleComponent> ent, ref BorgModuleInstalledEvent args)
    {
        EntityManager.AddComponents(args.ChassisEnt, ent.Comp.Components);
        _ui.SetUi(args.ChassisEnt, InstrumentUiKey.Key,
            new InterfaceData("InstrumentBoundUserInterface", requireInputValidation: false));
        _actions.AddAction(args.ChassisEnt, ref ent.Comp.ActionEntity, ent.Comp.Action, ent);
    }

    private void OnUninstalled(Entity<InstrumentBorgModuleComponent> ent, ref BorgModuleUninstalledEvent args)
    {
        _ui.CloseUi(args.ChassisEnt, InstrumentUiKey.Key);
        _actions.RemoveAction(args.ChassisEnt, ent.Comp.ActionEntity);
        EntityManager.RemoveComponents(args.ChassisEnt, ent.Comp.Components);
    }
}
