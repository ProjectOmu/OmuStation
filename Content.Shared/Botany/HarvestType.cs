// moved to shared so client and server code can both reference it.

namespace Content.Shared.Botany;

public enum HarvestType : byte
{
    NoRepeat,
    Repeat,
    SelfHarvest
}
