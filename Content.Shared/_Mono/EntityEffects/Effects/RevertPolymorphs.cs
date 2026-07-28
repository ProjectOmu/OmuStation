using Content.Shared.Polymorph.Components;
using Content.Shared.Polymorph.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.Polymorph;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._Mono.EntityEffects.Effects;

public sealed partial class RevertPolymorph : EventEntityEffect<RevertPolymorph>
{
    /// <summary>
    ///     What polymorph prototype is used on effect
    /// </summary>
    [DataField("prototype", customTypeSerializer:typeof(PrototypeIdSerializer<PolymorphPrototype>))]
    public string PolymorphPrototype { get; set; }

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)         //Omu Edit to make it work
    {
        var entProto = prototype.Index<PolymorphPrototype>(PolymorphPrototype).Configuration.Entity;
        if (entProto == null)
            return null;
        var ent = prototype.Index<EntityPrototype>(entProto.Value);
        return Loc.GetString("reagent-effect-guidebook-revert-polymorph",
            ("chance", Probability), ("entityname",
                prototype.Index<EntityPrototype>(ent).Name));
    }

}
