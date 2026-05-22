// Moved from Content.Server/Botany/SeedPrototype.cs to shared so the analyzer UI can reference it client-side.

namespace Content.Shared.Botany;

public enum HarvestType : byte
{
    NoRepeat,
    Repeat,
    SelfHarvest
}
