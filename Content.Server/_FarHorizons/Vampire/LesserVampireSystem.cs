using Content.Goobstation.Shared.Religion;
using Content.Server.Actions;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared._FarHorizons.Vampire;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Reaction;
using Robust.Server.GameObjects;

namespace Content.Server._FarHorizons.Vampire;

public sealed partial class LesserVampireSystem : SharedLesserVampireSystem
{
    [Dependency] private readonly IComponentFactory _compFactory = default!;
    [Dependency] private readonly ILogManager _log = default!;
    [Dependency] private readonly MetabolizerSystem _metabolism = default!;
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly BodySystem _body = default!;

    public override void Initialize()
    {
        base.Initialize();
        InitializeTraits();

        SubscribeLocalEvent<LesserVampireComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BodyComponent, MakeVampireOrganEvent>(OnConvertOrgan);
    }

    private void OnMapInit(Entity<LesserVampireComponent> ent, ref MapInitEvent args) =>
        MakeVampire(ent.AsNullable());

    private void OnConvertOrgan(Entity<BodyComponent> ent, ref MakeVampireOrganEvent args)
    {
        if (!_body.TryGetBodyOrganEntityComps<StomachComponent>((ent, ent), out var stomachs))
            return;

        foreach (var stomachOrgan in stomachs)
        {
            if (!TryComp<MetabolizerComponent>(stomachOrgan, out var stomach))
                continue;

            if (stomach.MetabolizerTypes?.Add(args.Metabolizer) == true)
                args.StomachHandled = true;
        }

        Dirty(ent);
    }

    public void MakeVampire(Entity<LesserVampireComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false)) return;

        EnsureComp<WeakToHolyComponent>(ent);

        var reactive = EnsureComp<ReactiveComponent>(ent);
        reactive.ReactiveGroups ??= new();

        if (!reactive.ReactiveGroups.ContainsKey("Unholy"))
            reactive.ReactiveGroups.Add("Unholy", [ReactionMethod.Touch]);

        if (ent.Comp.AllowTraitSelection)
            _actions.AddAction(ent, ent.Comp.TraitsAction);

        var ev = new MakeVampireOrganEvent(ent.Comp.Metabolizer);
        RaiseLocalEvent(ent, ref ev);

        if (ev.StomachHandled) return; // Slime moment

        if (!TryComp<MetabolizerComponent>(ent, out var bodyMetabolizer)) return;

        bodyMetabolizer.MetabolizerTypes = [ent.Comp.Metabolizer];
        Dirty(ent);
    }

    public override void SetBloodPool(Entity<LesserVampireComponent> ent, float value)
    {
        ent.Comp.BloodPoolLastValue = Math.Clamp(value, 0, ent.Comp.BloodPoolMax);
        ent.Comp.BloodPoolLastUpdated = Timing.CurTime;
        Dirty(ent);
    }

    public override void RefreshBloodPoolChange(Entity<LesserVampireComponent> ent)
    {
        var ev = new GetVampireBloodPoolChange();
        RaiseLocalEvent(ent, ref ev);

        ent.Comp.BloodPoolLastValue = GetBloodPool(ent);
        ent.Comp.BloodPoolLastUpdated = Timing.CurTime;
        ent.Comp.BloodPoolChange = ev.Change;
        Dirty(ent);
    }
}
