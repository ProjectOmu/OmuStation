// SPDX-License-Identifier: MIT

namespace Content.Omu.Server.Crewlet;

[ByRefEvent]
public readonly record struct CrewletOpinionChangedEvent(
    EntityUid Person,
    float Opinion,
    CrewletOpinionReason Reason);

public enum CrewletOpinionReason
{
    Hugged,
    Fed,
    FedPoison,
    Hurt,
    Healed,
    Shoved,
    Medicated,
    InjectedPoison,
}
