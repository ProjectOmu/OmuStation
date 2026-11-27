// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Silicons.Borgs;
using Content.Shared.Silicons.Borgs.Components;

namespace Content.Omu.Server.Silicons.Borgs;

public sealed class RemovableDefaultModuleSystem : EntitySystem
{
    [Dependency] private readonly BorgSystem _borg = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BorgModuleComponent, BorgModuleInstalledEvent>(OnInstalled);
    }

    private void OnInstalled(Entity<BorgModuleComponent> ent, ref BorgModuleInstalledEvent args)
    {
        if (ent.Comp.DefaultModule)
            _borg.SetBorgModuleDefault(ent, false);
    }
}
