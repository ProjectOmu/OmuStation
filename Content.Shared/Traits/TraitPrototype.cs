// SPDX-FileCopyrightText: 2022 CommieFlowers <rasmus.cedergren@hotmail.com>
// SPDX-FileCopyrightText: 2022 Morb <14136326+Morb0@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 Pieter-Jan Briers <pieterjan.briers+git@gmail.com>
// SPDX-FileCopyrightText: 2022 Rane <60792108+Elijahrane@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 Visne <39844191+Visne@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 rolfero <45628623+rolfero@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Leon Friedrich <60421075+ElectroJr@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 forkeyboards <91704530+forkeyboards@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Ed <96445749+TheShuEd@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Roles; // Omustation - Remake EE Traits System - change TraitPreferenceSelector for RequirementsSelector
using Content.Shared._Omu.Traits; // Omustation - Remake EE Traits System - Port trait functions
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Shared.Traits;

/// <summary>
/// Describes a trait.
/// </summary>
[Prototype]
public sealed partial class TraitPrototype : IPrototype,
    IComparable<TraitPrototype> // HardLight: IComparable<self>
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The name of this trait.
    /// </summary>
    [DataField]
    public LocId Name { get; private set; } = string.Empty;

    /// <summary>
    /// The description of this trait.
    /// </summary>
    [DataField]
    public LocId? Description { get; private set; }

    /// <summary>
    /// Requirements for the trait.
    /// </summary>
    /// <remarks>
    /// Omustation - Remake EE Traits System - change TraitPreferenceSelector for RequirementsSelector
    /// This field is used by the humanoid profile editor to disable RequirementsSelectors when requirements are(nt) met.
    /// </remarks>
    [DataField] // TODO: reduce scope / access to this field
    public HashSet<JobRequirement>? Requirements;

    /// <summary>
    /// Don't apply this trait to entities this whitelist IS NOT valid for.
    /// </summary>
    [DataField]
    public EntityWhitelist? Whitelist;

    /// <summary>
    /// Don't apply this trait to entities this whitelist IS valid for. (hence, a blacklist)
    /// </summary>
    [DataField]
    public EntityWhitelist? Blacklist;

    /// <summary>
    /// The components that get added to the player, when they pick this trait.
    /// </summary>
    [DataField]
    public ComponentRegistry? Components { get; private set; } = default!; // Omustation - remake EE traits system - this has been made nullable in order to allow for traits which rely on just functions, instead of giving components.

    /// <summary>
    /// Gear that is given to the player, when they pick this trait.
    /// </summary>
    [DataField]
    public EntProtoId? TraitGear;

    /// <summary>
    /// Trait Price. If negative number, points will be added.
    /// </summary>
    [DataField]
    public int Cost = 0;

    /// <summary>
    /// Adds a trait to a category, allowing you to limit the selection of some traits to the settings of that category.
    /// </summary>
    [DataField]
    public ProtoId<TraitCategoryPrototype>? Category;

    // begin Omustation - Remake EE Traits System
    /// <summary>
    ///     The number of trait points required for this trait.
    /// </summary>
    /// <remarks
    ///     This is an Omustation change, and is distinct from the upstream wizden cost field. This is for a few reasons:
    ///     First, is that traits points upstream are category specific. So, if you take a trait for 2 points from the "Languages" category, it will ONLY
    ///     use up the trait points availible to that category. Hence, these are "global".
    ///     Second, is that being able to optionally restrict the amount of selectable traits in a category AND globally might be useful for balancing.
    ///     Third, is that I want to alter as little upstream code as possible, and if that means adding two cost fields to the trait prototype, so be it.
    /// </remarks>
    [DataField]
    public int GlobalCost = 0;

    /// <summary>
    ///     Whether this trait will increment MaxTraits or not.
    ///     Useful for small traits, such as accents, which don't affect gameplay all that much and should be selectable at no cost.
    /// </summary>
    [DataField]
    public bool CountsTowardsMaxTraits = true;

    /// <summary>
    ///     Functions which should be called when this trait is added to a player.
    /// </summary>
    [DataField(serverOnly: true)]
    public HashSet<TraitFunction>? Functions;
    // end Omustation - Remake EE Traits System

    // Einstein Engines - Language begin (remove this if trait system refactor)
    /// <summary>
    ///     The list of all Spoken Languages that this trait adds.
    /// </summary>
    [DataField]
    public List<string>? LanguagesSpoken { get; private set; } = default!;

    /// <summary>
    ///     The list of all Understood Languages that this trait adds.
    /// </summary>
    [DataField]
    public List<string>? LanguagesUnderstood { get; private set; } = default!;

    /// <summary>
    ///     The list of all Spoken Languages that this trait removes.
    /// </summary>
    [DataField]
    public List<string>? RemoveLanguagesSpoken { get; private set; } = default!;

    /// <summary>
    ///     The list of all Understood Languages that this trait removes.
    /// </summary>
    [DataField]
    public List<string>? RemoveLanguagesUnderstood { get; private set; } = default!;
    // Einstein Engines - Language end

    // HardLight Changes start

    /// <summary>
    ///     If this trait should replace the added components. Hardlight change.
    /// </summary>
    [DataField]
    public bool ReplaceComponents;

    /// <summary>
    ///     Comparison for sorting traits by cost.
    /// </summary>
    public int CompareTo(TraitPrototype? other)
    {
        return other == null ? 1 : Cost.CompareTo(other.Cost);
    }

    // End HardLight
}
