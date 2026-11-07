namespace Content.Omu.Server._BSD.SignalSalv.Components;

[RegisterComponent]
public sealed partial class SignalSalvOutpostDataComponent : Component
{
    /// <summary>
    /// Amount of data provided to the mining rig
    /// </summary>
    [DataField]
    public float OutpostData = 2.0f;

    /// <summary>
    /// Amount of data provided to the mining rig
    /// </summary>
    [DataField]
    public bool OutpostDataRandom = false;

    /// <summary>
    /// Amount of data provided to the mining rig min
    /// </summary>
    [DataField]
    public float OutpostDataMin = 1.0f;

    /// <summary>
    /// Amount of data provided to the mining rig max
    /// </summary>
    [DataField]
    public float OutpostDataMax = 3.0f;

    /// <summary>
    /// Linked planet map to prevent it from beeing on the wrong planet
    /// </summary>
    [DataField]
    public EntityUid LinkedPlanetMap;

}
