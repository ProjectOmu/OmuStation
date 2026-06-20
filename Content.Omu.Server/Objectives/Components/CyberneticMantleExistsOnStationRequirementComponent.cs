using Content.Omu.Server.Objectives.Systems;

namespace Content.Omu.Server.Objectives.Components;

/// <summary>
///     Requires that a cybernetic mantle is present on station.
/// </summary>
[RegisterComponent, Access(typeof(CyberneticMantleExistsOnStationRequirementSystem))]
public sealed partial class CyberneticMantleExistsOnStationRequirementComponent : Component
{
}