namespace Content.Shared._Omu.Heretic;

[RegisterComponent]
public sealed partial class MansusMobComponent : Component
{
    /// <summary>
    /// Maximum number of entities that can affected by the Mansus Screech action.
    /// </summary>
    [DataField]
    public int ScreechRange = 10;
}
