using Robust.Shared.Prototypes;

namespace Content.Omu.Common.Nutrition.Components;

[RegisterComponent]
public sealed partial class EmoteOnSliceComponent : Component
{
    [DataField("emoteId")]
    public string EmoteId = "Crying";
}
