using Content.Shared._White.RadialSelector;
using Robust.Shared.GameStates;

namespace Content.Shared._EinsteinEngines.ShortConstruction;

[RegisterComponent, NetworkedComponent]
public sealed partial class ShortConstructionComponent : Component
{
    [DataField(required: true)]
    public List<RadialSelectorEntry> Entries = new();
}
