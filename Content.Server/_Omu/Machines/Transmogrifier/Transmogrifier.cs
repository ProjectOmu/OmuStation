using Content.Shared.ActionBlocker;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Content.Shared._Omu.Entities.Objects.Transmogrifier;
using System.Linq;
using Content.Shared.Coordinates;
using Robust.Shared.Random;

namespace Content.Server._Omu.Machines.Transmogrifier;

[RegisterComponent]
public sealed partial class TransmogrifierComponent : Component
{
    [DataField]
    public bool ControlsLocked = false;

    [DataField]
    public TransmogrifierMode State = TransmogrifierMode.OneToOne;

    /// <summary>
    /// The list of transmogrifier graphs - filled on map init and used to decide what we transform into.
    /// </summary>
    [DataField]
    public Dictionary<EntProtoId, ProtoId<TransmogrifierGraphPrototype>> TransformableItems = new();

    /// <summary>
    /// Defines prototype to spawn for bluespace effect
    /// </summary>
    [DataField("bluespaceEffectPrototype"), ViewVariables(VVAccess.ReadWrite)]
    public string BluespaceEffectPrototype { get; set; } = "EffectFlashBluespace";

    [DataField]
    public EntProtoId BackupGarbage = "RandomSpawner100";

}
public sealed partial class TransmogrifierSystem : EntitySystem
{

    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedInteractionSystem _interactionSystem = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {

        SubscribeLocalEvent<TransmogrifierComponent, GetVerbsEvent<Verb>>(OnVerb);
        SubscribeLocalEvent<TransmogrifierComponent, StorageAfterCloseEvent>(TransmogStart);
        SubscribeLocalEvent<TransmogrifierComponent, MapInitEvent>(OnInit);

        base.Initialize();
    }

    private void OnInit(Entity<TransmogrifierComponent> ent, ref MapInitEvent args)
    {
        var dict = ent.Comp.TransformableItems;
        foreach (var Proto in _proto.EnumeratePrototypes<TransmogrifierGraphPrototype>())
        {
            dict[Proto.TransformedItem] = Proto.ID; //Filling list with the items that are transformable
        }
    }

    private void OnVerb(Entity<TransmogrifierComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        // check if user can change sensor
        if (ent.Comp.ControlsLocked)
            return;

        // standard interaction checks
        if (!args.CanInteract || args.Hands == null)
            return;

        if (!_interactionSystem.InRangeUnobstructed(args.User, args.Target) && !_actionBlocker.CanInteract(args.User, null))
            return;

        args.Verbs.UnionWith(new[]
        {
            CreateVerb(ent, args.User, TransmogrifierMode.Coarse),
            CreateVerb(ent, args.User, TransmogrifierMode.Rough),
            CreateVerb(ent, args.User, TransmogrifierMode.OneToOne),
            CreateVerb(ent, args.User, TransmogrifierMode.Fine),
            CreateVerb(ent, args.User, TransmogrifierMode.VeryFine)
        });
    }
    private Verb CreateVerb(Entity<TransmogrifierComponent> ent, EntityUid userUid, TransmogrifierMode mode)
    {
        return new Verb()
        {
            Text = GetModeName(mode),
            Disabled = ent.Comp.State == mode,
            Priority = -(int) mode,
            Category = VerbCategory.SetState,
            Act = () => TrySetSensor(ent.AsNullable(), mode, userUid)
        };
    }
    public string GetModeName(TransmogrifierMode mode)
    {
        string name;
        switch (mode)
        {
            case TransmogrifierMode.Coarse:
                name = "transmog-coarse";
                break;
            case TransmogrifierMode.Rough:
                name = "transmog-rough";
                break;
            case TransmogrifierMode.OneToOne:
                name = "transmog-onetoone";
                break;
            case TransmogrifierMode.Fine:
                name = "transmog-fine";
                break;
            case TransmogrifierMode.VeryFine:
                name = "transmog-very-fine";
                break;
            default:
                return "";
        }

        return Loc.GetString(name);
    }
    public bool TrySetSensor(Entity<TransmogrifierComponent?> machine, TransmogrifierMode mode, EntityUid userUid)
    {
        if (!Resolve(machine, ref machine.Comp, false))
            return false;

        machine.Comp.State = mode;

        var msg = Loc.GetString("transmog-new-state", ("mode", GetModeName(mode)));
        _popup.PopupEntity(msg, machine);

        return true;
    }

    public void TransmogStart(EntityUid uid, TransmogrifierComponent comp, StorageAfterCloseEvent args)
    {
        if (!TryComp<EntityStorageComponent>(uid, out var storage) || storage.Contents.ContainedEntities.Count == 0)
            return;

        var containsHuman = false;

        foreach (var item in storage.Contents.ContainedEntities.ToArray())
        {
            if (HasComp<HumanoidAppearanceComponent>(item))
            {
                containsHuman = true;
                continue;
            }

            var metadata = MetaData(item);
            if (metadata.EntityPrototype is null)
                continue;

            if (!comp.TransformableItems.TryGetValue(metadata.EntityPrototype!, out var transmogID))
                continue;

            var graph = _proto.Index<TransmogrifierGraphPrototype>(transmogID);

            switch (comp.State)
            {
                case TransmogrifierMode.Coarse:
                    SpawnInContainerOrDrop(PickResult(graph.Coarse, comp), uid, storage.Contents.ID);
                    break;
                case TransmogrifierMode.Rough:
                    SpawnInContainerOrDrop(PickResult(graph.Rough, comp), uid, storage.Contents.ID);
                    break;
                case TransmogrifierMode.OneToOne:
                    SpawnInContainerOrDrop(PickResult(graph.OneToOne, comp), uid, storage.Contents.ID);
                    break;
                case TransmogrifierMode.Fine:
                    SpawnInContainerOrDrop(PickResult(graph.Fine, comp), uid, storage.Contents.ID);
                    break;
                case TransmogrifierMode.VeryFine:
                    SpawnInContainerOrDrop(PickResult(graph.VeryFine, comp), uid, storage.Contents.ID);
                    break;
            }

            //_containerSystem.Remove(item, storage.Contents);
            PredictedQueueDel(item);
        }


        Spawn(comp.BluespaceEffectPrototype, uid.ToCoordinates());

        if (containsHuman)
            _popup.PopupEntity(Loc.GetString("transmog-human-fail"), uid);
    }

    private EntProtoId? PickResult(TransmogrifierResult result, TransmogrifierComponent comp)
    {
        if (result.Weighted.Count == 0)
            return comp.BackupGarbage;

        var totalWeight = 0f;

        foreach (var entry in result.Weighted)
        {
            if (entry.Weight > 0)
                totalWeight += entry.Weight;
        }

        if (totalWeight <= 0)
            return result.Entity;

        var roll = _random.NextFloat() * totalWeight;

        foreach (var entry in result.Weighted)
        {
            if (entry.Weight <= 0)
                continue;

            roll -= entry.Weight;

            if (roll < 0)
                return entry.Entity;
        }

        return result.Weighted[^1].Entity;
    }
}

