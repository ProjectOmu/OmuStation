using Content.Shared.Chat;
using Content.Shared.Radio;

namespace Content.Server._Mono.CorticalBorer;

/// <summary>
/// Transfers radio messages heard by an entity to another source, allowing another entity to hear what another entity hears over comms.
/// </summary>
[ByRefEvent]
public record struct RadioMessageHeardEvent(
    EntityUid Headset,
    MsgChatMessage Msg,
    RadioChannelPrototype Channel
);
