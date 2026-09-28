using Robust.Shared.Prototypes;

namespace Content.Omu.Server.Entities.Heretic;

[RegisterComponent, Access(typeof(LodestoneSystem))]
public sealed partial class LodestoneComponent : Component
{
    [DataField]
    public ComponentRegistry? AddedComponents = new();

    [DataField]
    public List<string> ComponentsActuallyAdded = new();
}
