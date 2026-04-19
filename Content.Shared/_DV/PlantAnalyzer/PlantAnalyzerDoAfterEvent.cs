using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._DV.PlantAnalyzer;

[Serializable, NetSerializable]
public sealed partial class PlantAnalyzerDoAfterEvent : SimpleDoAfterEvent;
