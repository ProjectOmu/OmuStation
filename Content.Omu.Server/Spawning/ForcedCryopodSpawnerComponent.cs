using Content.Server.Spawners.Components;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Omu.Server.Spawning;

[RegisterComponent]
public sealed partial class ForcedCryosleepSpawnerComponent : Component
{
    /// <summary>
    /// The ID of the container that this entity will spawn players into
    /// </summary>
    [DataField(required: true)]
    public string ContainerId = string.Empty;

    /// <summary>
    /// Job specifier
    /// </summary>
    [DataField(required:true)]
    public ProtoId<JobPrototype>? Job;

    /// <summary>
    /// Force the spawn? Will override player spawn preferences.
    ///
    /// This system exists for this purpose, You should only set this to false for some really specific reasons.
    /// </summary>
    [DataField]
    public bool Forced = true;

    /// <summary>
    /// The type of spawn points to handle.
    /// </summary>
    [DataField(required:true)]
    public List<SpawnPointType> SpawnTypes;

}
