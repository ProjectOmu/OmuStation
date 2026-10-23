using Content.Shared._EinsteinEngines.Language;
using Content.Shared._Goobstation.Wizard;
using Content.Shared.Actions;
using Content.Shared.Body.Systems;
using Content.Shared.Chat;
using Content.Shared.Implants;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._Omu.Entities.Science.Meatmancer;

[RegisterComponent, Access(typeof(MeatmancerSystem))]
public sealed partial class MeatmancerComponent : Component
{
    [DataField]
    public EntProtoId ActionToAdd = "ActionMeatmancerTouch";

    [DataField]
    public EntityUid? ActionAdded;
}

public sealed partial class MeatmancerSystem : EntitySystem
{
    [Dependency] private readonly SharedChatSystem _chat = default!;
    [Dependency] private readonly SharedActionsSystem _actionSystem = default!;
    [Dependency] private readonly SharedSpellsSystem _spellsSystem = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedBodySystem _bodySystem = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<MeatmancerComponent, ImplantImplantedEvent>(OnImplantImplanted);
        SubscribeLocalEvent<MeatmancerComponent, ImplantRemovedEvent>(OnImplantRemoved);
        SubscribeLocalEvent<MeatmancerTouchEvent>(OnMeatmancerTouch);
        base.Initialize();
    }

    private void OnImplantImplanted(EntityUid uid, MeatmancerComponent component, ImplantImplantedEvent args)
    {
        component.ActionAdded = _actionSystem.AddAction(args.Implanted, component.ActionToAdd);
    }
    private void OnImplantRemoved(Entity<MeatmancerComponent> ent, ref ImplantRemovedEvent args)
    {
        if (ent.Comp.ActionAdded is not null)
            _actionSystem.RemoveAction(ent.Comp.ActionAdded);
    }

    private void OnMeatmancerTouch(MeatmancerTouchEvent args)
    {
        if (args.Handled)
            return;

        if (_spellsSystem.IsTouchSpellDenied(args.Target))
        {
            args.Handled = true;
            return;
        }

        if (!_mobState.IsDead(args.Target) || HasComp<MindShieldComponent>(args.Target))
        {
            return;
        }
        else
        {
            var transform = Transform(args.Target);
            for (var i = 0; i < args.NumberSpawned; i++)
            {
                PredictedSpawnAtPosition(args.EntToSpawn, transform.Coordinates);
            }
            _bodySystem.GibBody(args.Target);

            if (!_protoManager.TryIndex(args.LanguagePrototype, out var language))
                return;

            _chat.TrySendInGameICMessage(args.Performer, Loc.GetString(_random.Pick(args.ActionMessages)), InGameICChatType.Speak, true, languageOverride: language);
        }
    }
}

public sealed partial class MeatmancerTouchEvent : EntityTargetActionEvent
{
    [DataField]
    public List<LocId> ActionMessages = new()
    {
        "meatmancer-1",
        "meatmancer-2",
        "meatmancer-3",
        "meatmancer-4"
    };

    [DataField]
    public EntProtoId EntToSpawn = "SalvageFleshSpawner";

    [DataField]
    public int NumberSpawned = 3;

    [DataField]
    public ProtoId<LanguagePrototype> LanguagePrototype = "Eldritch";
}
