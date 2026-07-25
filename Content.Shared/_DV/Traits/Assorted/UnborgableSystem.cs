using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Systems;
using Content.Shared.Examine;
using Content.Shared.Movement.Components; // TODO: use BrainComponent instead of InputMover if it gets moved to shared
using Content.Shared.Silicons.Borgs.Components;
using Robust.Shared.Utility;

//namespace Content.Shared._DV.Traits.Assorted;
namespace Content.Shared.Silicons.Borgs;

/// <summary>
/// Adds a warning examine message to brains with <see cref="UnborgableComponent"/>.
/// </summary>
public sealed class UnborgableSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly ILogManager _logMgr = default!;
    private ISawmill _sawmill = default!;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _logMgr.GetSawmill("unborg.system");
        _sawmill.Info("Initialized");

        SubscribeLocalEvent<UnborgableComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<UnborgableComponent, ExaminedEvent>(OnExamined);
    }

    private void OnMapInit(Entity<UnborgableComponent> ent, ref MapInitEvent args)
    {
        _sawmill.Info("MapInit");

        if (!TryComp<BodyComponent>(ent, out var body))
            return;

        var brains = _body.GetBodyOrganEntityComps<InputMoverComponent>((ent.Owner, body));
        foreach (var brain in brains)
        {
            EnsureComp<UnborgableComponent>(brain);
        }
    }

    private void OnExamined(Entity<UnborgableComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || HasComp<BodyComponent>(ent))
            return;

        args.PushMarkup(Loc.GetString("brain-cannot-be-borged-message"));
    }
}
