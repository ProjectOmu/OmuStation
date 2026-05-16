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
using Content.Shared.GameTicking;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Movement.Systems; // HardLight
using Content.Shared.Preferences; // HardLight
using Content.Shared.Roles;
using Content.Shared.Traits;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager; // Omustation - Remake EE Traits System - Port trait functions

namespace Content.Server.Traits;

public sealed class TraitSystem : EntitySystem
{
    [Dependency] private readonly IComponentFactory _componentFactory = default!; // Omustation - Remake EE Traits System - Port trait functions
    [Dependency] private readonly ISerializationManager _serialization = default!; // Omustation - Remake EE Traits System - Port trait functions
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedHandsSystem _sharedHandsSystem = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;
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

        foreach (var traitId in args.Profile.TraitPreferences)
        {
            if (!_prototypeManager.TryIndex<TraitPrototype>(traitId, out var traitPrototype))
            {
                Log.Warning($"No trait found with ID {traitId}!");
                return;
            }

            AddTrait(args.Mob, traitPrototype); // Omu: refactor
        }
    }

    /// <summary>
    /// HardLight: Applies the selected traits from a humanoid profile to an existing entity.
    /// This is intended for non-standard spawn paths like admin spawning or cloning
    /// that already have a validated profile and just need its trait components replayed.
    /// </summary>
    public void ApplyProfileTraits(EntityUid uid, HumanoidCharacterProfile profile, string? playerName = null, bool addTraitGear = true)
    {
        var sortedTraits = new List<TraitPrototype>();
        foreach (var traitId in profile.TraitPreferences)
        {
            if (_prototypeManager.TryIndex<TraitPrototype>(traitId, out var traitPrototype))
                sortedTraits.Add(traitPrototype);
        }

        sortedTraits.Sort();

        foreach (var traitPrototype in sortedTraits)
        {
// OMU: commenting out this "trait<-->player whitelisting" code
//			if (traitPrototype.Logins.Count > 0 &&
//				(playerName == null || !traitPrototype.Logins.Contains(playerName)))
//			{
//				continue;
//			}

            AddTrait(uid, traitPrototype, addTraitGear);
        }
    }

    /// <summary>Adds a single Trait Prototype to an Entity.</summary>
    /// <remarks>
    ///   This method should handle all paths for which we are adding a trait to an entity,
    ///   including [admin > spawn here] and cloning. It is a mixmash of what we previously had
    ///   in OnPlayerSpawnComplete above, some HardLight code, and some refactorings @ishkab wrote.
    /// </remarks>
    public void AddTrait(EntityUid uid, TraitPrototype traitProto, bool addTraitGear = true)
    {
        // Check whitelist/blacklist
        if (_whitelistSystem.IsWhitelistFail(traitProto.Whitelist, uid) ||
            _whitelistSystem.IsBlacklistPass(traitProto.Blacklist, uid))
            return;

        // Add all components required by the prototype
        if(traitProto.Components is not null) // Omustation - Remake EE Traits System - Port trait functions (make traits that don't directly give you components *possible*)
            EntityManager.AddComponents(uid, traitProto.Components, traitProto.ReplaceComponents); // Hardlight: Added ReplaceComponents

        // Einstein Engines - Language begin (remove this if trait system refactor)
        // Remove/Add Languages required by the prototype
        var language = EntityManager.System<LanguageSystem>();

        // stop having the same code-structures four times in a row
        void DoLangProcessing(List<string>? langlist, Action<string> action)
        {
            if(langlist is not null)
                foreach(var lang in langlist)
                    action(lang);
        }

        DoLangProcessing(traitProto.RemoveLanguagesSpoken,     l => language.RemoveLanguage(uid, l, true, false));
        DoLangProcessing(traitProto.RemoveLanguagesUnderstood, l => language.RemoveLanguage(uid, l, false, true));
        DoLangProcessing(traitProto.LanguagesSpoken,           l => language.AddLanguage   (uid, l, true, false));
        DoLangProcessing(traitProto.LanguagesUnderstood,       l => language.AddLanguage   (uid, l, false, true));

        // begin Omustation - Remake EE Traits System - Port trait functions
        if (traitProto.Functions != null)
            foreach (var function in traitProto.Functions)
                function.OnPlayerSpawn(uid, _componentFactory, EntityManager, _serialization);
        // end Omustation - Remake EE Traits System - Port trait functions

        // HardLight: Force an immediate refresh so movement penalties/bonuses apply on spawn.
        _movementSpeed.RefreshMovementSpeedModifiers(uid);

        // Add item(s) required by the trait
        if (addTraitGear && traitProto.TraitGear != null && TryComp(uid, out HandsComponent? handsComponent)) // HardLight: Added addTraitGear
        {
            var coords = Transform(uid).Coordinates;
            var inhandEntity = Spawn(traitProto.TraitGear, coords);
            _sharedHandsSystem.TryPickup(uid,
                inhandEntity,
                checkActionBlocker: false,
                handsComp: handsComponent);
        }
    }
}
