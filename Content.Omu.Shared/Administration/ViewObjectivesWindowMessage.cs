using Content.Shared.Mind;
using Content.Shared.Objectives;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Omu.Shared.Administration;

[Serializable, NetSerializable]
public sealed class ViewObjectivesWindowMessage(NetEntity target, string characterName, ProtoId<RoleTypePrototype> roleType, LocId? subType, Dictionary<string, List<ObjectiveInfo>> objectives) : EntityEventArgs
{
    public NetEntity Target => target;
    public string CharacterName => characterName;
    public ProtoId<RoleTypePrototype> RoleType => roleType;
    public LocId? RoleTypeSubType => subType;
    public Dictionary<string, List<ObjectiveInfo>> Objectives => objectives;
}
