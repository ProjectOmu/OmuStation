namespace Content.Omu.Server._BSD.SignalSilo.Components;

[RegisterComponent]
public sealed partial class SignalSiloCargoManagmentComponent : Component
{

}

/// <summary>
/// This component is attathed to the locla material router in every department --> entierly exists for the ease of reading out data for cargo.... could be done better probably
/// </summary>
[RegisterComponent]
public sealed partial class SignalSiloDepartmentalMaterialManagmentComponent : Component
{
    /// <summary>
    /// Warning this is not unique several departments could share a name -> distinguish via the nID on user side
    /// </summary>
    [DataField]
    public string DepartmentName = "ERROR";
}