using Content.Shared.Examine;
using Content.Shared.Mobs;

namespace Content.Server.Omu.Werewolf;

[RegisterComponent, Access(typeof(WerewolfDevouredSystem))]
public sealed partial class WerewolfDevouredComponent : Component
{}

public sealed class WerewolfDevouredSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WerewolfDevouredComponent, MobStateChangedEvent>(OnStateChanged);
        SubscribeLocalEvent<WerewolfDevouredComponent, ExaminedEvent>(OnExamine);
    }

    private void OnStateChanged(EntityUid uid, WerewolfDevouredComponent comp, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Critical || args.NewMobState != MobState.Dead || !TerminatingOrDeleted(uid))
        {
            RemComp<WerewolfDevouredComponent>(uid);
        }
    }

    private void OnExamine(EntityUid uid, WerewolfDevouredComponent comp, ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("WerewolfDevouredDesc"));
    }
}
