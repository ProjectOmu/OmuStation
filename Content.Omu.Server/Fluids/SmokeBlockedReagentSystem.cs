// SPDX-License-Identifier: MIT

using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Omu.Shared.Fluids;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Fluids;
using Robust.Shared.Prototypes;

namespace Content.Omu.Server.Fluids;

public sealed class SmokeBlockedReagentSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedPuddleSystem _puddle = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;

    private ProtoId<ReagentPrototype>[] _blocked = Array.Empty<ProtoId<ReagentPrototype>>();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SmokeComponent, SolutionContainerChangedEvent>(OnSolutionChanged);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);

        CacheBlocked();
    }

    private void OnSolutionChanged(Entity<SmokeComponent> ent, ref SolutionContainerChangedEvent args)
    {
        if (args.SolutionId != SmokeComponent.SolutionName || _blocked.Length == 0)
            return;

        var blocked = args.Solution.GetTotalPrototypeQuantity(_blocked);
        if (blocked <= FixedPoint2.Zero
            || !_solutions.TryGetSolution(ent.Owner, SmokeComponent.SolutionName, out var smoke, out _))
            return;

        var removed = args.Solution.SplitSolutionWithOnly(blocked, _blocked);
        _solutions.UpdateChemicals(smoke.Value);
        _puddle.TrySpillAt(Transform(ent).Coordinates, removed, out _, false);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<SmokeBlockedReagentsPrototype>())
            CacheBlocked();
    }

    private void CacheBlocked()
    {
        _blocked = _proto.EnumeratePrototypes<SmokeBlockedReagentsPrototype>()
            .SelectMany(prototype => prototype.Reagents)
            .Distinct()
            .ToArray();
    }
}
