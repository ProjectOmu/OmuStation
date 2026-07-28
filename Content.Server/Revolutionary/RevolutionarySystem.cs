using Content.Server.Actions;
using Content.Server.Polymorph.Systems;
using Content.Shared.Polymorph;
using Content.Shared.Revolutionary;
using Content.Shared.Revolutionary.Components;


namespace Content.Server.Revolutionary;
 // funkystation start
public sealed class RevolutionarySystem : SharedRevolutionarySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!; // Goob


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HeadRevolutionaryComponent, ComponentInit>(OnStartHeadRev);

        // Goob
        SubscribeLocalEvent<RevolutionaryComponent, PolymorphedEvent>(OnPolymorphed);
        SubscribeLocalEvent<HeadRevolutionaryComponent, PolymorphedEvent>(OnHeadPolymorphed);
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
}
 // funkystation end
