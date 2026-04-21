using Content.Goobstation.Common.StationEvent.Metrics;
using Content.Omu.Server.GameDirector.Components;
using Content.Omu.Server.GameDirector.Metric;

namespace Content.Omu.Server.GameDirector;

/// <summary>
///   Pairs a PossibleEvent with the resultant chaos and a "score" for sorting by the GameDirector
///   Temporary class used in processing and ranking the list of event
///   lest do some reverse-engineering to do not add magic numbers
/// </summary>
public sealed class RankedEvent(PossibleEvent possibleEvent, ChaosMetrics result, float score)
{
    /// <summary>
    ///   Contains the StationEvent and expected chaos delta
    /// </summary>
    public readonly PossibleEvent PossibleEvent = possibleEvent;

    /// <summary>
    ///   Current chaos + PossibleEvent.Chaos at time of creation
    /// </summary>
    public readonly ChaosMetrics Result = result;

    /// <summary>
    ///   Preference for this RankedEvent, lower is better.
    ///   Essentially the "pain" of how far Result is from the StoryBeat.Goal
    /// </summary>
    public readonly float Score = score;
}

public sealed class PlayerCount
{
    public int Players;
    public int Ghosts;
}
