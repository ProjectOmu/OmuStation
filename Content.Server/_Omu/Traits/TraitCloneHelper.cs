using Content.Server.Cloning;
using Content.Server.GameTicking;
using Content.Server.Hands.Systems;
using Content.Server.Mind;
using Content.Server.Roles.Jobs;
using Content.Shared.Preferences;
using Content.Shared.Traits;
using Content.Shared.Whitelist;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Omu.Traits;

public sealed class OmuCloneHelperSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly JobSystem _jobs = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly CloningSystem _clone = default!;

    /// <summary>
    /// Gives clone-only trait equipment that normally depends on the spawned entity's mind.
    /// Clones do not receive the original mind, so we resolve the job from the original entity
    /// and spawn the equipment onto the clone.
    ///
    /// This is pain. But i do not want to fucking rewrite OnPlayerSpawn Functions nor fuck with more systems than i have to.
    /// Given only mantled beasts do this at this time, this shit is incredibly fucking specific.
    /// todo kill this, kill vulps, and omumod this.
    /// </summary>
    public void GiveCloneJobTraitEquipment(EntityUid original, EntityUid clone, HumanoidCharacterProfile? profile = null)
    {
        if (profile == null)
        {
            if (!TryComp<ActorComponent>(original, out var actor))
                return;
            profile = _gameTicker.GetPlayerProfile(actor.PlayerSession);
        }

        // Hey this is technically borderline duplicated code from the OnPlayerSpawn function.
        // if you are reading this, i am sorry, but also refactor the mantled best bullshit please.
        if (!_mind.TryGetMind(original, out var mindId, out _) ||
            !_jobs.MindTryGetJob(mindId, out var jobProto) ||
            !TryComp(clone, out TransformComponent? transform))
            return;

        foreach (var traitId in profile.TraitPreferences)
        {
            if (!_prototypeManager.TryIndex(traitId, out var traitPrototype) ||
                traitPrototype.Functions == null)
                continue;

            foreach (var function in traitPrototype.Functions)
            {
                if (function is not TraitGiveEquipmentIfHasJobs equipmentFunction)
                    continue;

                EntProtoId? equipment = null;

                if (equipmentFunction.JobEquipment.TryGetValue(jobProto.ID, out var equipmentForJob))
                    equipment = equipmentForJob;
                else if (equipmentFunction.EquipmentIfRequirementUnmet != null)
                    equipment = equipmentFunction.EquipmentIfRequirementUnmet;

                if (equipment == null)
                    continue;

                var spawned = Spawn(equipment.Value, transform.Coordinates);
                _hands.PickupOrDrop(clone, spawned);
            }
        }
    }

    // todo upstream this
    public void CopyHands(EntityUid original, EntityUid clone, EntityWhitelist? whitelist = null, EntityWhitelist? blacklist = null, bool copyStorage = true)
    {
        var coords = Transform(clone).Coordinates;

        foreach (var held in _hands.EnumerateHeld(original))
        {
            var cloneItem = _clone.CopyItem(held, coords, whitelist, blacklist, copyStorage);
            if (cloneItem == null)
                continue;

            if (!_hands.TryPickupAnyHand(clone, cloneItem.Value, checkActionBlocker: false))
                Del(cloneItem);
        }
    }
}
