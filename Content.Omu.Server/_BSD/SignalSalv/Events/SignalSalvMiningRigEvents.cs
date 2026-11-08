using Content.Shared.Materials;
using Robust.Shared.Prototypes;

namespace Content.Omu.Server._BSD.SignalSalv.Events;

[ByRefEvent]
public readonly record struct SignalSalvMiningRigProductionChangeEvent(Dictionary<ProtoId<MaterialPrototype>, int> OldProductionRate, Dictionary<ProtoId<MaterialPrototype>, int> NewProductionRate);