// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Omu.Common.Crayon;
using Content.Omu.Shared.Crayon;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.SprayPainter.Components;

namespace Content.Omu.Shared.Decals;

public sealed class DecalRotationSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeAllEvent<DecalRotateRequestEvent>(OnRotateRequest);
        SubscribeLocalEvent<CrayonRotationComponent, CrayonDrawRotationEvent>(OnCrayonDrawRotation);
    }

    public bool CanRotate(EntityUid? held)
    {
        return HasComp<CrayonRotationComponent>(held)
            || TryComp<SprayPainterComponent>(held, out var painter) && painter.DecalMode == DecalPaintMode.Add;
    }

    private void OnRotateRequest(DecalRotateRequestEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user
            || _hands.GetActiveItem(user) is not { } held
            || !CanRotate(held))
            return;

        if (TryComp<CrayonRotationComponent>(held, out var crayon))
        {
            crayon.Rotation = (crayon.Rotation - Angle.FromDegrees(90)).Reduced();
            Dirty(held, crayon);
            return;
        }

        var painter = Comp<SprayPainterComponent>(held);
        painter.SelectedDecalAngle = (painter.SelectedDecalAngle - 90) % 360;
        Dirty(held, painter);
    }

    private void OnCrayonDrawRotation(Entity<CrayonRotationComponent> ent, ref CrayonDrawRotationEvent args)
    {
        args.Rotation = ent.Comp.Rotation;
    }
}
