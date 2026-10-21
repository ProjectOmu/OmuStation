namespace Content.Omu.Server.StationEvents.Components;

/// <summary>
/// This event announces a temporary power surge and then smashes many of the powered light sources on the station
/// </summary>
[RegisterComponent, Access(typeof(Events.LightsOutMajorRule))]
public sealed partial class LightsOutMajorRuleComponent : LightsOutRuleComponent
{
    public override float DamageProbability { get; set; }= 0.20f;
}
