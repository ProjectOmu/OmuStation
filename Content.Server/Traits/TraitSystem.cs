// SPDX-FileCopyrightText: 2022 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 Paul Ritter <ritter.paul1@googlemail.com>
// SPDX-FileCopyrightText: 2022 Rane <60792108+Elijahrane@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 Visne <39844191+Visne@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 forkeyboards <91704530+forkeyboards@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Cojoke <83733158+Cojoke-dot@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 DrSmugleaf <drsmugleaf@gmail.com>
// SPDX-FileCopyrightText: 2024 Ed <96445749+TheShuEd@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 LordCarve <27449516+LordCarve@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Plykiya <58439124+Plykiya@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 lzk <124214523+lzk228@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._EinsteinEngines.Language;
using Content.Server.Traits.Assorted;
using Content.Shared.GameTicking;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Movement.Systems;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Traits;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Server.Traits;

public sealed class TraitSystem : EntitySystem
{
    [Dependency] private readonly IComponentFactory _componentFactory = default!; // Omustation - Remake EE Traits System - Port trait functions
    [Dependency] private readonly ISerializationManager _serialization = default!; // Omustation - Remake EE Traits System - Port trait functions
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedHandsSystem _sharedHandsSystem = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private readonly LanguageSystem _languageSystem = default!; // Goobstation - EE
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!; // HardLight

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    // When the player is spawned in, add all trait components selected during character creation
    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        // Check if player's job allows to apply traits
        if (args.JobId == null ||
            !_prototypeManager.TryIndex<JobPrototype>(args.JobId ?? string.Empty, out var protoJob) ||
            !protoJob.ApplyTraits)
        {
            return;
        }

        ApplyProfileTraits(args.Mob, args.Profile, true); // HardLight
    }

    /// <summary>
    /// This whole method is a hardlight edit. I hate it, but it is what it is.
    /// HardLight: Applies the selected traits from a humanoid profile to an existing entity.
    /// This is intended for non-standard spawn paths like admin spawning or cloning
    /// that already have a validated profile and just need its trait components replayed.
    /// </summary>
    public void ApplyProfileTraits(EntityUid uid, HumanoidCharacterProfile profile, bool addTraitGear)
    {
        var sortedTraits = new List<TraitPrototype>(); // Hardlight change sort and apply traits by cost.
        foreach (var traitId in profile.TraitPreferences)
        {
            if (!_prototypeManager.TryIndex<TraitPrototype>(traitId, out var traitPrototype))
            {
                Log.Warning($"No trait found with ID {traitId}!");
                return;
            }

            sortedTraits.Add(traitPrototype); // Hardlight change sort and apply traits by cost.
        }

        sortedTraits.Sort(); // Hardlight change sort and apply traits by cost.

        foreach (var traitPrototype
                 in sortedTraits)  // Hardlight change sort and apply traits by cost.
        {

            if (_whitelistSystem.IsWhitelistFail(traitPrototype.Whitelist, uid) ||
                _whitelistSystem.IsBlacklistPass(traitPrototype.Blacklist, uid))
                continue;

            // Add all components required by the prototype
            // Omu start - Remake EE Traits System - Port trait functions (make traits that don't directly give you components *possible*)
            if (traitPrototype.Components != null)
            {
                foreach (var (name, entry) in traitPrototype.Components) // omu edit, im tired, im hardcoding a very bad check.
                {
                    if (!addTraitGear && _componentFactory.GetRegistration(name).Type == typeof(BuckleOnMapInitComponent)) // omu todo, this bad. here to prevent wheelchairs from spawning on medical cloner.
                        continue;

                    if (!traitPrototype.ReplaceComponents && HasComp(uid, _componentFactory.GetRegistration(name).Type)) // hardlight
                        continue;

                    EntityManager.AddComponent(uid, _componentFactory.GetComponent(entry), traitPrototype.ReplaceComponents);
                }
            }
            // Omu end

            //  EE Lang, Goobedited to be less fucking shit holy fuck.
            _languageSystem.UpdateEntityLanguages(uid, traitPrototype);  // Remove/Add Languages required by the prototype
            // EE Lang end.

            // begin Omustation - Remake EE Traits System - Port trait functions
            if (traitPrototype.Functions != null)
                foreach (var function in traitPrototype.Functions)
                    function.OnPlayerSpawn(uid, _componentFactory, EntityManager, _serialization);
            // end Omustation - Remake EE Traits System - Port trait functions

            // HardLight: Force an immediate refresh so movement penalties/bonuses apply on spawn.
            _movementSpeed.RefreshMovementSpeedModifiers(uid);

            if (!addTraitGear) // required for cloning not to spawn shit
                continue;

            // Hardlight end.

            if (traitPrototype.TraitGear == null)
                continue;

            if (!TryComp(uid, out HandsComponent? handsComponent))
                continue;

            var coords = Transform(uid).Coordinates;
            var inhandEntity = Spawn(traitPrototype.TraitGear, coords);
            _sharedHandsSystem.TryPickup(uid,
                inhandEntity,
                checkActionBlocker: false,
                handsComp: handsComponent);
        }
    }
}
