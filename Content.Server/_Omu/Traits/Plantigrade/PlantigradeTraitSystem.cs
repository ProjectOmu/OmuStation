using Content.Server.Humanoid;
using Content.Shared._Omu.Traits.Plantigrade;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;

namespace Content.Server._Omu.Traits.Plantigrade;

public sealed class PlantigradeTraitSystem : SharedPlantigradeTraitSystem
{
    [Dependency] private readonly HumanoidAppearanceSystem _humanoid = default!;

    public override void UpdateAppearance(Entity<PlantigradeTraitComponent> ent, HumanoidAppearanceComponent appearance)
    {
        base.UpdateAppearance(ent, appearance);

        UpdateMarkings(ent, appearance);
    }

    private void UpdateMarkings(Entity<PlantigradeTraitComponent> ent, HumanoidAppearanceComponent appearance)
    {
        if (!appearance.MarkingSet.Markings.TryGetValue(MarkingCategories.Tail, out var markings))
            return;

        for (var i = 0; i < markings.Count; i++)
        {
            var currentMarkingId = markings[i].MarkingId;
            var newMarkingId = $"{ent.Comp.MarkingPrefix}{currentMarkingId}";

            if (!_proto.HasIndex<MarkingPrototype>(newMarkingId))
                continue;

            _humanoid.SetMarkingId(ent, MarkingCategories.Tail, i, newMarkingId, humanoid: appearance);
        }

        Dirty(ent.Owner, appearance);
    }
}
