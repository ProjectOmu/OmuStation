using Content.Shared.Mind;
using Content.Shared.Objectives;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Omu.Shared.Administration;

[Serializable, NetSerializable]
public sealed class ViewObjectivesWindowMessage(
    NetEntity target,
    string characterName,
    ProtoId<RoleTypePrototype> roleType,
    LocId? subType,
    Dictionary<string, List<ObjectiveInfo>> objectives,
    List<ViewObjectivesWindowTarget> targets) : EntityEventArgs
{
    public NetEntity Target => target;
    public string CharacterName => characterName;
    public ProtoId<RoleTypePrototype> RoleType => roleType;
    public LocId? RoleTypeSubType => subType;
    public Dictionary<string, List<ObjectiveInfo>> Objectives => objectives;
    public List<ViewObjectivesWindowTarget> Targets => targets;
}


[Serializable, NetSerializable]
public sealed class ViewObjectivesWindowTarget(NetEntity target, string targetName, ProtoId<JobPrototype> job)
{
    public NetEntity Target => target;
    public string TargetName => targetName;
    public ProtoId<JobPrototype> Job => job;
}
