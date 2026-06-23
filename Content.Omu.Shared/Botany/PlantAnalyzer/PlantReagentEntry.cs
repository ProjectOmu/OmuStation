using Robust.Shared.Serialization;

namespace Content.Omu.Shared.Botany.PlantAnalyzer;

[Serializable, NetSerializable]
public sealed class PlantReagentEntry
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string ColorHex { get; set; }
    public float Amount { get; set; }
    public string? DisplayAmount { get; set; }

    public PlantReagentEntry()
    {
        Id = string.Empty;
        Name = string.Empty;
        ColorHex = "#FFFFFF";
        Amount = 0f;
        DisplayAmount = null;
    }

    public PlantReagentEntry(string id, string name, string colorHex, float amount = 0f)
    {
        Id = id;
        Name = name;
        ColorHex = colorHex;
        Amount = amount;
        DisplayAmount = null;
    }
}
