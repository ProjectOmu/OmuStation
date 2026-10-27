using Content.Goobstation.Server.Chaplain.Components;
using Content.Goobstation.Shared.Bible;
using Content.Omu.Shared.Entities.Heretic;
using Content.Omu.Shared.MansusMob;
using Content.Server.GameTicking;
using Content.Shared._Omu.Heretic;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Examine;
using Content.Shared.Mobs;

namespace Content.Omu.Server.Entities.Heretic;

public sealed class FascinationSystem : EntitySystem
{
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FascinationComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<FascinationComponent, FascinationChangedArgs>(OnChange);
        SubscribeLocalEvent<FascinationComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FascinationComponent, BibleSmiteUsed>(OnBibleInteract);
        SubscribeLocalEvent<FascinationComponent, MobStateChangedEvent>(OnMobStateChanged);
    }
    private void OnStartup(EntityUid uid, FascinationComponent component, ComponentStartup args)
    {
        if (TryComp<SeeHereticFixturesComponent>(uid, out var seefixtures))
        {
            component.Naturalsight = true;
            if (seefixtures.SeeShifts == true)
                component.NaturalHereticsight = true;
        }
    }

    private void OnExamined(Entity<FascinationComponent> ent, ref ExaminedEvent args)
    {
        var comp = ent.Comp;
        var value = (int) Math.Round(comp.FascinationValue);

        args.PushMarkup(Loc.GetString($"fascination-examine-{((value < 1 || value > 5) ? 5 : value)}"));
    }
    private void OnChange(Entity<FascinationComponent> ent, ref FascinationChangedArgs args)
    {
        if (HasComp<MansusMobComponent>(ent)) // Ignore fascination tracking
        {
            return;
        }

        ent.Comp.FascinationValue = args.Amount + ent.Comp.FascinationValue; //increment the fascination value by the amount of knowledge gained!

        Dirty(ent, ent.Comp);

        float fascvalue = ent.Comp.FascinationValue;

        if (fascvalue < 5)
        {
            RemComp<MansusMaddenedComponent>(ent);
        }
        if (fascvalue <= 0)
        {
            if (TryComp<ActionsContainerComponent>(ent, out var actioncomp))
                foreach (var actionuid in actioncomp.Container.ContainedEntities)
                    if (TryComp<FascinationGrantedComponent>(actionuid, out _))
                        _actionContainer.RemoveAction(actionuid);           //Incentive to be just a little mad - you get the actions!

            RemComp<FascinationComponent>(ent);
        }
        if (fascvalue >= 5 && args.Amount > 0)
        {
            EnsureComp<MansusMaddenedComponent>(ent);

            if (args.SpawnMaiden)
            {
                _gameTicker.StartGameRule("MirrorMaidenSpawn", out var spawnEnt);
                var ev = new MaidenSpawnEvent();
                ev.SpawnedEntity = spawnEnt;
                ev.TrackedEntity = ent.Owner;
                RaiseLocalEvent(ev);
            }
        }
    }
    private void OnBibleInteract(Entity<FascinationComponent> ent, ref BibleSmiteUsed args)
    {
        var ev = new FascinationChangedArgs();
        ev.Amount = -1f;
        RaiseLocalEvent(ent, ev);
    }

    private void OnMobStateChanged(Entity<FascinationComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
        {
            RemComp<MansusMaddenedComponent>(ent);
            RemComp<FascinationComponent>(ent);
            RemComp<SeeHereticFixturesComponent>(ent);
        }
    }
}
