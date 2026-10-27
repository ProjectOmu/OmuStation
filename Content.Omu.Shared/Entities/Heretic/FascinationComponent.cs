using Robust.Shared.Prototypes;
using Content.Shared.StatusIcon;
using Content.Shared.NPC.Prototypes;
using Robust.Shared.GameStates;

namespace Content.Omu.Shared.Entities.Heretic;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FascinationComponent : Component
{
    public LocId MadnessMessage = "fascination-gain";

    public LocId SanityMessage = "fascination-loss";

    [DataField, AutoNetworkedField]
    public float FascinationValue;

    [DataField]
    public ProtoId<FactionIconPrototype> IconToAdd = "MadnessFaction";
    [DataField]
    public ProtoId<NpcFactionPrototype> FactionToAdd = "Madness";

    [ViewVariables(VVAccess.ReadOnly)]
    public bool Naturalsight;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool NaturalHereticsight;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool AlteredVision;

    [ViewVariables(VVAccess.ReadOnly)]
    public bool AlteredFaction;

}
public sealed class FascinationChangedArgs : EntityEventArgs
{
    public float Amount;
    public bool SpawnMaiden = true;
}
