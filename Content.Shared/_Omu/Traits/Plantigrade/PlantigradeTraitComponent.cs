using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Omu.Traits.Plantigrade;

[RegisterComponent, NetworkedComponent]
public sealed partial class PlantigradeTraitComponent : Component
{
    [DataField(required: true)]
    public Dictionary<HumanoidVisualLayers, ProtoId<HumanoidSpeciesSpriteLayer>> SpriteLayerMappings = new();

    [DataField]
    public List<string> InventorySlots = new() { "jumpsuit", "shoes", "outerClothing" };

    [DataField]
    public List<HumanoidVisualLayers> DisplacementLayers =
        new()
        {
            HumanoidVisualLayers.LLeg,
            HumanoidVisualLayers.RLeg,
            HumanoidVisualLayers.LFoot,
            HumanoidVisualLayers.RFoot,
            HumanoidVisualLayers.Chest,
        };
}
