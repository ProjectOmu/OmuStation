using Content.Shared.Whitelist;

namespace Content.Shared.Mind.Filters;

/// <summary>
/// A mind filter that checks the mind's owned entity against a blacklist.
/// </summary>
public sealed partial class BodyMindBlacklistFilter : MindFilter
{
    [DataField(required: true)]
    public EntityWhitelist Blacklist = new();

    protected override bool ShouldRemove(Entity<MindComponent> ent, EntityUid? exclude, IEntityManager entMan, SharedMindSystem mindSys)
    {
        if (ent.Comp.OwnedEntity is not {} mob)
            return true;

        var sys = entMan.System<EntityWhitelistSystem>();
        return sys.IsBlacklistPassOrNull(Blacklist, mob);
    }
}
