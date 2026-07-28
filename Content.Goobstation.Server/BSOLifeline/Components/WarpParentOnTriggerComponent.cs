namespace Content.Goobstation.Server.BSOLifeline.Components;

/// <summary>
/// This is used for...
/// </summary>
[RegisterComponent]
public sealed partial class WarpParentOnTriggerComponent : Component
{
    [DataField] public string WarpLocation { get; set; } = "CentComm";

}
