using Robust.Shared.Prototypes;

using Content.Omu.Server._BSD.IngameServerSystem.Helpers;

using Content.Omu.Server._BSD.MultiBlockSystem.Components;
using Content.Omu.Server._BSD.IngameServerSystem.Prototype;

namespace Content.Omu.Server._BSD.IngameServerSystem.Components;

[RegisterComponent]
//TODO split this class into a point manager and a server class, Thesia
//Also processing power is a form of point just saying
public sealed partial class IngameServerComponent : Component
{
    [DataField]
    public Dictionary<ProtoId<IngameServerPointPrototypePrototype>, int> StoredPoints = new();

    [DataField]
    public Dictionary<ProtoId<IngameServerPointPrototypePrototype>, int?> StoredPointsCapacity = new();

    [DataField]
    public float ProcessingPower = 0f;

    [DataField]
    public float AvailabeProcessingPower = 0f;

    [DataField]
    public HashSet<ProtoId<MultiStructTypePrototype>> ProcessingPowerProvidingTypes = new();

    [DataField]
    public Dictionary<IngameServerProgramTypes, IngameServerProgram> ActivePrograms = new();

    [DataField]
    public HashSet<float> InstalledPrograms = new();

    [DataField]
    public TimeSpan NextUpdate = new();
}