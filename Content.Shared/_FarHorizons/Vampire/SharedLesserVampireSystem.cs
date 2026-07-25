using System.Linq;
using Content.Shared._FarHorizons.Vampire.Traits;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.HealthExaminable;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._FarHorizons.Vampire;

public abstract partial class SharedLesserVampireSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly IPrototypeManager ProtoMan = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly StomachSystem _stomach = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly ReactiveSystem _reaction = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IngestionSystem _ingestion = default!;
    [Dependency] private readonly HungerSystem _hunger = default!;
    [Dependency] private readonly ThirstSystem _thirst = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;

    public override void Initialize()
    {
        base.Initialize();

        InitializeVerbs();
        InitializeDrinking();

        SubscribeLocalEvent<LesserVampireComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<LesserVampireComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange ||
            !IsFangsVisible(ent) ||
            !_ingestion.HasMouthAvailable(ent, ent))
            return;

        args.PushMarkup(Loc.GetString("lesser-vampire-fangs-examine", ("vampire", Identity.Entity(ent, EntityManager))));
    }

    public bool IsFangsVisible(Entity<LesserVampireComponent> ent)
    {
        var ev = new VampireFangsCheck();
        RaiseLocalEvent(ent, ref ev);

        return !ev.FangsHidden;
    }

    public List<ProtoId<LesserVampireTraitPrototype>> ValidatedTraits(List<ProtoId<LesserVampireTraitPrototype>> traits)
    {
        var result = new List<ProtoId<LesserVampireTraitPrototype>>();
        var points = 0;

        foreach (var t in traits)
        {
            var proto = ProtoMan.Index(t);
            if (proto.Incompatible.Intersect(result).Any()) continue;

            points -= proto.Cost;
            result.Add(t);
        }

        return points >= 0 ? result : [];
    }
}