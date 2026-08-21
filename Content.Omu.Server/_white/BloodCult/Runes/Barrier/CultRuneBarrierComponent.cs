using Robust.Shared.Prototypes;

namespace Content.Omu.Server.WhiteDream.BloodCult.Runes.Barrier;

[RegisterComponent]
public sealed partial class CultRuneBarrierComponent : Component
{
    [DataField]
    public EntProtoId SpawnPrototype = "BloodCultBarrier";
}
