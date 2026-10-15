namespace Content.Shared._Omu.Heretic;

[RegisterComponent]
public sealed partial class MansusMobComponent : Component
{
    /// <summary>
    /// Maximum number of entities that can affected by the Mansus Screech action.
    /// </summary>
    [DataField]
    public int ScreechRange = 10;

    /// <summary>
    /// Amount of fascination that the Mansus Screech action applies to entities within range.
    /// </summary>
    [DataField]
    public int ScreechFascination = 5;
}
