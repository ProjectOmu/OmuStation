namespace Content.Omu.Server._BSD.SignalSalv.Components;

[RegisterComponent]
public sealed partial class SignalSalvMiningRigStructreComponent : Component
{
    /// <summary>
    /// Value modifies how much faster the mining rig extracts materials from the planet, scales with productivity
    /// Math: MiningRateModifier = 1 + Productivity + OutpostData + GroundSurveyData
    /// </summary>
    [DataField]
    public float MiningRateModifier = 1;

    /// <summary>
    /// productivity needed for scaling base -> Mining Rate modifier = log(base = ProductivityScalingBase, value = SUM(ProductivityTypes.Values))
    /// </summary>
    [DataField]
    public int ProductivityScalingBase = 1;

    /// <summary>
    /// Components that boost the Mining Rigs Material output
    /// </summary>
    [DataField]
    public string[] ProductivityTypes = { "Productivity" };

    /// <summary>
    /// Scaling gained from inserted outpost data
    /// </summary>
    [DataField]
    public int ProductivityPoints = 0;

    /// <summary>
    /// Scaling gained from inserted outpost data
    /// </summary>
    [DataField]
    public float OutpostData = 0;

    /// <summary>
    /// Scaling gained from Ground Surveys data //WIP not sure if it will be in inicial release aka strech goal
    /// </summary>
    [DataField]
    public float GroundSurveyData = 0;
}
