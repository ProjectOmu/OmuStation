using Robust.Shared.GameStates;

namespace Content._Omu.Shared.Cybernetics;

/// <summary>
/// Component for IPCs to make them take ION when emped
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class IPCEmpVulnerableComponent : Component
{
    /// <summary>
    ///     Is the IPC allready EMPed?
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Disabled = false;
}
