// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 SolsticeOfTheWinter <solsticeofthewinter@gmail.com>
// SPDX-FileCopyrightText: 2025 TheBorzoiMustConsume <197824988+TheBorzoiMustConsume@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Religion.Nullrod;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.Audio;

namespace Content.Omu.Server.Religion.OnPray.ReloadOnPrayShotgun;

public sealed partial class ReloadOnPrayShotgunSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audioSystem = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ReloadOnPrayShotgunComponent, AlternatePrayEvent>(OnPray);
    }

    private void OnPray(EntityUid uid, ReloadOnPrayShotgunComponent comp, ref AlternatePrayEvent args)
    {
        if (!TryComp<BallisticAmmoProviderComponent>(uid, out var ammoProvider) || ammoProvider.Capacity == null)
            return;

        if (!_gun.UpdateBallisticAmmoCount(uid, ammoProvider.Capacity, ammoProvider))
            return;

        _audioSystem.PlayPvs(comp.ReloadSoundPath, uid);
    }
}
