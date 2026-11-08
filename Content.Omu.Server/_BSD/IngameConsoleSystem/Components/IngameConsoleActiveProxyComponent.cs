
namespace Content.Omu.Server._BSD.IngameConsoleSystem.Components;
/// <summary>
/// Signifies that this console is currently acting as a proxy and relaying the commands to another console.
/// </summary>
[RegisterComponent]
public sealed partial class IngameConsoleActiveProxyComponent : Component
{
    public EntityUid ProxyTarget;
    public string Channel;
}

[RegisterComponent]
public sealed partial class IngameConsoleActiveProxyTargetComponent : Component
{
    /// <summary>
    /// Used to display the netIDs of all consoles that are currently proxying into this device
    /// </summary>
    public HashSet<EntityUid> ProxyContollers = new();
}