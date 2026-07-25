using System.Linq;
using Content.Shared._Byrd.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Nutrition;

namespace Content.Shared._FarHorizons.Vampire;

public partial class SharedLesserVampireSystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;

    private void InitializeDrinking()
    {
        SubscribeLocalEvent<LesserVampireComponent, LesserVampireDrinkBloodDoAfterEvent>(DrinkDoAfter);
        SubscribeLocalEvent<LesserVampireComponent, LesserVampireBiteDoAfterEvent>(BiteDoAfter);
    }

    private void DrinkDoAfter(Entity<LesserVampireComponent> ent, ref LesserVampireDrinkBloodDoAfterEvent args)
    {
        if (args.Target == null || args.Cancelled)
            return;

        if (TryComp<SolutionContainerManagerComponent>(args.Target, out var soultionContainer) &&
            TryComp<VampireDrinkableComponent>(args.Target, out var drinkable))
        {
            DoDrink(ent, (args.Target.Value, soultionContainer, drinkable));
            args.Repeat = VampireCanDrinkBlood(ent) &&
                          CanDrinkFromContainer(ent, (args.Target.Value, soultionContainer, drinkable)) &&
                          GetBloodPool(ent) < ent.Comp.BloodPoolMax;
        }
    }

    private void BiteDoAfter(Entity<LesserVampireComponent> ent, ref LesserVampireBiteDoAfterEvent args)
    {
        if (args.Target == null || args.Cancelled)
            return;

        if (!TryComp<SolutionContainerManagerComponent>(args.Target, out var soultionContainer) ||
            !TryComp<VampireBiteableComponent>(args.Target, out var drinkable)) return;

        var ev = new OnVampireBite(ent, (args.Target.Value, drinkable));
        RaiseLocalEvent(ent, ref ev);

        DoDrink(ent, (args.Target.Value, soultionContainer, drinkable));
        args.Repeat = VampireCanDrinkBlood(ent) &&
                      VampireCanBite(ent, (args.Target.Value, drinkable)) &&
                      CanDrinkFromContainer(ent, (args.Target.Value, soultionContainer, drinkable)) &&
                      GetBloodPool(ent) < ent.Comp.BloodPoolMax;
    }

    private void DoDrink(Entity<LesserVampireComponent> ent,
        Entity<SolutionContainerManagerComponent, VampireConsumption> target)
    {
        if (!_solution.TryGetSolution((target.Owner, target.Comp1), target.Comp2.Container, out var solution) ||
            !TryComp<BodyComponent>(ent, out var body) ||
            !_body.TryGetBodyOrganEntityComps<StomachComponent>((ent, body), out var stomachs))
            return;

        Entity<StomachComponent>? stomach = stomachs.FirstOrDefault();

        if (stomach == null)
        {
            if (!TryComp<StomachComponent>(ent, out var bodyStomach))
                return;

            stomach = (ent, bodyStomach);
        }

        var split = _solution.SplitSolution(solution.Value, target.Comp2.Amount);
        var ingestEv = new IngestingEvent(target, split, false);
        RaiseLocalEvent(ent, ref ingestEv);

        _reaction.DoEntityReaction(ent, split, ReactionMethod.Ingestion);

        if (!_stomach.TryTransferSolution(stomach.Value.Owner, split, stomach.Value.Comp))
        {
            _solution.AddSolution(solution.Value, split);
            return;
        }

        _audio.PlayPredicted(ent.Comp.DrinkSound, ent, ent);
        EnsureComp<BloodSuckedComponent>(target);
    }

    public bool CanDrinkFromContainer(Entity<LesserVampireComponent> ent,
        Entity<SolutionContainerManagerComponent, VampireConsumption> target)
    {
        if (!_solution.TryGetSolution((target.Owner, target.Comp1), target.Comp2.Container, out var solution))
            return false;

        return solution.Value.Comp.Solution.Volume >= target.Comp2.Amount;
    }

    public bool VampireCanDrinkBlood(Entity<LesserVampireComponent> ent)
    {
        if (!_ingestion.HasMouthAvailable(ent.Owner, ent.Owner)) return false;

        var ev = new VampireDrinkCheck(ent);
        RaiseLocalEvent(ent, ref ev);

        return !ev.Cancelled;
    }

    public bool VampireCanBite(Entity<LesserVampireComponent> ent, Entity<VampireBiteableComponent?> target)
    {
        if (!Resolve(target, ref target.Comp)) return false;

        var ev = new VampireBiteCheck(ent, target!);
        RaiseLocalEvent(ent, ref ev);

        return !ev.Cancelled;
    }

}