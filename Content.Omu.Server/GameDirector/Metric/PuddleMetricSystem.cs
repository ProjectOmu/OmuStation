using System.Linq;
using Content.Omu.Server.GameDirector.Metric.Components;
using Content.Server.Chemistry.Containers.EntitySystems;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Goobstation.Common.StationEvent.Metrics;

namespace Content.Omu.Server.GameDirector.Metric;

/// <summary>
///   Measure the mess of the station in puddles on the floor
///
///   Jani - JaniMetricComponent.Puddles points per BaselineQty of various substances
/// </summary>
public sealed class PuddleMetricSystem : ChaosMetricSystem<PuddleMetricComponent>
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainerSystem = default!;

    protected override ChaosMetrics CalculateChaos(EntityUid uid, PuddleMetricComponent component, CalculateChaosEvent args)
    {
        var query = EntityQueryEnumerator<PuddleComponent, SolutionContainerManagerComponent>();
        double messChaos = 0;

        while (query.MoveNext(out var puddleUid, out var puddle, out var solutionMgr))
        {
            if (!_solutionContainerSystem.TryGetSolution(puddleUid, puddle.SolutionName, out var puddleSolution, out _))
                continue;

            double currentPuddleChaos = 0.0f;
            foreach (var substance in puddleSolution.Value.Comp.Solution.Contents)
            {
                var substanceChaos = component.Puddles.GetValueOrDefault(substance.Reagent.Prototype, component.PuddleDefault).Double();
                currentPuddleChaos += Math.Round(substanceChaos * substance.Quantity.Double());
            }

            messChaos += currentPuddleChaos;
        }

        return new ChaosMetrics(new Dictionary<ChaosMetric, double>()
        {
            { ChaosMetric.Mess, messChaos },
        });
    }
}
