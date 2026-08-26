using Content.Server.Polymorph.Components;
using Content.Server.Polymorph.Systems;
using Content.Shared._Mono.EntityEffects;

namespace Content.Server._Mono.EntityEffects.EffectsThatNeedToBeMigrated;

public sealed partial class RevertPolymorphEntityEffectSystem : SharedRevertPolymorphEntityEffectSystem
{
    [Dependency] private readonly PolymorphSystem _polymorph = default!;

    protected override void RevertPolymorph(EntityUid uid, RevertPolymorph effect)
    {
        if (!HasComp<PolymorphedEntityComponent>(uid))
            return;
        EnsureComp<PolymorphableComponent>(uid); // This SEEMS bad. Why the fuck is a polymorphed entity not polymorphable already.
        _polymorph.Revert(uid);
    }
}
