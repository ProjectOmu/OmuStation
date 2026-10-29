// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Omu.Shared.Entities.Voodoo;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Omu.Client.Entities.Voodoo;

public sealed class VoodooDollChameleonSystem : SharedVoodooDollChameleonSystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VoodooDollChameleonComponent, AfterAutoHandleStateEvent>(OnHandleState);
    }

    private void OnHandleState(Entity<VoodooDollChameleonComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateVisuals(ent);
    }

    protected override void UpdateSprite(EntityUid uid, EntityPrototype proto)
    {
        if (TryComp<SpriteComponent>(uid, out var sprite)
            && proto.TryGetComponent(out SpriteComponent? otherSprite, Factory))
            _sprite.CopySprite((EntityUid.Invalid, otherSprite), (uid, sprite));
    }
}
