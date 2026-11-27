using System.Collections.Concurrent;
using Content.Goobstation.Common.StationEvents.SecretPlus;
using Content.Omu.Server.StationEvents.SecretDoublePlus.ChaosFactors;
using Content.Omu.Server.StationEvents.SecretDoublePlus.Commands;
using Content.Shared.Random;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Omu.Server.StationEvents.SecretDoublePlus;

[RegisterComponent, AutoGenerateComponentPause, Access(typeof(SecretDoublePlusSystem), typeof(ChaosCommand))]
public sealed partial class SecretDoublePlusComponent : Component
{
    /// <summary>
    ///   How long until the next check for an event runs
    ///   Default value is how long until first event is allowed
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan TimeNextEvent;

    /// <summary>
    ///   Minimum interval between events
    /// </summary>
    [DataField(required: true)]
    public TimeSpan EventIntervalMin;

    /// <summary>
    ///   Maximum interval between events
    /// </summary>
    [DataField(required: true)]
    public TimeSpan EventIntervalMax;

    /// <summary>
    ///   The current chaos score.
    ///   Lower (negative - so the number is actually higher) values will tend to spawn higher-impact events.
    ///   Can be and usually is negative.
    /// </summary>
    [DataField]
    public float ChaosScore = 0;

    /// <summary>
    ///   The minimum amount of chaos to generate at the start per player.
    ///   Normally used for roundstart antags, but will still generate if they are disabled and will then be spent on midround events.
    /// </summary>
    [DataField]
    public float MinStartingChaos;

    /// <summary>
    ///   The maximum amount of chaos to generate at the start per player.
    /// </summary>
    [DataField]
    public float MaxStartingChaos;

    /// <summary>
    ///   The exponent applied when weighting events by chaos at round start.
    ///   See MidRoundChaosMatchingPower for more details.
    ///
    ///   Suggested range is [1.0, 2.0]
    /// </summary>
    [DataField]
    public float RoundStartChaosMatchingPower = 1f;

    /// <summary>
    ///   The exponent applied when weighting events by chaos. Higher values make the scheduler
    ///   pickier and favors events that keep the chaos score close to 0 on each roll.
    ///   Lower values make the round more random, which often allows a larger chaos
    ///   deficit to build before rolling one or more major events.
    ///
    ///   Suggested range is [2.0, 8.0], but go crazy if you want to with [0.0, 35.0]
    /// </summary>
    [DataField]
    public float MidRoundChaosMatchingPower = 6f;

    /// <summary>
    ///   The percentage of event weight still used when an event moves the overall round chaos score further from 0.
    ///   Values below 1 ensure we're pushing the round towards equilibrium (intended).
    ///   Values above 1 prioritize weird snowbally event selection, don't use for serious modes.
    /// </summary>
    [DataField]
    public float OppositeDirectionEventChance = 0.05f;

    /// <summary>
    ///   Chaos change multiplier we're using.
    ///   Affects midround chaos generation over time.
    ///   Rolled randomly when starting the gamerule.
    /// </summary>
    [ViewVariables]
    public float ChaosChangeVariation = 1f;

    /// <summary>
    ///   Minimum ChaosChangeVariation to generate at the start.
    /// </summary>
    [DataField]
    public float ChaosChangeVariationMin = 1f;

    /// <summary>
    ///   Maximum ChaosChangeVariation to generate at the start.
    /// </summary>
    [DataField]
    public float ChaosChangeVariationMax = 1f;

    /// <summary>
    ///   How much to bias ChaosChangeVariation towards generating closer to 1.
    ///   Higher values mean it rolls closer to 1, lower values means it rolls farther from 1.
    /// </summary>
    [DataField]
    public float ChaosChangeVariationExponent = 2f;

    /// <summary>
    ///   How much to ramp up per second.
    ///   Turns the scheduler into essentially the survival gamemode.
    ///   Linearly scales chaos point generation, event frequency, and reduces reoccurence delays.
    ///   For example, if you set this to 1/60, after 60 seconds chaos generation and event frequency will be doubled, and reoccurence delays halved.
    /// </summary>
    [DataField]
    public float SpeedRamping = 0f;

    /// <summary>
    ///   What percentage of the chaos budget can be saved when calculating the chaos target for a mid-round roll.
    ///   The per-roll save is rolled randomly from 0 to SaveChaosBudgetRange, and that chunk of chaos budget is shaved off to be held for the next roll.
    ///   Only used when Chaos is negative. Valid range [0.0, 1.0].
    /// </summary>
    [DataField]
    public float SaveChaosBudgetRange = 0.3f;

    /// <summary>
    ///   The percentage chance that a mid-round roll should be skipped entirely when close to 0 chaos.
    ///   This chance reduces smoothly as we get further from 0 chaos, cutting in half for each distance of MidRoundSkipRange magnitude in either direction.
    /// </summary>
    [DataField]
    public float MidRoundSkipChance = 0.5f;

    /// <summary>
    ///   The magnitude of chaos score at which MidRoundSkipChance halves.
    ///   As the chaos score gets further from 0 it continues to halve each time the magnitude reaches a multiple of MidRoundSkipRange.
    /// </summary>
    [DataField]
    public float MidRoundSkipRange = 250f;

    /// <summary>
    ///   Set to true to skip roundstart antags.
    ///   This will still let negative chaos accrue so make sure to tweak
    ///   starting chaos and factors unless you want huge mid-round spawns.
    /// </summary>
    [DataField]
    public bool NoRoundstartAntags = false;

    /// <summary>
    ///   Whether to ignore whether an event can actually currently run.
    ///   Will lead to events firing earlier than normal and several times even when normally limited.
    /// </summary>
    [DataField]
    public bool IgnoreTimings = false;

    /// <summary>
    ///   Whether to ignore incompatible roundstart antags. Also permits several of one antag.
    ///   Use if you want to allow really high amounts of roundstart antags.
    /// </summary>
    [DataField]
    public bool IgnoreIncompatible = false;

    /// <summary>
    ///   All the event types that are disallowed to run.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<EventTypePrototype>> DisallowedEvents = new();

    /// <summary>
    ///   Weight table for primary roundstart antags.
    /// </summary>
    [DataField]
    public ProtoId<WeightedRandomPrototype> PrimaryAntagsWeightTable = "SecretPlusPrimary"; // Keep using the old SecretPlus tables for now

    /// <summary>
    ///   Weight table for roundstart antags picked after the primary.
    /// </summary>
    [DataField]
    public ProtoId<WeightedRandomPrototype> RoundStartAntagsWeightTable = "SecretPlus"; // Keep using the old SecretPlus tables for now

    /// <summary>
    ///   List of chaos factors used when calculating change in chaos every tick.
    /// </summary>
    [DataField]
    public List<ChaosFactor> ChaosFactors = new();

    /// <summary>
    ///   Queued operations that will be applied on the next tick.
    ///   Typically created by admin commands to directly modify the chaos score.
    /// </summary>
    [ViewVariables]
    public readonly ConcurrentQueue<ChaosOperation> PendingOperations = new();
}
