using Content.Omu.Common.SkillSystem;
using Content.Shared.Clothing;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Mind.Components;
using Content.Omu.Server.SkillSystem.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Physics.Collision.Shapes;
using System.Linq;

namespace Content.Omu.Server.SkillSystem;

public sealed partial class OmuSkillSystem : CommenOmuSkillSystem
{
    [Dependency] private readonly PhysicsSystem _physicsSystem = default!;
    [Dependency] private readonly FixtureSystem _fixtures = default!;
    public void ModifyPulledMass(Entity<SkillRequermentComponent> ent, ref PullStartedMessage args)
    {
        if (!TryComp<MindContainerComponent>(args.PullerUid, out var mindContainer)) return;
        if (!mindContainer.HasMind) return;
        if (!TryComp<SkillHolderComponent>(mindContainer.Mind, out var skillComp)) return;
        if (!TryComp<SkillRequermentComponent>(args.PulledUid, out var requComp)) return;
        if (!TryComp<FixturesComponent>(args.PulledUid, out var fixturesComp)) return;
        if (fixturesComp.Fixtures.Keys.Count < 1) return;

        var fixture = fixturesComp.Fixtures[fixturesComp.Fixtures.Keys.First()];
        var shape = fixture.Shape;
        //we adda  new fixture that has a decnity scaled based on the based on the requirment -> aka make a unique skill profile for boxes and other items if you drag them
        _fixtures.TryCreateFixture(args.PulledUid,
                                    shape,
                                    "SKILL-SYS-WHIGHT",
                                    GetSkillSpeedModifier(args.PullerUid, args.PulledUid, out var _));
        _physicsSystem.ResetMassData(args.PulledUid);//basicly a recalc function
        return;
    }
    public void ResetModifiedPulledMass(Entity<SkillRequermentComponent> ent, ref PullStoppedMessage args)
    {
        _fixtures.DestroyFixture(args.PulledUid, "SKILL-SYS-WHIGHT");
        _physicsSystem.ResetMassData(args.PulledUid);//basicly a recalc function
    }
}
