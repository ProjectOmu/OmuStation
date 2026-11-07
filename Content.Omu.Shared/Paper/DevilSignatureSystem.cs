// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Devil;
using Content.Omu.Common.Paper;

namespace Content.Omu.Shared.Paper;

public sealed class DevilSignatureSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DevilComponent, SignatureNameEvent>(OnSignatureName);
    }

    private void OnSignatureName(Entity<DevilComponent> ent, ref SignatureNameEvent args)
    {
        if (!string.IsNullOrWhiteSpace(ent.Comp.TrueName))
            args.Name = ent.Comp.TrueName;
    }
}
