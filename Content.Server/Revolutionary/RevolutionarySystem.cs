using Content.Server.Actions;
using Content.Server.Polymorph.Systems;
using Content.Shared.Polymorph;
using Content.Shared.Revolutionary;
using Content.Shared.Revolutionary.Components;
using Content.Shared._Omu.Revs;
using Content.Server._Omu.Revs;


namespace Content.Server.Revolutionary;
 // funkystation start
public sealed class RevolutionarySystem : SharedRevolutionarySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!; // Goob

    [Dependency] private readonly MoraleHarmerAreaSystem _MoraleArea = default!; //Omu


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HeadRevolutionaryComponent, ComponentInit>(OnStartHeadRev);

        // Goob
        SubscribeLocalEvent<RevolutionaryComponent, PolymorphedEvent>(OnPolymorphed);
        SubscribeLocalEvent<HeadRevolutionaryComponent, PolymorphedEvent>(OnHeadPolymorphed);

        // Omu start
        SubscribeLocalEvent<HeadRevolutionaryComponent, BookConverterUsedEvent>(OnBookArea);
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
        _MoraleArea.AreaChange(ent, args.Change, args.range);
    }
}

