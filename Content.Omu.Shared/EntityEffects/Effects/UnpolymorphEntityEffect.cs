using Robust.Shared.Prototypes;
using Content.Shared.EntityEffects;

namespace Content.Omu.Shared.EntityEffects.Effects;

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class Unpolymorph : EntityEffectBase<Unpolymorph>
{
    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("entity-effect-status-effect-unpolymorph");
}
