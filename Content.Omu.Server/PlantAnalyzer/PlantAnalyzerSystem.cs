using System.Linq;
using Content.Omu.Shared.PlantAnalyzer;
using Content.Omu.Shared.PlantAnalyzer.Components;
using Content.Server.Botany;
using Content.Server.Botany.Components;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Server.PowerCell;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Omu.Server.PlantAnalyzer;

public sealed class PlantAnalyzerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly PowerCellSystem _cell = default!;
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

        // only valid on seeds and plant trays that have a seed
        var isValidTarget = HasComp<SeedComponent>(target)
            || TryComp<PlantHolderComponent>(target, out var holder) && holder.Seed != null;

        if (!isValidTarget)
            return;

        // do not start a second doafter while one is already running
        if (ent.Comp.DoAfter != null)
            return;

        // check battery up front - no point starting an 8s wait with no power
        if (!_cell.HasActivatableCharge(ent.Owner, user: args.User))
            return;

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, ent.Comp.ScanDelay, new PlantAnalyzerDoAfterEvent(), ent, target: target, used: ent)
        {
            NeedHand = true,
            BreakOnDamage = true,
            BreakOnMove = true,
            MovementThreshold = 0.01f
        };

        _doAfter.TryStartDoAfter(doAfterArgs, out ent.Comp.DoAfter);
    }

    private void OnDoAfter(Entity<PlantAnalyzerComponent> ent, ref PlantAnalyzerDoAfterEvent args)
    {
        ent.Comp.DoAfter = null;

        if (args.Handled || args.Cancelled || args.Target is not { } target)
            return;

        // consume 2 charges (scan is always advanced)
        if (!_cell.TryUseActivatableCharge(ent.Owner, user: args.User))
            return;
        if (!_cell.TryUseActivatableCharge(ent.Owner, user: args.User))
            return;

        var state = BuildScanState(ent, target);
        if (state == null)
            return;

        _ui.SetUiState(ent.Owner, PlantAnalyzerUiKey.Key, state);
        _ui.TryOpenUi(ent.Owner, PlantAnalyzerUiKey.Key, args.User);
        _audio.PlayPvs(ent.Comp.ScanningEndSound, ent);

        args.Handled = true;
    }

    private PlantAnalyzerScannedSeedMessage? BuildScanState(Entity<PlantAnalyzerComponent> ent, EntityUid target)
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

        // collect gas name strings (works for any gas, no hardcoded enum)
        var exudeGases = seed.ExudeGasses.Keys
            .Select(g => Loc.GetString($"gases-{g}"))
            .ToArray();

        var consumeGases = seed.ConsumeGasses.Keys
            .Select(g => Loc.GetString($"gases-{g}"))
            .ToArray();

        var chemicals = seed.Chemicals.Keys.ToArray();

        // resolve possible sub-species display names
        var speciation = seed.MutationPrototypes
            .Select(id => _proto.TryIndex<SeedPrototype>(id, out var s) ? s.DisplayName : id.ToString())
            .ToArray();

        var mutations = PlantMutationFlags.None;
        if (seed.TurnIntoKudzu) mutations |= PlantMutationFlags.TurnIntoKudzu;
        if (seed.Seedless)      mutations |= PlantMutationFlags.Seedless;
        if (seed.Ligneous)      mutations |= PlantMutationFlags.Ligneous;
        if (seed.CanScream)     mutations |= PlantMutationFlags.CanScream;

        return new PlantAnalyzerScannedSeedMessage
        {
            TargetEntity = GetNetEntity(target),
            IsTray = isTray,
            IsDead = trayComp?.Dead ?? false,
            PlantHealth = trayComp?.Health ?? seed.Endurance,
            PlantMaxHealth = seed.Endurance,

            SeedName = seed.DisplayName,
            SeedYield = seed.Yield,
            SeedPotency = seed.Potency,
            HarvestType = (PlantAnalyzerHarvestType) seed.HarvestRepeat,
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

            Mutations = mutations,
            Speciation = speciation,
        };
    }
}
