using System.Linq;
using Content.Shared.Heretic;

namespace Content.Server._Omu.Heretic.Systems;

public sealed class ListSacrificeTargetsSystem : EntitySystem
{
    public IEnumerable<SacrificeTargetData> GetHereticSacrificeTargets(EntityUid heretic)
    {
        if (!TryComp<HereticComponent>(heretic, out var hereticComp))
        {
            return Enumerable.Empty<SacrificeTargetData>();
        }

        return hereticComp.SacrificeTargets;
    }

    public string GetHereticTargetName(NetEntity targetnet)
    {
        var target = GetEntity(targetnet);
        if (!TryComp<MetaDataComponent>(target, out var meta))
        {
            return "Unknown";
        }

        return meta.EntityName;
    }

    public bool IsHeretic(EntityUid entity)
    {
        return HasComp<HereticComponent>(entity);
    }
}
