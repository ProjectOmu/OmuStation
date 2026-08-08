using Content.Shared.Follower;

namespace Content.Omu.Server.Entities.Heretic;

public sealed class LodestoneSystem : EntitySystem
{
    [Dependency] private readonly FollowerSystem _follow = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LodestoneComponent, StartedFollowingEntityEvent>(OnStartFollowing);
        SubscribeLocalEvent<LodestoneComponent, StoppedFollowingEntityEvent>(OnStopFollowing);
    }

    public void OnStartFollowing(Entity<LodestoneComponent> ent, ref StartedFollowingEntityEvent args)
    {}

    public void OnStopFollowing(Entity<LodestoneComponent> ent, ref StoppedFollowingEntityEvent args)
    {}
}
