// SPDX-License-Identifier: MIT

using Content.Shared.Eye.Blinding.Systems;

namespace Content.Omu.Shared.Arcade;

public sealed class PolybiusBlurSystem : EntitySystem
{
    [Dependency] private readonly BlurryVisionSystem _blurryVision = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PolybiusBlurComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<PolybiusBlurComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<PolybiusBlurComponent, GetBlurEvent>(OnGetBlur);
    }

    private void OnMapInit(Entity<PolybiusBlurComponent> ent, ref MapInitEvent args)
    {
        _blurryVision.UpdateBlurMagnitude(ent.Owner);
    }

    private void OnShutdown(Entity<PolybiusBlurComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        _blurryVision.UpdateBlurMagnitude(ent.Owner);
    }

    private void OnGetBlur(Entity<PolybiusBlurComponent> ent, ref GetBlurEvent args)
    {
        if (ent.Comp.LifeStage <= ComponentLifeStage.Running)
            args.Blur += ent.Comp.Blur;
    }
}
