using Content.Shared.EntityEffects;
using Content.Shared.Polymorph;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._Mono.EntityEffects;

// Omustation heavy edit.
// AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA
public sealed partial class RevertPolymorph : EntityEffectBase<RevertPolymorph>
{
    [DataField(required: true, customTypeSerializer: typeof(PrototypeIdSerializer<PolymorphPrototype>))]
    public string Prototype { get; set; } = default!;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        var polymorph = prototype.Index<PolymorphPrototype>(Prototype);
        var entName = "Unknown";

        if (polymorph.Configuration.Entity is { } entity)
            entName = prototype.Index<EntityPrototype>(entity.Id).Name;

        return Loc.GetString("reagent-effect-guidebook-revert-polymorph", ("chance", Probability), ("entityname", entName));
    }
}


// KILL ME! BUT WHAT AM I MEANT TO DO! PREDICT ENTIRE POLYMORPH SYSTEM!? POUND FUCKING SAND! GO DO IT YOURSELF!
public abstract partial class SharedRevertPolymorphEntityEffectSystem : EntityEffectSystem<MetaDataComponent, RevertPolymorph>
{
    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<RevertPolymorph> args)
    {
        RevertPolymorph(entity.Owner, args.Effect);
    }
    protected virtual void RevertPolymorph(EntityUid uid, RevertPolymorph effect) // check server
    {
    }
}
