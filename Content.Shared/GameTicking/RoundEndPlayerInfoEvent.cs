// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Mind;

namespace Content.Shared.GameTicking;

/// <summary>
///     Raised (broadcast, server-side) once per mind while the round-end scoreboard is being built.
///     It is raised after <see cref="Info"/> has been filled in with the core per-player data and
///     immediately before that entry is committed to the <see cref="RoundEndMessageEvent"/> that gets
///     networked to clients. <see cref="MindId"/> says which mind the entry belongs to.
/// </summary>
/// <remarks>
///     <para>
///     This is the extension point for <b>per-player structured</b> round-end data. Subscribe with
///     <c>SubscribeLocalEvent&lt;RoundEndPlayerInfoEvent&gt;</c> from any assembly and mutate
///     <see cref="Info"/> in place; whatever a subscriber writes is what ends up on the wire. Use
///     this instead of editing <c>GameTicker.ShowRoundEndScoreboard</c>.
///     </para>
///     <para>
///     It is deliberately <b>broadcast</b>, not directed. The directed bus permits only one
///     subscription per (component, event) pair engine-wide
///     (<c>EntityEventBus.Directed.cs</c>, "Duplicate Subscriptions"), so a directed version of this
///     event could only ever have a single subscriber - which defeats the entire purpose of adding
///     an extension point that both forks, and future content, need to share.
///     </para>
///     <para>
///     If you only want to append free text to the round-end summary, use
///     <c>Content.Server.GameTicking.RoundEndTextAppendEvent</c> instead.
///     </para>
///     <para>
///     This is deliberately a class and not a <c>[ByRefEvent]</c> struct. <see cref="Info"/> is a
///     mutable struct <i>field</i> on a heap object, so every subscriber mutates the exact same
///     storage. Two subscribers annotating different fields therefore cannot clobber one another,
///     and the result is independent of the order in which subscribers run.
///     </para>
/// </remarks>
public sealed class RoundEndPlayerInfoEvent : EntityEventArgs
{
    /// <summary>
    ///     The mind entity whose summary entry this is. Same as the entity the event is raised on.
    /// </summary>
    public readonly EntityUid MindId;

    /// <summary>
    ///     The <see cref="MindComponent"/> on <see cref="MindId"/>.
    /// </summary>
    public readonly MindComponent Mind;

    /// <summary>
    ///     The summary entry for this mind, pre-populated with the core data. Subscribers should
    ///     assign directly to the fields of this (e.g. <c>args.Info.SomeField = ...</c>).
    /// </summary>
    public RoundEndMessageEvent.RoundEndPlayerInfo Info;

    public RoundEndPlayerInfoEvent(
        EntityUid mindId,
        MindComponent mind,
        RoundEndMessageEvent.RoundEndPlayerInfo info)
    {
        MindId = mindId;
        Mind = mind;
        Info = info;
    }
}
