using Content.Shared.Eui;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Imp.ReadyManifest;


[Serializable, NetSerializable]
public sealed class RequestReadyManifestMessage : EntityEventArgs
{
    public RequestReadyManifestMessage() { }
}

[Serializable, NetSerializable]
public sealed class ReadyManifestEuiState : EuiStateBase
{
    public Dictionary<ProtoId<JobPrototype>, int> JobCounts { get; }

    public ReadyManifestEuiState(Dictionary<ProtoId<JobPrototype>, int> jobCounts)
    {
        JobCounts = jobCounts;
    }
}
