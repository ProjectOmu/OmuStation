namespace Content.Omu.Server._BSD.IngameServerSystem.Helpers;

public struct IngameServerPointConversions
{
    public IngameServerPointConversions()
    {
        PointAToPointB = new();
        ConversionRate = new();
        //PointAToPointB[IngameServerPoints.SciRawData] = IngameServerPoints.SciGeneralPoint; 
        //ConversionRate[IngameServerPoints.SciRawData] = 1.0f;//left here cause I wanted to but redoing all of wizdens RND code is something I rather do later :3
        PointAToPointB["SigSciRawTelemetry"] = "SigSciSignificantData";
        ConversionRate["SigSciRawTelemetry"] = 0.1f;
        PointAToPointB["SigSciSignificantData"] = "SciGeneralPoint";
        ConversionRate["SigSciSignificantData"] = 5.0f;
    }
    public Dictionary<string, string> PointAToPointB { get; init; }

    public Dictionary<string, float> ConversionRate { get; init; }
}

public enum QuerryTypes
{
    local,
    networkTotal,
    networkUpwards,
    networkDownwards
}