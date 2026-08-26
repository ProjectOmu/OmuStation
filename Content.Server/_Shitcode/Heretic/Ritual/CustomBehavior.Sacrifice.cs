// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Common.Heretic;
using Content.Server._Goobstation.Objectives.Components;
using Content.Server.Body.Systems;
using Content.Server.Heretic.Components;
using Content.Shared.Heretic.Prototypes;
using Content.Shared.Mobs.Components;
using Robust.Shared.Prototypes;
using Content.Shared.Humanoid;
using Content.Server.Revolutionary.Components;
using Content.Shared.Mind;
using Content.Shared.Heretic;
using Content.Server.Heretic.EntitySystems;
using Content.Shared.Gibbing.Events;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Store.Components;
using Content.Server.Humanoid;            //Imp start
using Content.Shared.Forensics.Components;
using Robust.Shared.Toolshed.TypeParsers;
using Robust.Server.GameObjects;
using System;
using Content.Server._Goobstation.Heretic.EntitySystems;
using Content.Server.Forensics;
using Content.Server.Body.Components;
using Content.Shared.Forensics;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.GameObjects;
using Content.Shared.Chemistry.EntitySystems;
using  Content.Shared.Body.Components;        //Imp end


namespace Content.Server.Heretic.Ritual;

/// <summary>
///     Checks for a nearest dead body,
///     gibs it and gives the heretic knowledge points.
/// </summary>
// these classes should be lead out and shot
[Virtual] public partial class RitualSacrificeBehavior : RitualCustomBehavior
{
    /// <summary>
    ///     Minimal amount of corpses.
    /// </summary>
    [DataField]
    public float Min = 1;

    /// <summary>
    ///     Maximum amount of corpses.
    /// </summary>
    [DataField]
    public float Max = 1;

    /// <summary>
    ///     Should we count only targets?
    /// </summary>
    [DataField]
    public bool OnlyTargets;

    /// <summary>
    ///     Should we count only humanoids?
    /// </summary>
    [DataField]
    public bool OnlyHumanoid = true;

    // this is awful but it works so i'm not complaining
    protected SharedMindSystem _mind = default!;
    protected HereticSystem _heretic = default!;
    protected BodySystem _body = default!;
    protected EntityLookupSystem _lookup = default!;
    //imp start
    protected HumanoidAppearanceSystem _humanoid = default!;
    protected TransformSystem _transformSystem = default!;
    protected HellWorldSystem _hellworld = default!;
    protected BloodstreamSystem _bloodstream = default!;
    protected SharedSolutionContainerSystem _solutionContainerSystem = default!;
    //imp end


    [Dependency] protected IPrototypeManager _proto = default!;
    [Dependency] protected ILogManager _log = default!;
    [Dependency] protected IEntityManager _entmanager = default!;        //Imp

    private ISawmill? _sawmill;

    protected List<EntityUid> uids = new();

    public override bool Execute(RitualData args, out string? outstr)
    {
        _mind = args.EntityManager.System<SharedMindSystem>();
        _heretic = args.EntityManager.System<HereticSystem>();
        _body = args.EntityManager.System<BodySystem>();
        _lookup = args.EntityManager.System<EntityLookupSystem>();
        _proto = IoCManager.Resolve<IPrototypeManager>();
        _log = IoCManager.Resolve<ILogManager>();
        //Imp start
        _humanoid = args.EntityManager.System<HumanoidAppearanceSystem>();
        _transformSystem = args.EntityManager.System<TransformSystem>();
        _hellworld = args.EntityManager.System<HellWorldSystem>();
        _bloodstream = args.EntityManager.System<BloodstreamSystem>();
        _solutionContainerSystem = args.EntityManager.System<SharedSolutionContainerSystem>();
        _entmanager = IoCManager.Resolve<IEntityManager>();
        //Imp end

        uids = new();

        var hereticComp = args.Mind.Comp;

        var lookup = _lookup.GetEntitiesInRange(args.Platform, 1.5f);
        if (lookup.Count == 0)
        {
            outstr = Loc.GetString("heretic-ritual-fail-sacrifice");
            return false;
        }

        // get all the dead ones
        foreach (var look in lookup)
        {
            if (!args.EntityManager.TryGetComponent<MobStateComponent>(look, out var mobstate) // only mobs
            || OnlyHumanoid && !args.EntityManager.HasComponent<HumanoidAppearanceComponent>(look) // only humans
            || args.EntityManager.HasComponent<BorgChassisComponent>(look) // no borgs
            || OnlyTargets
                && hereticComp.SacrificeTargets.All(x => x.Entity != args.EntityManager.GetNetEntity(look)) // only targets
                && !_heretic.TryGetHereticComponent(look, out _, out _)) // or other heretics
                continue;

            if (mobstate.CurrentState != Shared.Mobs.MobState.Alive)
                uids.Add(look);
        }

        if (uids.Count < Min)
        {
            outstr = Loc.GetString("heretic-ritual-fail-sacrifice-ineligible");
            return false;
        }

        outstr = null;
        return true;
    }

    public override void Finalize(RitualData args)
    {
        var heretic = args.Mind.Comp;

        if (!args.EntityManager.TryGetComponent(args.Mind, out StoreComponent? store) ||
            !args.EntityManager.TryGetComponent(args.Mind, out MindComponent? mind))
            return;

        var knowledgeGain = 0f;
        for (var i = 0; i < Max && i < uids.Count; i++)
        {
            var isCommand = args.EntityManager.HasComponent<CommandStaffComponent>(uids[i]);
            var isSec = args.EntityManager.HasComponent<SecurityStaffComponent>(uids[i]);
            var isHeretic = _heretic.TryGetHereticComponent(uids[i], out var otherHeretic, out var otherMind);
            //get the humanoid appearance component
            if (!args.EntityManager.TryGetComponent<HumanoidAppearanceComponent>(uids[i], out var humanoid))
                return;

            //get the species prototype from that
            if (!_proto.TryIndex(humanoid.Species, out var speciesPrototype))
                return;

            //spawn a clone of the victim
            var sacrificalbody = args.EntityManager.Spawn(speciesPrototype.Prototype, _transformSystem.GetMapCoordinates(uids[i]));
            _humanoid.CloneAppearance(uids[i], sacrificalbody);
            //make sure it has the right DNA
            if (args.EntityManager.TryGetComponent<DnaComponent>(uids[i], out var victimDna))
            {
                if (args.EntityManager.TryGetComponent<BloodstreamComponent>(sacrificalbody, out var dummyBlood))
                {
                    //this is copied from BloodstreamSystem's OnDnaGenerated
                    //i hate it
                    if(_solutionContainerSystem.ResolveSolution(sacrificalbody, dummyBlood.BloodSolutionName, ref dummyBlood.BloodSolution, out var bloodSolution))
                    {
                        foreach (var reagent in bloodSolution.Contents)
                        {
                            List<ReagentData> reagentData = reagent.Reagent.EnsureReagentData();
                            reagentData.RemoveAll(x => x is DnaData);
                            reagentData.AddRange(_bloodstream.GetEntityBloodData(uids[i]));
                        }
                    }
                }
            }
            //beat the clone to death. this is just to get matching organs
            try
            {
                // YES!!! GIB!!!
                _body.GibBody(sacrificalbody);
            }
            catch (Exception e)
            {
                if (!args.EntityManager.IsQueuedForDeletion(sacrificalbody) && !args.EntityManager.Deleted(sacrificalbody))
                    args.EntityManager.QueueDeleteEntity(sacrificalbody);

                _sawmill ??= _log.GetSawmill("sacrifice");
                _sawmill.Error(e.Message);
            }
            //send the target to hell world
            _hellworld.AddVictimComponent(uids[i]);
            _hellworld.TeleportRandomly(args, uids[i]);
            _hellworld.SendToHell(uids[i], args, speciesPrototype);

            //update the heretic's knowledge

            // Sacrificed heretics lose their powers forever
            if (otherMind != EntityUid.Invalid && otherHeretic is { } h)
                args.EntityManager.RemoveComponentDeferred(otherMind, h);

            // update objectives
            // this is godawful dogshit. but it works :)
            if (_mind.TryFindObjective((args.Mind, mind), "HereticSacrificeObjective", out var crewObj)
            && args.EntityManager.TryGetComponent<HereticSacrificeConditionComponent>(crewObj, out var crewObjComp))
            {
                knowledgeGain = 2;
                crewObjComp.Sacrificed += 1;
            }

            if (_mind.TryFindObjective((args.Mind, mind), "HereticSacrificeHeadObjective", out var crewHeadObj)
            && args.EntityManager.TryGetComponent<HereticSacrificeConditionComponent>(crewHeadObj, out var crewHeadObjComp)
            && isCommand)
            {
                knowledgeGain = 3;
                crewHeadObjComp.Sacrificed += 1;
            }

            if (isHeretic)
            {
                knowledgeGain = 5;
            }
        }

        if (knowledgeGain > 0)
            _heretic.UpdateMindKnowledge((args.Mind, args.Mind.Comp, store, mind), args.Performer, knowledgeGain);

        // reset it because it refuses to work otherwise.
        uids = new();
        args.EntityManager.EventBus.RaiseLocalEvent(args.Mind, new EventHereticUpdateTargets());
    }
}
