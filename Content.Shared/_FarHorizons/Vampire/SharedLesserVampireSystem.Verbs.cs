using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared._FarHorizons.Vampire;

public partial class SharedLesserVampireSystem
{
    private void InitializeVerbs()
    {
        SubscribeLocalEvent<VampireDrinkableComponent, GetVerbsEvent<AlternativeVerb>>(OnBloodpackVerb);
        SubscribeLocalEvent<VampireBiteableComponent, GetVerbsEvent<AlternativeVerb>>(OnBiteVerb);
    }

    private void OnBloodpackVerb(Entity<VampireDrinkableComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanComplexInteract ||
            !_interaction.InRangeAndAccessible(args.User, args.Target) ||
            !TryComp<LesserVampireComponent>(args.User, out var vampire) ||
            !TryComp<SolutionContainerManagerComponent>(ent, out var solutionContainer) ||
            !VampireCanDrinkBlood((args.User, vampire)) ||
            !CanDrinkFromContainer((args.User, vampire), (ent, solutionContainer, ent.Comp)))
            return;

        Entity<LesserVampireComponent> vampireEnt = (args.User, vampire);

        AlternativeVerb drink = new()
        {
            Act = () => DrinkFromContainer(vampireEnt, (ent, solutionContainer, ent.Comp)),
            Text = Loc.GetString("lesser-vampire-drink-bloodpack"),
            Priority = 1
        };

        args.Verbs.Add(drink);
    }

    private void OnBiteVerb(Entity<VampireBiteableComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanComplexInteract ||
            !_interaction.InRangeAndAccessible(args.User, args.Target) ||
            args.User == args.Target ||
            !TryComp<LesserVampireComponent>(args.User, out var vampire) ||
            !TryComp<SolutionContainerManagerComponent>(ent, out var solutionContainer) ||
            !VampireCanDrinkBlood((args.User, vampire)) ||
            !VampireCanBite((args.User, vampire), args.Target) ||
            !CanDrinkFromContainer((args.User, vampire), (ent, solutionContainer, ent.Comp)))
            return;

        Entity<LesserVampireComponent> vampireEnt = (args.User, vampire);

        AlternativeVerb drink = new()
        {
            Act = () => BiteTarget(vampireEnt, (ent, solutionContainer, ent.Comp)),
            Text = Loc.GetString("lesser-vampire-drink-bite"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Nyanotrasen/Icons/verbiconfangs.png")),
            Priority = 3
        };

        args.Verbs.Add(drink);
    }

    public void DrinkFromContainer(Entity<LesserVampireComponent> ent,
        Entity<SolutionContainerManagerComponent, VampireDrinkableComponent> target)
    {
        var doAfterEventArgs =
            new DoAfterArgs(EntityManager, ent, target.Comp2.Duration, new LesserVampireDrinkBloodDoAfterEvent(),
                eventTarget: ent, target: target)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                MovementThreshold = 0.01f,
                DistanceThreshold = 1.0f,
                NeedHand = false
            };

        _doAfter.TryStartDoAfter(doAfterEventArgs);
    }

    public void BiteTarget(Entity<LesserVampireComponent> ent,
        Entity<SolutionContainerManagerComponent, VampireBiteableComponent> target)
    {
        _popup.PopupPredicted(Loc.GetString("lesser-vampire-bite-warning", ("vampire", Identity.Entity(ent, EntityManager)), ("target", Identity.Entity(target, EntityManager))), ent, ent, PopupType.MediumCaution);

        var doAfterEventArgs =
            new DoAfterArgs(EntityManager, ent, target.Comp2.Duration, new LesserVampireBiteDoAfterEvent(),
                eventTarget: ent, target: target)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                MovementThreshold = 0.01f,
                DistanceThreshold = 1.0f,
                NeedHand = false
            };

        _doAfter.TryStartDoAfter(doAfterEventArgs);
    }
}