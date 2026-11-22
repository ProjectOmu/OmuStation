namespace Content.Omu.Server.StationEvents.Components;

/// <summary>
/// This event announces a temporary power surge and then smashes some powered lights around one station beacon
/// </summary>
[RegisterComponent, Access(typeof(Events.LightsOutMinorRule))]
public sealed partial class LightsOutMinorRuleComponent : LightsOutRuleComponent
{
    public override float DamageProbability { get; set; } = 0.60f;

    /// <summary>
    /// All the powered lights on station; they get flickered to make the major and minor versions of this event initially indistinguishable
    /// </summary>
    public List<EntityUid> AllLights = [];
}
