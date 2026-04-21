// SPDX-FileCopyrightText: 2026 Raze500
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.StationEvent.Metrics;
using Robust.Shared.Prototypes;

namespace Content.Omu.Shared.GameTicking.EventDirector;

/// <summary>
///   A series of named StoryBeats which we want to take the station through in the given sequence.
///   Gated by various settings such as the number of players
/// </summary>
[DataDefinition]
[Prototype("story")]
public sealed partial class StoryPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    ///   A human-readable description string for logging / admins
    /// </summary>
    // the yaml fills this in. empty text as backup in case someone forgets to write a description.
    [DataField]
    public string Description = string.Empty;

    /// <summary>
    ///   Minimum number of players on the station to pick this story
    /// </summary>
    [DataField]
    public int MinPlayers = -1;

    /// <summary>
    ///   Maximum number of players on the station to pick this story
    /// </summary>
    [DataField]
    public int MaxPlayers = Int32.MaxValue;

    /// <summary>
    ///   List of beat-ids in this story.
    /// </summary>
    [DataField]
    public ProtoId<StoryBeatPrototype>[]? Beats;
}

/// <summary>
///   A point in the story of the station where the dynamic system tries to achieve a certain level of chaos.
///   for instance you want a battle (goal has lots of hostiles)
///   then the next beat you might want a restoration of peace (goal has a balanced combat score)
///   then you might want to have the station heal up (goal has low medical, atmos and power scores)
///
///   In each case you create a beat and string them together into a story.
///
///   EndIfAnyWorse might be used for a battle to trigger when the chaos has become high enough.
///   endIfAllBetter is suitable for when you want the station to reach a given level of peace before you subject them to
///   the next round of chaos.
/// </summary>
[DataDefinition]
[Prototype("storyBeat")]
public sealed partial class StoryBeatPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    ///   A human-readable description string for logging / admins
    /// </summary>
    // the yaml fills this in. empty text as backup in case someone forgets to write a description.
    [DataField]
    public string Description = string.Empty;

    /// <summary>
    ///   Which chaos levels we are driving in this beat and the values we are aiming for
    /// </summary>
    [DataField]
    public ChaosMetrics Goal = new ChaosMetrics();

    /// <summary>
    ///   Early end if things deteriorate too much.
    ///   If the current metrics get worse than any of these, end the story beat.
    /// </summary>
    [DataField]
    public ChaosMetrics EndIfAnyWorse = new ChaosMetrics();

    /// <summary>
    ///   Early end if life is good enough.
    ///   If the current metrics get better than all of these, end the story beat.
    /// </summary>
    [DataField]
    public ChaosMetrics EndIfAllBetter = new ChaosMetrics();

    /// <summary>
    ///   The number of seconds that we will remain in this state at minimum
    /// </summary>
    [DataField]
    public float MinSecs = 480.0f;

    /// <summary>
    ///   The number of seconds that we will remain in this state at maximum
    /// </summary>
    [DataField]
    public float MaxSecs = 1200.0f;

    /// <summary>
    ///   Seconds between events during this beat (min) - 2 minute default
    /// </summary>
    [DataField]
    public float EventDelayMin = 120.0f;

    /// <summary>
    ///   Seconds between events during this beat (max) - 6 minute default
    /// </summary>
    [DataField]
    public float EventDelayMax = 360.0f;

    /// <summary>
    ///   How many different events we choose from (at random) when performing this StoryBeat.
    ///   Higher values add more variety, 1 always picks the best event.
    /// </summary>
    [DataField]
    public int RandomEventLimit = 3;
}
