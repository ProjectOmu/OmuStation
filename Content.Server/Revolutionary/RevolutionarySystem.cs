using Content.Server.Actions;
using Content.Server.Polymorph.Systems;
using Content.Shared.Polymorph;
using Content.Shared.Revolutionary;
using Content.Shared.Revolutionary.Components;
using Content.Shared._Omu.Revs;
using Content.Server._Omu.Revs;
using Content.Server.Mind;


namespace Content.Server.Revolutionary;
 // funkystation start
public sealed class RevolutionarySystem : SharedRevolutionarySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!; // Goob

    [Dependency] private readonly MoraleHarmerAreaSystem _MoraleArea = default!; //Omu
    [Dependency] private readonly MindSystem _mindSystem = default!;    //Omu


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HeadRevolutionaryComponent, ComponentInit>(OnStartHeadRev);

        // Goob
        SubscribeLocalEvent<RevolutionaryComponent, PolymorphedEvent>(OnPolymorphed);
        SubscribeLocalEvent<HeadRevolutionaryComponent, PolymorphedEvent>(OnHeadPolymorphed);

        // Omu start
        SubscribeLocalEvent<HeadRevolutionaryComponent, BookConverterUsedEvent>(OnBookArea);
        SubscribeLocalEvent<HeadRevolutionaryComponent, BookConverterTargetUsedEvent>(OnBookDoAfter);
        SubscribeLocalEvent<RevolutionaryComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnPolymorphed(Entity<RevolutionaryComponent> ent, ref PolymorphedEvent args)
        => _polymorph.CopyPolymorphComponent<RevolutionaryComponent>(ent, args.NewEntity);

    private void OnHeadPolymorphed(Entity<HeadRevolutionaryComponent> ent, ref PolymorphedEvent args)
        => _polymorph.CopyPolymorphComponent<HeadRevolutionaryComponent>(ent, args.NewEntity);


    /// <summary>
    /// Add the starting ability(s) to the Head Rev.
    /// </summary>
    private void OnStartHeadRev(Entity<HeadRevolutionaryComponent> uid, ref ComponentInit args)
    {
        foreach (var actionId in uid.Comp.BaseHeadRevActions)
        {
            var actionEnt = _actions.AddAction(uid, actionId);
        }
    }
    // funkystation end
    // Omu start
    private void OnBookArea(Entity<HeadRevolutionaryComponent> ent, ref BookConverterUsedEvent args)
    {
        _MoraleArea.AreaChange(ent, args.Change, args.Range, args.Lang, args.Objective);
    }

    private void OnBookDoAfter(Entity<HeadRevolutionaryComponent> ent, ref BookConverterTargetUsedEvent args)
    {
        EnsureComp<MoraleComponent>(args.Target);
        var ev = new MoraleChangedArgs
        {
            Amount = args.Change,

            User = ent,

            Objective = args.Objective
        };
        RaiseLocalEvent(args.Target, ev);
    }
    private void OnShutdown(Entity<RevolutionaryComponent> uid, ref ComponentShutdown args)
    {
        if (uid.Comp.Objective is { } objective && _mindSystem.TryGetMind(uid, out var mindID, out var mindComp))
        {
            _mindSystem.TryRemoveObjective(mindID, mindComp, objective);
        }
    }
}

