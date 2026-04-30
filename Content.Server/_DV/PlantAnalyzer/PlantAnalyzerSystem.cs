using System.Linq;
using Content.Shared._DV.PlantAnalyzer;
using Content.Shared._DV.PlantAnalyzer.Components;
using Content.Server.Botany;
using Content.Server.Botany.Components;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._DV.PlantAnalyzer;

public sealed class PlantAnalyzerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlantAnalyzerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<PlantAnalyzerComponent, PlantAnalyzerDoAfterEvent>(OnDoAfter);
    }

    private void OnAfterInteract(Entity<PlantAnalyzerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Target is not { } target || !args.CanReach)
            return;

        if (!IsScannable(target))
            return;

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, ent.Comp.ScanDelay, new PlantAnalyzerDoAfterEvent(), ent, target: target, used: ent)
        {
            NeedHand = true,
            BreakOnDamage = true,
            BreakOnMove = true,
            MovementThreshold = 0.01f
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private bool IsScannable(EntityUid target)
    {
        if (HasComp<SeedComponent>(target))
            return true;

        return TryComp<PlantHolderComponent>(target, out var holder) && holder.Seed != null;
    }

    private void OnDoAfter(Entity<PlantAnalyzerComponent> ent, ref PlantAnalyzerDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target)
            return;

        var state = BuildScanState(target);
        if (state == null)
            return;

        _ui.SetUiState(ent.Owner, PlantAnalyzerUiKey.Key, state);
        _ui.TryOpenUi(ent.Owner, PlantAnalyzerUiKey.Key, args.User);
        _audio.PlayPvs(ent.Comp.ScanningEndSound, ent);

        args.Handled = true;
    }

    private PlantAnalyzerScannedSeedMessage? BuildScanState(EntityUid target)
    {
        SeedData? seed = null;
        var isTray = false;
        PlantHolderComponent? trayComp = null;

        if (TryComp<SeedComponent>(target, out var seedComp))
        {
            if (seedComp.Seed != null)
                seed = seedComp.Seed;
            else if (seedComp.SeedId != null && _proto.TryIndex<SeedPrototype>(seedComp.SeedId, out var proto))
                seed = proto;
        }
        else if (TryComp<PlantHolderComponent>(target, out var plantHolder))
        {
            seed = plantHolder.Seed;
            isTray = true;
            trayComp = plantHolder;
        }

        if (seed == null)
            return null;

        var exudeGases = seed.ExudeGasses.Keys
            .Select(g => Loc.GetString($"gases-{g}"))
            .ToArray();

        var consumeGases = seed.ConsumeGasses.Keys
            .Select(g => Loc.GetString($"gases-{g}"))
            .ToArray();

        var chemicals = seed.Chemicals.Keys.ToArray();

        var speciation = seed.MutationPrototypes
            .Select(id => _proto.TryIndex<SeedPrototype>(id, out var s) ? s.DisplayName : id.ToString())
            .ToArray();

        var mutations = new List<string>();
        if (seed.Seedless)      mutations.Add(Loc.GetString("plant-analyzer-mut-seedless"));
        if (seed.Ligneous)      mutations.Add(Loc.GetString("plant-analyzer-mut-ligneous"));
        if (seed.CanScream)     mutations.Add(Loc.GetString("plant-analyzer-mut-screaming"));
        if (seed.TurnIntoKudzu) mutations.Add(Loc.GetString("plant-analyzer-mut-kudzu"));
        foreach (var mut in seed.Mutations)
        {
            if (mut.Description is { } desc)
                mutations.Add(Loc.GetString(desc));
        }

        return new PlantAnalyzerScannedSeedMessage
        {
            TargetEntity = GetNetEntity(target),
            IsTray = isTray,
            IsDead = trayComp?.Dead ?? false,
            PlantHealth = trayComp?.Health ?? 0f,
            PlantMaxHealth = seed.Endurance,

            SeedName = seed.DisplayName,
            SeedYield = seed.Yield,
            SeedPotency = seed.Potency,
            HarvestType = seed.HarvestRepeat,
            Chemicals = chemicals,
            ExudeGases = exudeGases,
            ConsumeGases = consumeGases,
            Lifespan = seed.Lifespan,
            Maturation = seed.Maturation,
            Production = seed.Production,
            GrowthStages = seed.GrowthStages,
            Endurance = seed.Endurance,

            NutrientConsumption = seed.NutrientConsumption,
            WaterConsumption = seed.WaterConsumption,
            IdealHeat = seed.IdealHeat,
            HeatTolerance = seed.HeatTolerance,
            IdealLight = seed.IdealLight,
            LightTolerance = seed.LightTolerance,
            ToxinsTolerance = seed.ToxinsTolerance,
            LowPressureTolerance = seed.LowPressureTolerance,
            HighPressureTolerance = seed.HighPressureTolerance,
            PestTolerance = seed.PestTolerance,
            WeedTolerance = seed.WeedTolerance,

            Mutations = mutations.ToArray(),
            Speciation = speciation,
        };
    }
}
