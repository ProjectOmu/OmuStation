using Content.Omu.Server._BSD.IngameServerSystem.Helpers;

namespace Content.Omu.Server._BSD.IngameConsoleSystem.IngameProgramSystem.Components;

[RegisterComponent]
public sealed partial class IngamePointConversionProgramComponent : Component
{
    /// <summary>
    /// Includes all datatypes that automaticly get converted, more can be added later via ingame commands
    /// </summary>
    [DataField]
    public HashSet<string> EnabeledConversions =
    new HashSet<string> { "SciRawData", "SigSciRawTelemetry" };
}