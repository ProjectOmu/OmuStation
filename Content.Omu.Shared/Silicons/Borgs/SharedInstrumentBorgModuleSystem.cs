// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Shared.Containers;

namespace Content.Omu.Shared.Silicons.Borgs;

public abstract class SharedInstrumentBorgModuleSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InstrumentBorgModuleComponent, ContainerGettingInsertedAttemptEvent>(OnInsertAttempt);
    }

    private void OnInsertAttempt(Entity<InstrumentBorgModuleComponent> ent,
        ref ContainerGettingInsertedAttemptEvent args)
    {
        if (!TryComp<BorgChassisComponent>(args.Container.Owner, out var chassis)
            || args.Container != chassis.ModuleContainer
            || !chassis.ModuleContainer.ContainedEntities.Any(HasComp<InstrumentBorgModuleComponent>))
            return;

        args.Cancel();

        if (_container.TryGetContainingContainer(ent.Owner, out var holder))
            _popup.PopupClient(Loc.GetString("borg-module-duplicate"), args.Container.Owner, holder.Owner);
    }
}
