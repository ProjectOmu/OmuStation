using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Omu.Shared.PlantAnalyzer;

[Serializable, NetSerializable]
public sealed partial class PlantAnalyzerDoAfterEvent : SimpleDoAfterEvent;
