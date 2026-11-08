using Content.Omu.Server._BSD.IngameServerSystem.Helpers;

namespace Content.Omu.Server._BSD.IngameServerSystem.Events;

[ByRefEvent]
public readonly record struct IngameServerProgrammExecutionEvent(IngameServerProgramTypes Type, float AllotedProcessing);