using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Omu.Shared.StationEvents.SecretDoublePlus;

[Prototype]
public sealed partial class AmbientAntagChaosRatesPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public Dictionary<string, PresenceChaosEntry> Presence = new();

    [DataField]
    public Dictionary<string, ObjectiveChaosEntry> Objectives = new();
}

[DataDefinition]
public sealed partial class PresenceChaosEntry
{
    [DataField]
    public List<ProtoId<AntagPrototype>> Antags = new();

    [IncludeDataField]
    public AmbientChaos Rates = new();
}

[DataDefinition]
public sealed partial class ObjectiveChaosEntry
{
    [DataField]
    public List<string> Components = new();

    [IncludeDataField]
    public AmbientChaos Rates = new();
}

[DataDefinition]
public sealed partial class AmbientChaos
{
    [DataField]
    public float Chaos = 0;

    [DataField]
    public TimeSpan ExpectedTime = TimeSpan.Zero;

    [DataField]
    public TimeSpan DecayPeriod = TimeSpan.Zero;
}
